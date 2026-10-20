using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RetailQ.DecisionLab.Core;
using RetailQ.DecisionLab.Core.AI;

namespace RetailQ.DecisionLab.Infrastructure.AI;

/// <summary>
/// Thin server-side Responses API adapter. It uses raw HTTP rather than coupling Core/UI to an
/// SDK. The request uses <c>store:false</c> and strict JSON Schema; provider errors are returned
/// as controlled result states, never allowed to change a replenishment decision.
/// </summary>
public sealed class OpenAiDecisionAssistant(
    HttpClient httpClient,
    LlmOptions options,
    DecisionPromptBuilder promptBuilder,
    DecisionResponseValidator validator,
    ILogger<OpenAiDecisionAssistant> logger) : ILlmDecisionAssistant
{
    private readonly LlmOptions _options = options;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<AiExplanationResult> ExplainAsync(DecisionSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        var correlationId = Guid.NewGuid().ToString("N");
        var apiKey = string.IsNullOrWhiteSpace(_options.ApiKey)
            ? Environment.GetEnvironmentVariable("OPENAI_API_KEY")
            : _options.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
            return new(AiExplanationStatus.Unavailable, UserMessage: "AI-Erklärung ist nicht konfiguriert.", CorrelationId: correlationId);

        var started = Stopwatch.StartNew();
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "v1/responses");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            request.Content = new StringContent(JsonSerializer.Serialize(new
            {
                model = _options.Model,
                store = false,
                instructions = promptBuilder.BuildInstructions(),
                input = promptBuilder.BuildInput(snapshot),
                max_output_tokens = 350,
                // Narrowing the schema to this snapshot prevents the provider from selecting a
                // generally known but factually inapplicable driver or action. The validator below
                // remains the independent business-rule boundary (defence in depth).
                text = new { format = JsonSchema(snapshot) }
            }), Encoding.UTF8, "application/json");

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(_options.TimeoutSeconds));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
            using var response = await httpClient.SendAsync(request, linked.Token);
            var payload = await response.Content.ReadAsStringAsync(linked.Token);
            if (!response.IsSuccessStatusCode)
            {
                var providerError = ParseProviderError(payload);
                var requestId = response.Headers.TryGetValues("x-request-id", out var requestIds)
                    ? requestIds.FirstOrDefault()
                    : null;
                logger.LogWarning(
                    "LLM request {CorrelationId} failed with HTTP {StatusCode}, provider type {ErrorType}, code {ErrorCode}, request {RequestId} after {ElapsedMs}ms.",
                    correlationId, (int)response.StatusCode, providerError.Type, providerError.Code, requestId, started.ElapsedMilliseconds);
                return new(AiExplanationStatus.Failed,
                    UserMessage: ProviderErrorMessage(providerError.Code),
                    CorrelationId: correlationId);
            }

            var modelJson = ExtractOutputText(payload);
            var modelResponse = JsonSerializer.Deserialize<LlmExplanationDto>(modelJson, JsonOptions)
                ?? throw new InvalidDataException("LLM response did not contain an explanation object.");
            var explanation = validator.Validate(snapshot, modelResponse, _options.Model);
            logger.LogInformation("LLM request {CorrelationId} completed in {ElapsedMs}ms using {Model}.", correlationId, started.ElapsedMilliseconds, _options.Model);
            return new(AiExplanationStatus.Completed, explanation, CorrelationId: correlationId);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("LLM request {CorrelationId} timed out after {ElapsedMs}ms.", correlationId, started.ElapsedMilliseconds);
            return new(AiExplanationStatus.Failed, UserMessage: "AI-Erklärung hat das Zeitlimit überschritten.", CorrelationId: correlationId);
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or InvalidDataException)
        {
            logger.LogWarning(exception, "LLM request {CorrelationId} returned invalid provider data after {ElapsedMs}ms.", correlationId, started.ElapsedMilliseconds);
            return new(AiExplanationStatus.Failed, UserMessage: "AI-Erklärung konnte nicht validiert werden.", CorrelationId: correlationId);
        }
    }

    private static object JsonSchema(DecisionSnapshot snapshot)
    {
        var allowedDriverCodes = new List<string>();
        if (snapshot.Decision.Status == DecisionStatus.Critical) allowedDriverCodes.Add("COVERAGE_BELOW_LEAD_TIME");
        if (snapshot.Decision.ReorderQuantity > 0) allowedDriverCodes.Add("SAFETY_BUFFER");
        if (snapshot.Decision.Status == DecisionStatus.NoCurrentDemand) allowedDriverCodes.Add("NO_CURRENT_DEMAND");
        if (snapshot.Decision.Status == DecisionStatus.Stable) allowedDriverCodes.Add("STABLE_STOCK");

        var expectedAction = snapshot.Decision.ReorderQuantity > 0 ? "REORDER" : "NO_ACTION";
        return new
        {
            type = "json_schema", name = "decision_explanation", strict = true,
            schema = new
            {
                type = "object", additionalProperties = false,
                properties = new
                {
                    summary = new { type = "string" },
                    driverCodes = new { type = "array", items = new { type = "string", @enum = allowedDriverCodes }, minItems = 1 },
                    riskNote = new { type = "string" },
                    recommendedAction = new { type = "string", @enum = new[] { expectedAction } }
                },
                required = new[] { "summary", "driverCodes", "riskNote", "recommendedAction" }
            }
        };
    }

    private static string ExtractOutputText(string responseJson)
    {
        using var response = JsonDocument.Parse(responseJson);
        foreach (var item in response.RootElement.GetProperty("output").EnumerateArray())
        foreach (var content in item.GetProperty("content").EnumerateArray())
            if (content.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
                return text.GetString()!;
        throw new InvalidDataException("Responses API returned no output text.");
    }

    /// <summary>
    /// Extracts only non-sensitive machine-readable error classification. Provider messages
    /// and request payloads are deliberately not propagated to UI or normal application logs.
    /// </summary>
    private static ProviderError ParseProviderError(string responseJson)
    {
        try
        {
            using var response = JsonDocument.Parse(responseJson);
            if (!response.RootElement.TryGetProperty("error", out var error)) return new(null, null);
            var type = error.TryGetProperty("type", out var typeValue) ? typeValue.GetString() : null;
            var code = error.TryGetProperty("code", out var codeValue) && codeValue.ValueKind == JsonValueKind.String
                ? codeValue.GetString()
                : null;
            return new(type, code);
        }
        catch (JsonException)
        {
            return new(null, null);
        }
    }

    private static string ProviderErrorMessage(string? code) => code switch
    {
        "credit_balance_exhausted" => "Das API-Guthaben des OpenAI-Projekts ist ausgeschöpft.",
        "project_spend_limit_exceeded" => "Das Ausgabenlimit des OpenAI-Projekts ist erreicht.",
        "organization_spend_limit_exceeded" => "Das Ausgabenlimit der OpenAI-Organisation ist erreicht.",
        "organization_usage_limit_exceeded" => "Das Nutzungslimit der OpenAI-Organisation ist erreicht.",
        "slow_down" => "Der OpenAI-Provider drosselt Anfragen vorübergehend.",
        _ => "AI-Erklärung ist vorübergehend nicht verfügbar."
    };

    private sealed record ProviderError(string? Type, string? Code);
}

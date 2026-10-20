using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using RetailQ.DecisionLab.Core.AI;

namespace RetailQ.DecisionLab.Infrastructure.AI;

/// <summary>
/// OpenAI is used only at the natural-language boundary. Strict Structured Outputs constrain the
/// wire shape; ScenarioIntentValidator independently enforces domain ranges before calculation.
/// </summary>
public sealed class OpenAiScenarioIntentInterpreter(
    HttpClient httpClient,
    LlmOptions options,
    ScenarioIntentPromptBuilder promptBuilder,
    ILogger<OpenAiScenarioIntentInterpreter> logger) : IScenarioIntentInterpreter
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public async Task<ScenarioInterpretationResult> InterpretAsync(string question, CancellationToken cancellationToken = default)
    {
        var correlationId = Guid.NewGuid().ToString("N");
        var apiKey = string.IsNullOrWhiteSpace(options.ApiKey)
            ? Environment.GetEnvironmentVariable("OPENAI_API_KEY")
            : options.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
            return new(ScenarioInterpreterStatus.Unavailable, UserMessage: "Szenarioanalyse ist nicht konfiguriert.", CorrelationId: correlationId);

        var started = Stopwatch.StartNew();
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "v1/responses");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            request.Content = new StringContent(JsonSerializer.Serialize(new
            {
                model = options.Model,
                store = false,
                instructions = promptBuilder.BuildInstructions(),
                input = promptBuilder.BuildInput(question),
                max_output_tokens = 250,
                text = new { format = JsonSchema() }
            }), Encoding.UTF8, "application/json");

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(options.TimeoutSeconds));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
            using var response = await httpClient.SendAsync(request, linked.Token);
            var payload = await response.Content.ReadAsStringAsync(linked.Token);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Scenario intent {CorrelationId} failed with HTTP {StatusCode} after {ElapsedMs}ms.",
                    correlationId, (int)response.StatusCode, started.ElapsedMilliseconds);
                return new(ScenarioInterpreterStatus.Failed, UserMessage: "Die Szenariofrage konnte vorübergehend nicht interpretiert werden.", CorrelationId: correlationId);
            }

            var output = ExtractOutputText(payload);
            if (output is null)
                return new(ScenarioInterpreterStatus.Failed, UserMessage: "Das Modell hat die Szenariofrage nicht beantwortet.", CorrelationId: correlationId);
            var intent = JsonSerializer.Deserialize<ScenarioIntent>(output, JsonOptions)
                ?? throw new InvalidDataException("Scenario response did not contain an intent object.");
            logger.LogInformation("Scenario intent {CorrelationId} completed in {ElapsedMs}ms using {Model}.",
                correlationId, started.ElapsedMilliseconds, options.Model);
            return new(ScenarioInterpreterStatus.Completed, intent, CorrelationId: correlationId);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Scenario intent {CorrelationId} timed out after {ElapsedMs}ms.", correlationId, started.ElapsedMilliseconds);
            return new(ScenarioInterpreterStatus.Failed, UserMessage: "Die Szenarioanalyse hat das Zeitlimit überschritten.", CorrelationId: correlationId);
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or InvalidDataException)
        {
            logger.LogWarning(exception, "Scenario intent {CorrelationId} returned invalid provider data after {ElapsedMs}ms.",
                correlationId, started.ElapsedMilliseconds);
            return new(ScenarioInterpreterStatus.Failed, UserMessage: "Die Modellantwort konnte nicht sicher validiert werden.", CorrelationId: correlationId);
        }
    }

    private static object JsonSchema() => new
    {
        type = "json_schema",
        name = "scenario_intent",
        strict = true,
        schema = new
        {
            type = "object",
            additionalProperties = false,
            properties = new
            {
                status = new { type = "string", description = "Simulate when one supported change and numeric value are explicit; clarify only when information is missing.", @enum = new[] { "Simulate", "ClarificationRequired", "Unsupported" } },
                parameter = new
                {
                    anyOf = new object[]
                    {
                        new { type = "string", @enum = new[] { "LeadTimeDeltaDays", "DemandChangePercent", "StockDeltaUnits" } },
                        new { type = "null" }
                    }
                },
                value = new { anyOf = new object[] { new { type = "number" }, new { type = "null" } } },
                interpretation = new { anyOf = new object[] { new { type = "string" }, new { type = "null" } } },
                clarificationQuestion = new { anyOf = new object[] { new { type = "string" }, new { type = "null" } } }
            },
            required = new[] { "status", "parameter", "value", "interpretation", "clarificationQuestion" }
        }
    };

    private static string? ExtractOutputText(string responseJson)
    {
        using var response = JsonDocument.Parse(responseJson);
        if (!response.RootElement.TryGetProperty("output", out var output)) return null;
        foreach (var item in output.EnumerateArray())
        {
            if (!item.TryGetProperty("content", out var contentItems)) continue;
            foreach (var content in contentItems.EnumerateArray())
            {
                if (content.TryGetProperty("refusal", out var refusal) && refusal.ValueKind == JsonValueKind.String) return null;
                if (content.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String) return text.GetString();
            }
        }
        return null;
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}

using RetailQ.DecisionLab.Core;
using RetailQ.DecisionLab.Core.AI;

namespace RetailQ.DecisionLab.Infrastructure.AI;

/// <summary>
/// Structured Outputs guarantees JSON shape, not business truth. This validator binds every
/// model-proposed reason back to the deterministic snapshot before it can reach the UI.
/// </summary>
public sealed class DecisionResponseValidator
{
    private static readonly HashSet<string> KnownCodes =
    ["COVERAGE_BELOW_LEAD_TIME", "SAFETY_BUFFER", "NO_CURRENT_DEMAND", "STABLE_STOCK"];

    public DecisionExplanation Validate(DecisionSnapshot snapshot, LlmExplanationDto response, string model)
    {
        if (string.IsNullOrWhiteSpace(response.Summary) || string.IsNullOrWhiteSpace(response.RiskNote))
            throw new InvalidDataException("LLM explanation is missing required text.");
        if (response.DriverCodes.Count == 0 || response.DriverCodes.Any(code => !KnownCodes.Contains(code)))
            throw new InvalidDataException("LLM explanation contains an unknown driver code.");

        var allowed = AllowedCodes(snapshot);
        if (response.DriverCodes.Any(code => !allowed.Contains(code)))
            throw new InvalidDataException("LLM explanation contains a driver code unsupported by the decision facts.");

        var expectedAction = snapshot.Decision.ReorderQuantity > 0 ? "REORDER" : "NO_ACTION";
        if (!string.Equals(response.RecommendedAction, expectedAction, StringComparison.Ordinal))
            throw new InvalidDataException("LLM attempted to change the deterministic recommended action.");

        return new DecisionExplanation(response.Summary.Trim(), response.DriverCodes, response.RiskNote.Trim(), expectedAction, model);
    }

    private static HashSet<string> AllowedCodes(DecisionSnapshot snapshot)
    {
        var codes = new HashSet<string>();
        if (snapshot.Decision.Status == DecisionStatus.Critical) codes.Add("COVERAGE_BELOW_LEAD_TIME");
        if (snapshot.Decision.ReorderQuantity > 0) codes.Add("SAFETY_BUFFER");
        if (snapshot.Decision.Status == DecisionStatus.NoCurrentDemand) codes.Add("NO_CURRENT_DEMAND");
        if (snapshot.Decision.Status == DecisionStatus.Stable) codes.Add("STABLE_STOCK");
        return codes;
    }
}

/// <summary>Transport DTO matching the strict JSON schema sent to the Responses API.</summary>
public sealed record LlmExplanationDto(string Summary, List<string> DriverCodes, string RiskNote, string RecommendedAction);

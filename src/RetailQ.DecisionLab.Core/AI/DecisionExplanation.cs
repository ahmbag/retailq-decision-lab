namespace RetailQ.DecisionLab.Core.AI;

public enum AiExplanationStatus { Completed, Unavailable, Failed }

/// <summary>Validated, presentation-ready LLM content. Numeric recommendations remain in DecisionSnapshot.</summary>
public sealed record DecisionExplanation(
    string Summary,
    IReadOnlyList<string> DriverCodes,
    string RiskNote,
    string RecommendedAction,
    string Model);

public sealed record AiExplanationResult(
    AiExplanationStatus Status,
    DecisionExplanation? Explanation = null,
    string? UserMessage = null,
    string? CorrelationId = null);

public interface ILlmDecisionAssistant
{
    Task<AiExplanationResult> ExplainAsync(DecisionSnapshot snapshot, CancellationToken cancellationToken = default);
}

namespace RetailQ.DecisionLab.Core.AI;

/// <summary>
/// Immutable and deliberately small fact set given to an LLM. It contains the decision
/// already made by the deterministic engine; it is not an instruction to calculate one.
/// No SAP payloads, credentials or free-form ERP texts cross this boundary.
/// </summary>
public sealed record DecisionSnapshot(
    string ProductId,
    string ProductName,
    string Source,
    DecisionSnapshotFacts Decision,
    string RuleVersion = "1.0");

public sealed record DecisionSnapshotFacts(
    DecisionStatus Status,
    int? AvailableStock,
    int? BlockedStock,
    decimal? AverageDailyDemand,
    decimal? CoverageDays,
    int LeadTimeDays,
    int SafetyDays,
    int? TargetStock,
    int? ReorderQuantity,
    decimal? ProjectedArrivalStock);

public static class DecisionSnapshotBuilder
{
    /// <summary>
    /// A scenario retains the original product for baseline comparison. Its effective inputs
    /// therefore come from SimulatedInputs, not from Simulated.Product. Passing the latter
    /// would explain a seven-day delivery using the original five-day lead time.
    /// Only the explanation snapshot is copied; neither baseline nor rule output is mutated.
    /// </summary>
    public static DecisionSnapshot FromScenario(ScenarioComparison comparison)
    {
        var snapshot = FromDecision(comparison.Simulated);
        var inputs = comparison.SimulatedInputs;
        return snapshot with { Decision = snapshot.Decision with
        {
            AvailableStock = inputs.AvailableStock,
            AverageDailyDemand = inputs.DailyDemand,
            LeadTimeDays = inputs.LeadTimeDays,
            SafetyDays = inputs.SafetyDays
        }};
    }

    public static DecisionSnapshot FromDecision(ReplenishmentDecision decision) => new(
        decision.Product.Id,
        decision.Product.Name,
        decision.Product.Source,
        new DecisionSnapshotFacts(
            decision.Status,
            decision.Product.UnrestrictedStock,
            decision.Product.BlockedStock,
            decision.DailyDemand,
            decision.CoverageDays,
            decision.Product.LeadTimeDays,
            decision.Product.SafetyDays,
            decision.TargetStock,
            decision.ReorderQuantity,
            decision.ProjectedArrivalStock));
}

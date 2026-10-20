using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace RetailQ.DecisionLab.Core.AI;

public enum ScenarioIntentStatus { Simulate, ClarificationRequired, Unsupported }
public enum ScenarioParameter { LeadTimeDeltaDays, DemandChangePercent, StockDeltaUnits }
public enum ScenarioInterpreterStatus { Completed, Unavailable, Failed }
public enum ScenarioAnalysisStatus
{
    Completed,
    ClarificationRequired,
    Unsupported,
    ValidationFailed,
    BaselineConflict,
    NotEvaluable,
    ProviderUnavailable,
    ProviderFailed
}

/// <summary>
/// The LLM may describe only the user's intent. It never returns stock decisions or order values.
/// Nullable members are required by the provider schema but remain empty for clarification/error cases.
/// </summary>
public sealed record ScenarioIntent(
    ScenarioIntentStatus Status,
    ScenarioParameter? Parameter,
    decimal? Value,
    string? Interpretation,
    string? ClarificationQuestion);

public sealed record ScenarioOverlay(
    int? LeadTimeDeltaDays = null,
    decimal? DemandChangePercent = null,
    int? StockDeltaUnits = null);

public sealed record ScenarioComparison(
    ReplenishmentDecision Baseline,
    ReplenishmentDecision Simulated,
    ScenarioCalculationInputs BaselineInputs,
    ScenarioCalculationInputs SimulatedInputs,
    decimal ReorderQuantityDelta,
    decimal ArrivalStockDelta,
    string ScenarioDescription,
    string BaselineFingerprint);

/// <summary>Effective calculation inputs are explicit; SAP baseline data itself is never rewritten.</summary>
public sealed record ScenarioCalculationInputs(
    int AvailableStock,
    decimal DailyDemand,
    int LeadTimeDays,
    int SafetyDays);

public sealed record ScenarioInterpretationResult(
    ScenarioInterpreterStatus Status,
    ScenarioIntent? Intent = null,
    string? UserMessage = null,
    string? CorrelationId = null);

public sealed record ScenarioAnalysisResult(
    ScenarioAnalysisStatus Status,
    string DataSource,
    string BaselineFingerprint,
    ScenarioIntent? Interpretation = null,
    ScenarioComparison? Comparison = null,
    AiExplanationResult? Explanation = null,
    string? UserMessage = null);

public interface IScenarioIntentInterpreter
{
    Task<ScenarioInterpretationResult> InterpretAsync(string question, CancellationToken cancellationToken = default);
}

public interface IScenarioAnalysisService
{
    Task<ScenarioAnalysisResult> AnalyzeAsync(ProductSnapshot product, string question,
        string? expectedBaselineFingerprint, CancellationToken cancellationToken = default);
}

/// <summary>
/// Converts a validated, single-parameter intent into an immutable overlay. The limits are explicit
/// MVP demonstration rules and are enforced independently from the probabilistic provider.
/// </summary>
public sealed class ScenarioIntentValidator
{
    public (ScenarioOverlay? Overlay, string? Error) Validate(ProductSnapshot product, ScenarioIntent intent)
    {
        if (intent.Status != ScenarioIntentStatus.Simulate)
            return (null, "Nur ein Simulations-Intent kann ausgewertet werden.");
        if (intent.Parameter is null || intent.Value is null)
            return (null, "Simulationsparameter und Wert fehlen.");

        var value = intent.Value.Value;
        ScenarioOverlay overlay;
        switch (intent.Parameter.Value)
        {
            case ScenarioParameter.LeadTimeDeltaDays:
                if (value != decimal.Truncate(value)) return (null, "Die Lieferzeitänderung muss aus ganzen Tagen bestehen.");
                if (value is < -14 or > 14) return (null, "Die Lieferzeitänderung muss zwischen -14 und +14 Tagen liegen.");
                var leadTime = product.LeadTimeDays + (int)value;
                if (leadTime is < 1 or > 30) return (null, "Die resultierende Lieferzeit muss zwischen 1 und 30 Tagen liegen.");
                overlay = new(LeadTimeDeltaDays: (int)value);
                break;
            case ScenarioParameter.DemandChangePercent:
                if (value is < -50 or > 100) return (null, "Die Nachfrageänderung muss zwischen -50 % und +100 % liegen.");
                overlay = new(DemandChangePercent: value);
                break;
            case ScenarioParameter.StockDeltaUnits:
                if (value != decimal.Truncate(value)) return (null, "Die Bestandsänderung muss aus ganzen Stück bestehen.");
                if (value is < -200 or > 200) return (null, "Die Bestandsänderung muss zwischen -200 und +200 Stück liegen.");
                if (product.UnrestrictedStock is null || product.UnrestrictedStock + value < 0)
                    return (null, "Der simulierte verfügbare Bestand darf nicht negativ sein.");
                overlay = new(StockDeltaUnits: (int)value);
                break;
            default:
                return (null, "Der Simulationsparameter wird nicht unterstützt.");
        }

        return ValidateOverlay(product, overlay);
    }

    public (ScenarioOverlay? Overlay, string? Error) ValidateOverlay(ProductSnapshot product, ScenarioOverlay overlay)
    {
        var changedParameters = new object?[] { overlay.LeadTimeDeltaDays, overlay.DemandChangePercent, overlay.StockDeltaUnits }
            .Count(value => value is not null);
        if (changedParameters != 1)
            return (null, "Im MVP muss genau ein Simulationsparameter gesetzt sein.");
        return (overlay, null);
    }
}

/// <summary>
/// Applies an overlay only as arguments to the existing Decision Engine. ProductSnapshot is immutable,
/// so neither fixture data nor the baseline product can be changed by a simulation.
/// </summary>
public sealed class ScenarioEvaluationService(IDecisionEngine engine, ScenarioIntentValidator validator)
{
    public ScenarioAnalysisResult Evaluate(ProductSnapshot product, ScenarioIntent intent, string? expectedFingerprint)
    {
        var baseline = engine.Evaluate(product);
        var fingerprint = BaselineFingerprint.Create(baseline);

        if (!string.IsNullOrWhiteSpace(expectedFingerprint) &&
            !string.Equals(expectedFingerprint, fingerprint, StringComparison.Ordinal))
            return Result(ScenarioAnalysisStatus.BaselineConflict, "Die Ausgangsdaten haben sich geändert. Bitte die Seite aktualisieren.");

        if (baseline.Status is DecisionStatus.NotEvaluable or DecisionStatus.DataError)
            return Result(ScenarioAnalysisStatus.NotEvaluable, "Die Ausgangsdaten sind für eine Simulation nicht zuverlässig genug.");
        if (intent.Status == ScenarioIntentStatus.ClarificationRequired)
            return Result(ScenarioAnalysisStatus.ClarificationRequired,
                intent.ClarificationQuestion ?? "Welche konkrete Änderung soll simuliert werden?");
        if (intent.Status == ScenarioIntentStatus.Unsupported)
            return Result(ScenarioAnalysisStatus.Unsupported, "Diese Frage gehört nicht zu den unterstützten What-if-Szenarien.");

        var (overlay, error) = validator.Validate(product, intent);
        if (overlay is null) return Result(ScenarioAnalysisStatus.ValidationFailed, error);

        var simulated = engine.Evaluate(
            product,
            overlay.LeadTimeDeltaDays ?? 0,
            1m + (overlay.DemandChangePercent ?? 0m) / 100m,
            overlay.StockDeltaUnits ?? 0);
        if (simulated.Status is DecisionStatus.NotEvaluable or DecisionStatus.DataError)
            return Result(ScenarioAnalysisStatus.ValidationFailed, "Die simulierten Werte ergeben keine auswertbare Entscheidung.");

        var comparison = new ScenarioComparison(
            baseline,
            simulated,
            new(product.UnrestrictedStock!.Value, baseline.DailyDemand ?? 0m, product.LeadTimeDays, product.SafetyDays),
            new(product.UnrestrictedStock.Value + (overlay.StockDeltaUnits ?? 0), simulated.DailyDemand ?? 0m,
                product.LeadTimeDays + (overlay.LeadTimeDeltaDays ?? 0), product.SafetyDays),
            (simulated.ReorderQuantity ?? 0) - (baseline.ReorderQuantity ?? 0),
            (simulated.ProjectedArrivalStock ?? 0) - (baseline.ProjectedArrivalStock ?? 0),
            intent.Interpretation ?? Describe(overlay),
            fingerprint);
        return new(ScenarioAnalysisStatus.Completed, product.Source, fingerprint, intent, comparison);

        ScenarioAnalysisResult Result(ScenarioAnalysisStatus status, string? message) =>
            new(status, product.Source, fingerprint, intent, UserMessage: message);
    }

    private static string Describe(ScenarioOverlay overlay) => overlay switch
    {
        { LeadTimeDeltaDays: { } value } => $"Lieferzeitänderung: {value:+#;-#;0} Tage.",
        { DemandChangePercent: { } value } => $"Nachfrageänderung: {value:+0.##;-0.##;0} %.",
        { StockDeltaUnits: { } value } => $"Bestandsänderung: {value:+#;-#;0} Stück.",
        _ => "What-if-Simulation"
    };
}

public sealed class ScenarioAnalysisService(
    IScenarioIntentInterpreter interpreter,
    ScenarioEvaluationService evaluation,
    ILlmDecisionAssistant explanationAssistant,
    IDecisionEngine engine) : IScenarioAnalysisService
{
    public async Task<ScenarioAnalysisResult> AnalyzeAsync(ProductSnapshot product, string question,
        string? expectedBaselineFingerprint, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(question))
            return new(ScenarioAnalysisStatus.ClarificationRequired, product.Source,
                BaselineFingerprint.Create(engine.Evaluate(product)),
                UserMessage: "Bitte eine konkrete What-if-Frage eingeben.");

        var interpretation = await interpreter.InterpretAsync(question.Trim(), cancellationToken);
        if (interpretation.Status != ScenarioInterpreterStatus.Completed || interpretation.Intent is null)
        {
            var status = interpretation.Status == ScenarioInterpreterStatus.Unavailable
                ? ScenarioAnalysisStatus.ProviderUnavailable : ScenarioAnalysisStatus.ProviderFailed;
            return new(status, product.Source,
                BaselineFingerprint.Create(engine.Evaluate(product)),
                UserMessage: interpretation.UserMessage ?? "Die Szenariofrage konnte nicht interpretiert werden.");
        }

        var result = evaluation.Evaluate(product, interpretation.Intent, expectedBaselineFingerprint);
        if (result.Status != ScenarioAnalysisStatus.Completed || result.Comparison is null) return result;

        // Explanation is deliberately best effort: deterministic numbers survive provider failure.
        var explanation = await explanationAssistant.ExplainAsync(
            DecisionSnapshotBuilder.FromScenario(result.Comparison), cancellationToken);
        return result with { Explanation = explanation };
    }
}

public static class BaselineFingerprint
{
    public static string Create(ReplenishmentDecision decision)
    {
        var product = decision.Product;
        var values = string.Join('|', new[]
        {
            product.Id,
            product.Source,
            product.UnrestrictedStock?.ToString(CultureInfo.InvariantCulture) ?? "null",
            product.BlockedStock?.ToString(CultureInfo.InvariantCulture) ?? "null",
            string.Join(',', product.SalesHistory),
            product.LeadTimeDays.ToString(CultureInfo.InvariantCulture),
            product.SafetyDays.ToString(CultureInfo.InvariantCulture),
            decision.ReorderQuantity?.ToString(CultureInfo.InvariantCulture) ?? "null",
            "rule-1.0"
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(values)))[..16].ToLowerInvariant();
    }
}

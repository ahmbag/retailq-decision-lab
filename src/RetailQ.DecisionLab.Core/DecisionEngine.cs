namespace RetailQ.DecisionLab.Core;

public sealed class DecisionEngine : IDecisionEngine
{
    public ReplenishmentDecision Evaluate(ProductSnapshot product, int leadTimeDeltaDays = 0,
        decimal demandMultiplier = 1m, int stockDelta = 0)
    {
        if (product.UnrestrictedStock is null || product.SalesHistory.Count != 14)
            return Result(DecisionStatus.NotEvaluable, null, null, null, null, null, "Vollständiger Bestand oder 14 Verkaufstage fehlen.");
        var stock = product.UnrestrictedStock.Value + stockDelta;
        if (stock < 0 || demandMultiplier < 0 || product.LeadTimeDays + leadTimeDeltaDays < 0)
            return Result(DecisionStatus.DataError, null, null, null, null, null, "Ungültige Bestands- oder Szenariowerte.");

        var dailyDemand = product.SalesHistory.Sum() / 14m * demandMultiplier;
        if (dailyDemand == 0)
            return Result(DecisionStatus.NoCurrentDemand, 0, null, stock, 0, stock, "Kein aktueller Absatz über 14 Kalendertage.");

        var leadTime = product.LeadTimeDays + leadTimeDeltaDays;
        var coverage = Math.Round(stock / dailyDemand, 1, MidpointRounding.AwayFromZero);
        var target = (int)Math.Ceiling(dailyDemand * (leadTime + product.SafetyDays));
        var reorder = Math.Max(0, target - stock);
        var arrival = stock - dailyDemand * leadTime;
        var status = stock < dailyDemand * leadTime ? DecisionStatus.Critical
            : stock < target ? DecisionStatus.Reorder : DecisionStatus.Stable;
        var reason = status switch
        {
            DecisionStatus.Critical => "Der Bestand deckt den erwarteten Verbrauch bis zur Nachlieferung nicht.",
            DecisionStatus.Reorder => "Der Bestand deckt die Lieferzeit, liegt aber unter dem Sicherheitsziel.",
            _ => "Der Bestand erreicht das Ziel einschließlich Sicherheitspuffer."
        };
        return Result(status, dailyDemand, coverage, target, reorder, arrival, reason);

        ReplenishmentDecision Result(DecisionStatus status, decimal? demand, decimal? coverage, int? target,
            int? quantity, decimal? arrival, string reason) => new(product, status, demand, coverage, target, quantity, arrival, reason);
    }
}

namespace RetailQ.DecisionLab.Core;

public sealed record ProductSnapshot(
    string Id, string Sku, string Name, string Category, string Unit,
    int? UnrestrictedStock, IReadOnlyList<int> SalesHistory,
    int LeadTimeDays, int SafetyDays, string Plant = "001", string StorageLocation = "0001",
    int? BlockedStock = null, string Source = "DEMO_DATA",
    string? ProductType = null, bool IsReplenishable = true,
    string MasterDataSource = "SYNTHETIC", string StockDataSource = "SYNTHETIC",
    string SalesDataSource = "SYNTHETIC");

/// <summary>
/// Describes the active provider independently from its products. The UI uses this contract to
/// distinguish a working sandbox, an explicit fixture and a failed/not-configured remote source.
/// </summary>
public sealed record RetailDataSourceStatus(
    string Mode, string Label, string Description, bool IsRemote, bool IsHealthy,
    int ProductCount = 0, int StockRecordCount = 0, string? Error = null,
    DateTimeOffset? RefreshedAtUtc = null);

public enum DecisionStatus { Critical, Reorder, Stable, NoCurrentDemand, NotEvaluable, DataError }

public sealed record ReplenishmentDecision(
    ProductSnapshot Product, DecisionStatus Status, decimal? DailyDemand,
    decimal? CoverageDays, int? TargetStock, int? ReorderQuantity,
    decimal? ProjectedArrivalStock, string Reason);

public interface IRetailDataProvider
{
    RetailDataSourceStatus Status { get; }
    IReadOnlyList<ProductSnapshot> GetProducts();
    ProductSnapshot? GetProduct(string id);
}

public interface IDecisionEngine
{
    ReplenishmentDecision Evaluate(ProductSnapshot product, int leadTimeDeltaDays = 0,
        decimal demandMultiplier = 1m, int stockDelta = 0);
}

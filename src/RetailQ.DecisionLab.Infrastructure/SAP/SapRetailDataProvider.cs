using Microsoft.Extensions.Logging;
using RetailQ.DecisionLab.Core;
using RetailQ.DecisionLab.Infrastructure.SAP.Mapping;
using RetailQ.DecisionLab.Infrastructure.SAP.Models;

namespace RetailQ.DecisionLab.Infrastructure.SAP;

/// <summary>
/// One provider boundary for explicit Fixture and Sandbox modes. Remote failures are represented
/// in Status and never trigger an implicit fallback to packaged fixtures.
/// </summary>
public sealed class SapRetailDataProvider : IRetailDataProvider
{
    private readonly IReadOnlyList<ProductSnapshot> _products;
    public RetailDataSourceStatus Status { get; }

    /// <summary>Builds the deterministic offline provider used by regression tests and demos.</summary>
    public SapRetailDataProvider(
        IRetailDataProvider replenishmentProfiles,
        SapProductClient? productClient = null,
        SapMaterialStockClient? stockClient = null,
        SapProductMapper? mapper = null)
    {
        productClient ??= new SapProductClient();
        stockClient ??= new SapMaterialStockClient();
        mapper ??= new SapProductMapper();

        var profiles = replenishmentProfiles.GetProducts().ToDictionary(x => x.Id, StringComparer.Ordinal);
        var products = productClient.ParseV4Response(SapFixtureData.ProductsV4());
        var stock = stockClient.ParseV2Response(SapFixtureData.MaterialStockV2());
        _products = products.Select(product =>
        {
            if (product.Product is null || !profiles.TryGetValue(product.Product, out var profile))
                throw new InvalidDataException($"No replenishment profile exists for SAP product '{product.Product}'.");
            return mapper.Map(product, stock, profile);
        }).ToList();
        Status = new("Fixture", "SAP ODATA FIXTURE",
            "Lokale, versionierte OData-Vertragsdaten. Es findet kein Remote-Zugriff statt.",
            false, true, _products.Count, stock.Count, RefreshedAtUtc: DateTimeOffset.UtcNow);
    }

    private SapRetailDataProvider(IReadOnlyList<ProductSnapshot> products, RetailDataSourceStatus status)
    {
        _products = products;
        Status = status;
    }

    public static SapRetailDataProvider Unavailable(string mode, string message) => new([], new(
        mode, $"SAP {mode.ToUpperInvariant()} · NICHT AKTIV", message,
        !string.Equals(mode, "Fixture", StringComparison.OrdinalIgnoreCase), false, Error: message,
        RefreshedAtUtc: DateTimeOffset.UtcNow));

    public static async Task<SapRetailDataProvider> CreateSandboxAsync(
        IRetailDataProvider demoProfiles,
        SapProductClient productClient,
        SapMaterialStockClient stockClient,
        SapProductMapper mapper,
        SapOptions options,
        ILogger<SapRetailDataProvider> logger,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<SapProductDto> products;
        IReadOnlyDictionary<string, string> descriptions;
        try
        {
            products = await productClient.GetProductsAsync(cancellationToken);
            descriptions = await productClient.GetGermanDescriptionsAsync(cancellationToken);
        }
        catch (SapRemoteException ex)
        {
            logger.LogWarning(ex, "SAP Product sandbox request failed with category {FailureKind}", ex.Kind);
            return Failed(ex.Message);
        }

        IReadOnlyList<SapMaterialStockDto> stocks = [];
        string? stockError = null;
        try
        {
            stocks = await stockClient.GetStocksAsync(cancellationToken);
        }
        catch (SapRemoteException ex)
        {
            stockError = ex.Message;
            logger.LogWarning(ex, "SAP Material Stock sandbox request failed with category {FailureKind}", ex.Kind);
        }

        var profiles = demoProfiles.GetProducts().ToDictionary(x => x.Id, StringComparer.Ordinal);
        var mapped = new List<ProductSnapshot>();
        foreach (var raw in products)
        {
            if (string.IsNullOrWhiteSpace(raw.Product)) continue;
            var product = descriptions.TryGetValue(raw.Product, out var description)
                ? raw with { ProductDescription = description }
                : raw;
            ProductSnapshot? profile = null;
            if (options.DemoProductMappings.TryGetValue(raw.Product, out var demoId))
            {
                if (!profiles.TryGetValue(demoId, out profile))
                    throw new InvalidDataException(
                        $"Explicit SAP mapping for '{raw.Product}' references unknown demo product '{demoId}'.");
            }

            mapped.Add(mapper.MapRemote(product, stocks, profile,
                options.StockSelection.Plant, options.StockSelection.StorageLocation));
        }

        var healthy = stockError is null;
        var descriptionText = healthy
            ? "Produktstamm und Bestand wurden live aus der SAP-gehosteten Testumgebung geladen. Verkaufsdaten werden nur bei expliziter Zuordnung ergänzt."
            : $"Produktstamm wurde live geladen; Material Stock ist fehlgeschlagen: {stockError}";
        return new SapRetailDataProvider(mapped, new RetailDataSourceStatus(
            "Sandbox", healthy ? "SAP API SANDBOX" : "SAP SANDBOX · TEILWEISE",
            descriptionText, true, healthy, mapped.Count, stocks.Count, stockError, DateTimeOffset.UtcNow));

        SapRetailDataProvider Failed(string error) => new([], new RetailDataSourceStatus(
            "Sandbox", "SAP SANDBOX · FEHLGESCHLAGEN",
            "Der Remote-Modus bleibt aktiv; es wurde nicht auf Fixtures zurückgefallen.",
            true, false, Error: error, RefreshedAtUtc: DateTimeOffset.UtcNow));
    }

    public IReadOnlyList<ProductSnapshot> GetProducts() => _products;
    public ProductSnapshot? GetProduct(string id) => _products.SingleOrDefault(x => x.Id == id);
}

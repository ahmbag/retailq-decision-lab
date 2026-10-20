using System.Globalization;
using RetailQ.DecisionLab.Core;
using RetailQ.DecisionLab.Infrastructure.SAP.Models;

namespace RetailQ.DecisionLab.Infrastructure.SAP.Mapping;

/// <summary>
/// Converts SAP's transport-shaped DTOs into the Core's business-shaped ProductSnapshot.
/// This is the anti-corruption boundary: SAP naming and OData quirks must not leak into
/// the decision engine or the Blazor components.
/// </summary>
public sealed class SapProductMapper
{
    /// <summary>
    /// Creates a domain product from one SAP master record and its stock records.
    /// Only stock type 01 (unrestricted-use) is included. Missing rows remain unknown
    /// (<c>null</c>) rather than silently becoming zero, preventing false reorder advice.
    /// </summary>
    public ProductSnapshot Map(
        SapProductDto product,
        IEnumerable<SapMaterialStockDto> materialStocks,
        ProductSnapshot replenishmentProfile)
        => MapCore(product, materialStocks, replenishmentProfile, null, null, "SAP_ODATA_FIXTURE");

    /// <summary>
    /// Maps a sandbox product even when no POS profile exists. In that case SalesHistory stays
    /// empty and IsReplenishable is false, so the Decision Engine cannot invent demand.
    /// </summary>
    public ProductSnapshot MapRemote(
        SapProductDto product,
        IEnumerable<SapMaterialStockDto> materialStocks,
        ProductSnapshot? explicitlyMappedProfile,
        string? selectedPlant,
        string? selectedStorageLocation)
        => MapCore(product, materialStocks, explicitlyMappedProfile, selectedPlant,
            selectedStorageLocation, "SAP_API_SANDBOX");

    private static ProductSnapshot MapCore(
        SapProductDto product,
        IEnumerable<SapMaterialStockDto> materialStocks,
        ProductSnapshot? replenishmentProfile,
        string? selectedPlant,
        string? selectedStorageLocation,
        string source)
    {
        if (string.IsNullOrWhiteSpace(product.Product))
            throw new InvalidDataException("SAP product is missing Product.");
        if (string.IsNullOrWhiteSpace(product.BaseUnit))
            throw new InvalidDataException($"SAP product {product.Product} is missing BaseUnit.");

        var candidates = materialStocks
            .Where(x => MatchesAssignment(x, product.Product, selectedPlant, selectedStorageLocation)
                && IsUnrestrictedUse(x.InventoryStockType))
            .ToList();
        var blockedCandidates = materialStocks
            .Where(x => MatchesAssignment(x, product.Product, selectedPlant, selectedStorageLocation)
                && IsBlocked(x.InventoryStockType))
            .ToList();

        ValidateUnits(product, candidates.Concat(blockedCandidates));

        // A material can occur once per storage location (and potentially per batch). The
        // records represent disjoint physical quantities, so additive aggregation is valid.
        // Other stock types are excluded before aggregation to avoid double counting.
        int? unrestrictedStock = candidates.Count == 0 ? null : AggregateWholeUnitStock(candidates, product.Product);
        int? blockedStock = blockedCandidates.Count == 0 ? null : AggregateWholeUnitStock(blockedCandidates, product.Product);
        var primaryLocation = candidates.Select(x => x.StorageLocation).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))
            ?? selectedStorageLocation ?? replenishmentProfile?.StorageLocation ?? "—";
        var plant = candidates.Select(x => x.Plant).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))
            ?? selectedPlant ?? replenishmentProfile?.Plant ?? "—";

        var hasExplicitPosMapping = replenishmentProfile is not null;
        var profile = replenishmentProfile ?? new ProductSnapshot(
            product.Product, product.Product, product.ProductDescription ?? $"SAP-Produkt {product.Product}",
            product.ProductGroup ?? product.ProductType ?? "SAP Produktstamm", product.BaseUnit,
            null, [], 0, 0, plant, primaryLocation);

        // Services and every product without an explicit demo mapping stay outside replenishment.
        // A name, position or coincidentally matching identifier never creates POS history.
        var isStockItem = !string.Equals(product.ProductType, "SERV", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(product.BaseUnit, "H", StringComparison.OrdinalIgnoreCase);

        return profile with
        {
            Id = product.Product,
            Sku = product.Product,
            Name = product.ProductDescription ?? $"SAP-Produkt {product.Product}",
            Category = product.ProductGroup ?? product.ProductType ?? profile.Category,
            Unit = product.BaseUnit,
            UnrestrictedStock = unrestrictedStock,
            BlockedStock = blockedStock,
            Source = source,
            Plant = plant,
            StorageLocation = primaryLocation,
            ProductType = product.ProductType,
            IsReplenishable = hasExplicitPosMapping && isStockItem,
            MasterDataSource = source,
            StockDataSource = source,
            SalesDataSource = hasExplicitPosMapping ? "SYNTHETIC_POS_EXPLICIT_MAPPING" : "NOT_MAPPED"
        };
    }

    private static bool MatchesAssignment(SapMaterialStockDto stock, string material,
        string? plant, string? storageLocation) =>
        string.Equals(stock.Material, material, StringComparison.Ordinal)
        && (string.IsNullOrWhiteSpace(plant) || string.Equals(stock.Plant, plant, StringComparison.Ordinal))
        && (string.IsNullOrWhiteSpace(storageLocation)
            || string.Equals(stock.StorageLocation, storageLocation, StringComparison.Ordinal));

    private static void ValidateUnits(SapProductDto product, IEnumerable<SapMaterialStockDto> stocks)
    {
        foreach (var stock in stocks)
            if (!string.IsNullOrWhiteSpace(stock.MaterialBaseUnit)
                && !string.Equals(stock.MaterialBaseUnit, product.BaseUnit, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException(
                    $"SAP stock unit {stock.MaterialBaseUnit} does not match product base unit {product.BaseUnit} for {product.Product}.");
    }

    private static bool IsUnrestrictedUse(string? stockType) =>
        string.Equals(stockType, "01", StringComparison.OrdinalIgnoreCase)
        || string.Equals(stockType, "UnrestrictedUse", StringComparison.OrdinalIgnoreCase);

    private static bool IsBlocked(string? stockType) =>
        string.Equals(stockType, "02", StringComparison.OrdinalIgnoreCase)
        || string.Equals(stockType, "BlockedStock", StringComparison.OrdinalIgnoreCase);

    private static int AggregateWholeUnitStock(IEnumerable<SapMaterialStockDto> stocks, string material)
    {
        decimal total = 0;
        foreach (var stock in stocks)
        {
            if (!decimal.TryParse(stock.QuantityInBaseUnit, NumberStyles.Number, CultureInfo.InvariantCulture, out var quantity)
                || quantity < 0)
                throw new InvalidDataException($"SAP stock for material {material} has an invalid quantity.");
            total += quantity;
        }

        // The MVP works with whole pieces. Refusing a fractional base unit is safer than
        // rounding it, because a hidden unit conversion would change the recommendation.
        if (decimal.Truncate(total) != total || total > int.MaxValue)
            throw new InvalidDataException($"SAP stock for material {material} cannot be represented as whole units.");
        return decimal.ToInt32(total);
    }
}

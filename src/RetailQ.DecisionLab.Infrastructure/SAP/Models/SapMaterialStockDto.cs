using System.Text.Json.Serialization;

namespace RetailQ.DecisionLab.Infrastructure.SAP.Models;

/// <summary>
/// Narrow contract for API_MATERIAL_STOCK (OData V2).
/// V2 wraps entity collections as <c>d.results</c>, which is deliberately modeled
/// separately from the V4 product response rather than hidden behind one generic parser.
/// </summary>
public sealed class SapMaterialStockV2Response
{
    [JsonPropertyName("d")] public SapMaterialStockV2Envelope? D { get; init; }
}

public sealed class SapMaterialStockV2Envelope
{
    [JsonPropertyName("results")] public List<SapMaterialStockDto> Results { get; init; } = [];
    [JsonPropertyName("__next")] public string? NextLink { get; init; }
}

public sealed class SapMaterialStockDto
{
    [JsonPropertyName("Material")] public string? Material { get; init; }
    [JsonPropertyName("Plant")] public string? Plant { get; init; }
    [JsonPropertyName("StorageLocation")] public string? StorageLocation { get; init; }
    [JsonPropertyName("InventoryStockType")] public string? InventoryStockType { get; init; }

    // SAP frequently serializes EDM decimal values as strings. Preserve that exact value
    // until the mapper validates it with decimal.TryParse; do not use double or round it.
    [JsonPropertyName("MatlWrhsStkQtyInMatlBaseUnit")]
    public string? QuantityInBaseUnit { get; init; }
    [JsonPropertyName("MaterialBaseUnit")] public string? MaterialBaseUnit { get; init; }
}

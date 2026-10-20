using System.Text.Json.Serialization;

namespace RetailQ.DecisionLab.Infrastructure.SAP.Models;

/// <summary>
/// Narrow contract for Product (Version 2), an OData V4 service.
/// V4 collection responses use the <c>value</c> envelope. We intentionally keep this
/// DTO technical and small: only fields required by our independent domain are accepted.
/// Field names must be checked against the $metadata document of the target S/4HANA tenant.
/// </summary>
public sealed class SapProductV4Response
{
    [JsonPropertyName("value")]
    public List<SapProductDto> Value { get; init; } = [];

    [JsonPropertyName("@odata.nextLink")]
    public string? NextLink { get; init; }
}

public sealed record SapProductDto
{
    [JsonPropertyName("Product")] public string? Product { get; init; }
    [JsonPropertyName("ProductDescription")] public string? ProductDescription { get; init; }
    [JsonPropertyName("BaseUnit")] public string? BaseUnit { get; init; }
    [JsonPropertyName("ProductType")] public string? ProductType { get; init; }
    [JsonPropertyName("ProductGroup")] public string? ProductGroup { get; init; }
}

public sealed class SapProductDescriptionDto
{
    [JsonPropertyName("Product")] public string? Product { get; init; }
    [JsonPropertyName("Language")] public string? Language { get; init; }
    [JsonPropertyName("ProductDescription")] public string? ProductDescription { get; init; }
}

using System.Text.Json;
using RetailQ.DecisionLab.Infrastructure.SAP.Models;

namespace RetailQ.DecisionLab.Infrastructure.SAP;

/// <summary>Calls and deserializes Product API V2's OData V4 response envelope.</summary>
public sealed class SapProductClient(HttpClient? httpClient = null, SapOptions? options = null)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly HttpClient? _httpClient = httpClient;
    private readonly SapOptions? _options = options;

    public IReadOnlyList<SapProductDto> ParseV4Response(string responseJson) =>
        ParseEnvelope(responseJson).Value;

    public async Task<IReadOnlyList<SapProductDto>> GetProductsAsync(CancellationToken cancellationToken = default)
    {
        EnsureRemoteConfigured();
        var uri = SapHttp.BuildUri(_options!.ProductApi.BaseUrl, _options.ProductApi.Top, _options.ProductApi.Filter);
        var products = new List<SapProductDto>();
        while (true)
        {
            var envelope = ParseEnvelope(await SapHttp.GetAsync(_httpClient!, uri, _options, cancellationToken));
            products.AddRange(envelope.Value);
            if (string.IsNullOrWhiteSpace(envelope.NextLink)) break;
            uri = SapHttp.ResolveNext(uri, envelope.NextLink);
        }
        return products;
    }

    public async Task<IReadOnlyDictionary<string, string>> GetGermanDescriptionsAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureRemoteConfigured();
        if (string.IsNullOrWhiteSpace(_options!.ProductDescriptionApi.BaseUrl))
            return new Dictionary<string, string>(StringComparer.Ordinal);

        var filter = string.IsNullOrWhiteSpace(_options.ProductDescriptionApi.Filter)
            ? "Language eq 'DE'" : _options.ProductDescriptionApi.Filter;
        var uri = SapHttp.BuildUri(_options.ProductDescriptionApi.BaseUrl,
            _options.ProductDescriptionApi.Top, filter);
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        while (true)
        {
            var envelope = ParseDescriptionEnvelope(await SapHttp.GetAsync(_httpClient!, uri, _options, cancellationToken));
            foreach (var item in envelope.Value)
                if (!string.IsNullOrWhiteSpace(item.Product) && !string.IsNullOrWhiteSpace(item.ProductDescription))
                    result.TryAdd(item.Product, item.ProductDescription);
            if (string.IsNullOrWhiteSpace(envelope.NextLink)) break;
            uri = SapHttp.ResolveNext(uri, envelope.NextLink);
        }
        return result;
    }

    private static SapProductV4Response ParseEnvelope(string responseJson)
    {
        try
        {
            return JsonSerializer.Deserialize<SapProductV4Response>(responseJson, JsonOptions)
                ?? throw new JsonException("Leere Antwort.");
        }
        catch (JsonException ex)
        {
            throw new SapRemoteException(SapFailureKind.Contract,
                "Product API lieferte keine gültige OData-V4-value-Antwort.", ex);
        }
    }

    private static SapDescriptionV4Response ParseDescriptionEnvelope(string responseJson)
    {
        try
        {
            return JsonSerializer.Deserialize<SapDescriptionV4Response>(responseJson, JsonOptions)
                ?? throw new JsonException("Leere Antwort.");
        }
        catch (JsonException ex)
        {
            throw new SapRemoteException(SapFailureKind.Contract,
                "ProductDescription API lieferte keine gültige OData-V4-Antwort.", ex);
        }
    }

    private void EnsureRemoteConfigured()
    {
        if (_httpClient is null || _options is null)
            throw new InvalidOperationException("Dieser Client wurde nur für lokales Contract-Parsing erstellt.");
    }

    private sealed class SapDescriptionV4Response
    {
        public List<SapProductDescriptionDto> Value { get; init; } = [];
        [System.Text.Json.Serialization.JsonPropertyName("@odata.nextLink")]
        public string? NextLink { get; init; }
    }
}

using System.Text.Json;
using RetailQ.DecisionLab.Infrastructure.SAP.Models;

namespace RetailQ.DecisionLab.Infrastructure.SAP;

/// <summary>Calls and deserializes API_MATERIAL_STOCK's OData V2 <c>d.results</c> envelope.</summary>
public sealed class SapMaterialStockClient(HttpClient? httpClient = null, SapOptions? options = null)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private readonly HttpClient? _httpClient = httpClient;
    private readonly SapOptions? _options = options;

    public IReadOnlyList<SapMaterialStockDto> ParseV2Response(string responseJson) =>
        ParseEnvelope(responseJson).D!.Results;

    public async Task<IReadOnlyList<SapMaterialStockDto>> GetStocksAsync(CancellationToken cancellationToken = default)
    {
        if (_httpClient is null || _options is null)
            throw new InvalidOperationException("Dieser Client wurde nur für lokales Contract-Parsing erstellt.");

        var selection = _options.StockSelection;
        var clauses = new List<string>();
        if (!string.IsNullOrWhiteSpace(_options.MaterialStockApi.Filter)) clauses.Add(_options.MaterialStockApi.Filter);
        if (!string.IsNullOrWhiteSpace(selection.Plant)) clauses.Add($"Plant eq '{EscapeOData(selection.Plant)}'");
        if (!string.IsNullOrWhiteSpace(selection.StorageLocation)) clauses.Add($"StorageLocation eq '{EscapeOData(selection.StorageLocation)}'");
        var uri = SapHttp.BuildUri(_options.MaterialStockApi.BaseUrl, _options.MaterialStockApi.Top,
            clauses.Count == 0 ? null : string.Join(" and ", clauses), v2: true);
        var stocks = new List<SapMaterialStockDto>();
        while (true)
        {
            var envelope = ParseEnvelope(await SapHttp.GetAsync(_httpClient, uri, _options, cancellationToken));
            stocks.AddRange(envelope.D!.Results);
            if (string.IsNullOrWhiteSpace(envelope.D.NextLink)) break;
            uri = SapHttp.ResolveNext(uri, envelope.D.NextLink);
        }
        return stocks;
    }

    private static SapMaterialStockV2Response ParseEnvelope(string responseJson)
    {
        try
        {
            var envelope = JsonSerializer.Deserialize<SapMaterialStockV2Response>(responseJson, JsonOptions);
            return envelope?.D is not null ? envelope : throw new JsonException("d fehlt.");
        }
        catch (JsonException ex)
        {
            throw new SapRemoteException(SapFailureKind.Contract,
                "Material Stock API lieferte keine gültige OData-V2-d.results-Antwort.", ex);
        }
    }

    private static string EscapeOData(string value) => value.Replace("'", "''", StringComparison.Ordinal);
}

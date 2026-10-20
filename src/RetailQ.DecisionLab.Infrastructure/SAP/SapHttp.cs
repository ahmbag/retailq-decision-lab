using System.Net;

namespace RetailQ.DecisionLab.Infrastructure.SAP;

/// <summary>
/// Shared GET pipeline for both OData versions. Retries are deliberately limited to 429/5xx;
/// authentication and contract errors fail immediately and are never hidden by fixture data.
/// </summary>
internal static class SapHttp
{
    public static async Task<string> GetAsync(HttpClient client, Uri uri, SapOptions options,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.ApiKey))
            throw new SapRemoteException(SapFailureKind.NotConfigured,
                "Der SAP-Sandbox-Key fehlt. Setze Sap__ApiKey oder ein .NET User Secret.");

        for (var attempt = 0; ; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.TryAddWithoutValidation("APIKey", options.ApiKey);
            request.Headers.Accept.ParseAdd("application/json");

            try
            {
                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken);
                if (response.IsSuccessStatusCode)
                    return await response.Content.ReadAsStringAsync(cancellationToken);

                var retryable = response.StatusCode == HttpStatusCode.TooManyRequests
                    || (int)response.StatusCode >= 500;
                if (retryable && attempt < options.MaxRetries)
                {
                    var delay = response.Headers.RetryAfter?.Delta
                        ?? TimeSpan.FromMilliseconds(250 * Math.Pow(2, attempt));
                    await Task.Delay(delay > TimeSpan.FromSeconds(3) ? TimeSpan.FromSeconds(3) : delay,
                        cancellationToken);
                    continue;
                }

                var kind = response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
                    ? SapFailureKind.Authentication
                    : response.StatusCode == HttpStatusCode.TooManyRequests
                        ? SapFailureKind.RateLimited
                        : (int)response.StatusCode >= 500 ? SapFailureKind.Server : SapFailureKind.Contract;
                throw new SapRemoteException(kind,
                    $"SAP-Request fehlgeschlagen (HTTP {(int)response.StatusCode} {response.ReasonPhrase}).");
            }
            catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                throw new SapRemoteException(SapFailureKind.Timeout,
                    "Zeitlimit beim Abruf der SAP-Sandbox überschritten.", ex);
            }
            catch (HttpRequestException ex)
            {
                throw new SapRemoteException(SapFailureKind.Network,
                    "Die SAP-Sandbox ist aktuell nicht erreichbar.", ex);
            }
        }
    }

    public static Uri BuildUri(string? baseUrl, int top, string? filter, bool v2 = false)
    {
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri))
            throw new SapRemoteException(SapFailureKind.NotConfigured, "Für einen SAP-Endpunkt fehlt eine gültige BaseUrl.");

        var separator = string.IsNullOrEmpty(uri.Query) ? "?" : "&";
        var query = $"$top={Math.Clamp(top, 1, 500)}";
        if (!string.IsNullOrWhiteSpace(filter))
            query += $"&$filter={Uri.EscapeDataString(filter)}";
        if (v2)
            query += "&$format=json";
        return new Uri(uri + separator + query);
    }

    public static Uri ResolveNext(Uri current, string nextLink) =>
        Uri.TryCreate(nextLink, UriKind.Absolute, out var absolute) ? absolute : new Uri(current, nextLink);
}

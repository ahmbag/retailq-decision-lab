namespace RetailQ.DecisionLab.Infrastructure.SAP;

/// <summary>
/// Server-side SAP configuration. ApiKey is intentionally absent from appsettings.json and is
/// supplied as Sap__ApiKey (or a .NET user secret) at runtime.
/// </summary>
public sealed class SapOptions
{
    public const string SectionName = "Sap";
    public string Mode { get; set; } = "Fixture";
    public string? ApiKey { get; set; }
    public int TimeoutSeconds { get; set; } = 15;
    public int MaxRetries { get; set; } = 2;
    public SapEndpointOptions ProductApi { get; set; } = new();
    public SapEndpointOptions ProductDescriptionApi { get; set; } = new();
    public SapEndpointOptions MaterialStockApi { get; set; } = new();
    public SapStockSelectionOptions StockSelection { get; set; } = new();

    // Remote SAP IDs may use synthetic POS histories only through this explicit allow-list.
    // An empty mapping is valid and deliberately produces non-evaluable product master rows.
    public Dictionary<string, string> DemoProductMappings { get; set; } = new(StringComparer.Ordinal);
}

public sealed class SapEndpointOptions
{
    public string? BaseUrl { get; set; }
    public int Top { get; set; } = 20;
    public string? Filter { get; set; }
}

public sealed class SapStockSelectionOptions
{
    public string? Plant { get; set; }
    public string? StorageLocation { get; set; }
}

public enum SapFailureKind { NotConfigured, Authentication, RateLimited, Server, Timeout, Contract, Network }

/// <summary>Typed error used by the provider to expose a controlled, non-secret UI message.</summary>
public sealed class SapRemoteException(SapFailureKind kind, string message, Exception? inner = null)
    : Exception(message, inner)
{
    public SapFailureKind Kind { get; } = kind;
}

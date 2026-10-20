namespace RetailQ.DecisionLab.Infrastructure.AI;

/// <summary>
/// Server-only settings. Set OpenAI__ApiKey as an environment variable or .NET user secret;
/// never place a real key in appsettings.json, source control, Blazor markup or browser code.
/// </summary>
public sealed class LlmOptions
{
    public const string SectionName = "OpenAI";
    public string? ApiKey { get; init; }
    public string Model { get; init; } = "gpt-4o-mini";
    public int TimeoutSeconds { get; init; } = 15;
}

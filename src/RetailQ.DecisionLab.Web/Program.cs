using RetailQ.DecisionLab.Web.Components;
using RetailQ.DecisionLab.Core;
using RetailQ.DecisionLab.Core.AI;
using RetailQ.DecisionLab.Infrastructure.SAP;
using RetailQ.DecisionLab.Infrastructure.AI;
using RetailQ.DecisionLab.Infrastructure.SAP.Mapping;
using System.Text.Json.Serialization;
using System.Net;

var builder = WebApplication.CreateBuilder(args);

// `dotnet run -c Release` is part of the local release gate but is not a published deployment.
// Loading the generated static-web-assets manifest keeps CSS, Blazor JS and scoped component
// styles available in that Production-mode verification run as well as in Development.
builder.WebHost.UseStaticWebAssets();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddSingleton<IDecisionEngine, DecisionEngine>();
builder.Services.AddSingleton<DecisionPromptBuilder>();
builder.Services.AddSingleton<DecisionResponseValidator>();
builder.Services.AddSingleton<ScenarioIntentPromptBuilder>();
builder.Services.AddSingleton<ScenarioIntentValidator>();
builder.Services.AddSingleton<ScenarioEvaluationService>();
builder.Services.AddSingleton(builder.Configuration.GetSection(LlmOptions.SectionName).Get<LlmOptions>() ?? new LlmOptions());
builder.Services.AddHttpClient<OpenAiDecisionAssistant>(client =>
{
    // The endpoint and key remain on the server. A browser never receives the API key.
    client.BaseAddress = new Uri("https://api.openai.com/");
});
builder.Services.AddScoped<ILlmDecisionAssistant>(sp => sp.GetRequiredService<OpenAiDecisionAssistant>());
builder.Services.AddHttpClient<OpenAiScenarioIntentInterpreter>(client =>
{
    // Scenario text leaves the server only for intent extraction; no SAP client is exposed to the LLM.
    client.BaseAddress = new Uri("https://api.openai.com/");
});
builder.Services.AddScoped<IScenarioIntentInterpreter>(sp => sp.GetRequiredService<OpenAiScenarioIntentInterpreter>());
builder.Services.AddScoped<IScenarioAnalysisService, ScenarioAnalysisService>();

// Provider selection happens only at the composition root. Components and the decision
// engine still receive IRetailDataProvider, which keeps SAP implementation details isolated.
builder.Services.AddSingleton<DemoRetailDataProvider>();
var sapOptions = builder.Configuration.GetSection(SapOptions.SectionName).Get<SapOptions>() ?? new SapOptions();
builder.Services.AddSingleton(sapOptions);
builder.Services.AddSingleton<SapProductMapper>();
builder.Services.AddHttpClient<SapProductClient>(client =>
    client.Timeout = TimeSpan.FromSeconds(Math.Clamp(sapOptions.TimeoutSeconds, 1, 120)))
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli
    });
builder.Services.AddHttpClient<SapMaterialStockClient>(client =>
    client.Timeout = TimeSpan.FromSeconds(Math.Clamp(sapOptions.TimeoutSeconds, 1, 120)))
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli
    });

// The mode is explicit. Sandbox failures return a visible failed provider and never substitute
// fixture data. Tenant is a documented future mode and intentionally inactive in this MVP.
builder.Services.AddSingleton<IRetailDataProvider>(sp => sapOptions.Mode.ToUpperInvariant() switch
{
    "FIXTURE" => new SapRetailDataProvider(sp.GetRequiredService<DemoRetailDataProvider>()),
    "SANDBOX" => SapRetailDataProvider.CreateSandboxAsync(
        sp.GetRequiredService<DemoRetailDataProvider>(),
        sp.GetRequiredService<SapProductClient>(),
        sp.GetRequiredService<SapMaterialStockClient>(),
        sp.GetRequiredService<SapProductMapper>(), sapOptions,
        sp.GetRequiredService<ILogger<SapRetailDataProvider>>()).GetAwaiter().GetResult(),
    "TENANT" => SapRetailDataProvider.Unavailable("Tenant",
        "Der Tenant-Modus ist vorbereitet, aber für DL-03B bewusst noch nicht aktiviert."),
    "DEMO" => sp.GetRequiredService<DemoRetailDataProvider>(),
    _ => SapRetailDataProvider.Unavailable(sapOptions.Mode,
        $"Unbekannter SAP-Modus '{sapOptions.Mode}'. Erlaubt sind Fixture, Sandbox, Tenant und Demo.")
});

var app = builder.Build();

// Browser hardening for the public showcase. AI/SAP calls remain server-side; the browser only
// needs same-origin Blazor connections plus the explicitly declared font CDN.
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    context.Response.Headers["Content-Security-Policy"] =
        "default-src 'self'; base-uri 'self'; object-src 'none'; frame-ancestors 'none'; " +
        "script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; " +
        "font-src 'self' https://fonts.gstatic.com; img-src 'self' data:; connect-src 'self' ws: wss:";
    await next();
});

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
app.MapGet("/api/products", (IRetailDataProvider provider) => provider.GetProducts());
app.MapGet("/api/data-source", (IRetailDataProvider provider) => provider.Status);
app.MapGet("/api/products/{id}", (string id, IRetailDataProvider provider) =>
    provider.GetProduct(id) is { } product ? Results.Ok(product) : Results.NotFound());
app.MapPost("/api/decisions/evaluate", (ProductSnapshot product, IDecisionEngine engine) => engine.Evaluate(product));

// AI interactions intentionally have no public HTTP endpoint. Interactive Server components call
// scoped server-side services directly, so API keys and model access cannot be exercised through
// an unauthenticated REST route during the public demo.

app.Run();

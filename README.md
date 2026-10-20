# RetailQ Decision Lab

**A transparent replenishment decision prototype for retail inventory teams.**

Retail planners often have to combine stock figures, recent sales and supplier lead times by hand before they can decide whether to reorder. RetailQ Decision Lab turns those inputs into an auditable recommendation, shows the calculation behind it and lets users simulate a small, controlled set of alternatives in natural language.

This repository is an engineering portfolio project. It demonstrates how an ERP integration, deterministic business rules and generative AI can collaborate without making the model the source of business truth. It does **not** claim proven stock-out reduction in a production retailer.

![RetailQ Decision Lab logical architecture](assets/architecture.svg)

## Business problem and value

Watch the [85-second showcase](assets/video/retailq-decision-lab-showcase.mp4), or view
the [dashboard](assets/screenshots/01-decision-dashboard.png),
[Product 360°](assets/screenshots/02-product-360.png),
[AI what-if comparison](assets/screenshots/03-ai-what-if.png) and
[SAP sandbox integration](assets/screenshots/04-sap-remote-integration.png).
Capture provenance and the recording method are documented in [DL-07B.1](docs/dl-07b-showcase.md).

The prototype addresses three recurring problems in retail replenishment:

- stock and sales signals are spread across systems and require manual interpretation;
- a recommendation is difficult to trust if its inputs and formula are hidden;
- free-form AI output is unsafe when it can silently change an operational quantity.

RetailQ consolidates a product snapshot, calculates the replenishment decision with explicit rules, preserves data lineage and uses AI only behind validation boundaries. The intended value is faster identification of inventory risk and easier review of a recommendation. Real-world business impact has not yet been measured.

## Key capabilities

- **Decision dashboard and Product 360°** for 15 coherent fixture products, including stock, 14-day sales history, coverage and reorder status.
- **Deterministic Decision Engine** with explicit handling for stable stock, safety-buffer replenishment, critical coverage, zero stock, zero demand, incomplete history and invalid source data.
- **SAP integration boundary** supporting versioned offline fixtures and read-only SAP Business Accelerator Hub sandbox calls.
- **OData V4 and V2 handling** for Product/Product Description and Material Stock, including paging, filters, mapping, retries and visible partial-failure states.
- **Explainable AI** through the OpenAI Responses API and strict Structured Outputs; the validated explanation cannot overwrite the calculated quantity.
- **Natural-language what-if analysis** for one bounded change to lead time, demand or stock, followed by deterministic recalculation.
- **Data lineage and failure isolation** so fixture, sandbox, stock and synthetic POS origins stay visible and no silent fallback disguises an integration failure.

## System architecture

The solution is a modular .NET monolith with three projects:

| Layer | Responsibility |
|---|---|
| `RetailQ.DecisionLab.Web` | Blazor UI, HTTP composition root, security headers and server-side service orchestration |
| `RetailQ.DecisionLab.Core` | Provider-neutral product snapshots, deterministic decisions, scenario validation and evaluation |
| `RetailQ.DecisionLab.Infrastructure` | SAP OData contracts/transport/mapping and OpenAI Responses API adapters |

External integrations terminate in Infrastructure. SAP DTOs do not enter the domain model, and the browser never receives provider credentials. The complete context, data flows, trust boundaries and trade-offs are documented in [Architecture](docs/architecture.md).

## Engineering decisions

### Deterministic core

Order quantity is calculated from available stock, average demand, lead time and safety days. The LLM never calculates or authorizes an order. A recommendation remains reproducible when AI is unavailable.

### Provider-isolated ERP integration

SAP authentication, OData V2/V4 envelopes, paging and transport failures remain inside Infrastructure. Mapping produces a provider-neutral `ProductSnapshot`. Remote SAP products receive synthetic POS history only through an explicit configured mapping; the application does not infer such a relationship.

### Controlled LLM orchestration

The Responses API is asked for strict JSON. A domain validator checks explanations against the immutable decision snapshot. For what-if analysis, the model may extract exactly one supported intent; server-side bounds are applied before the existing Decision Engine is called again. Invalid output, timeouts and missing credentials affect only the AI feature.

### Modular monolith

The MVP needs clear module boundaries, not distributed deployment complexity. A message broker and vector database would not improve the current synchronous, structured-data use cases. There are no ERP write operations.

## Technology stack

- .NET 10 and C# with nullable reference types
- Blazor Interactive Server
- ASP.NET Core `HttpClientFactory`
- SAP OData V4 Product APIs and OData V2 Material Stock API
- OpenAI Responses API with strict JSON Schema output
- Embedded JSON fixtures and a dependency-free executable contract suite

The application has no third-party NuGet package references. Runtime framework and externally hosted font information is recorded in [Third-party notices](THIRD-PARTY-NOTICES.md).

## Getting started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- PowerShell, Windows Terminal or an equivalent shell
- optional: your own SAP Business Accelerator Hub sandbox key
- optional: your own OpenAI Platform project key and available API credit

### Fixture demo — no credentials required

Fixture mode is the safe default in `appsettings.json`. It loads versioned SAP-shaped Product V4 and Material Stock V2 responses, then combines them with the 15 local replenishment profiles.

```powershell
dotnet restore RetailQ.DecisionLab.slnx
dotnet build RetailQ.DecisionLab.slnx -c Release
dotnet test RetailQ.DecisionLab.slnx -c Release --no-build
dotnet run --project tests/RetailQ.DecisionLab.Tests -c Release --no-build
dotnet run --project src/RetailQ.DecisionLab.Web -c Release --no-build
```

Open the local URL printed by ASP.NET Core. The dashboard works without SAP or OpenAI access. Missing AI configuration is shown as an isolated “not configured” state.

### SAP remote sandbox — optional

Use an environment variable for the current shell:

```powershell
$env:Sap__Mode = 'Sandbox'
$env:Sap__ApiKey = '<YOUR-SAP-SANDBOX-KEY>'
dotnet run --project src/RetailQ.DecisionLab.Web
```

Alternatively, store the values with .NET User Secrets:

```powershell
dotnet user-secrets set 'Sap:Mode' 'Sandbox' --project src/RetailQ.DecisionLab.Web
dotnet user-secrets set 'Sap:ApiKey' '<YOUR-SAP-SANDBOX-KEY>' --project src/RetailQ.DecisionLab.Web
```

The adapter performs read-only calls against SAP-hosted sample APIs. This proves contract and transport integration, not connectivity to a productive S/4HANA tenant. Product and description responses use OData V4; Material Stock uses OData V2. There is no automatic fixture fallback when the sandbox fails.

### OpenAI features — optional

```powershell
$env:OPENAI_API_KEY = '<YOUR-OPENAI-PLATFORM-KEY>'
dotnet run --project src/RetailQ.DecisionLab.Web
```

Or use the configured User Secrets store:

```powershell
dotnet user-secrets set 'OpenAI:ApiKey' '<YOUR-OPENAI-PLATFORM-KEY>' --project src/RetailQ.DecisionLab.Web
```

Never put a real key in `appsettings*.json`, source files, screenshots or committed shell scripts. ChatGPT subscriptions and OpenAI Platform API billing are separate products.

## Configuration reference

| Key | Default | Purpose |
|---|---:|---|
| `Sap:Mode` / `Sap__Mode` | `Fixture` | `Fixture`, `Sandbox`, prepared `Tenant`, or legacy `Demo` provider |
| `Sap:ApiKey` / `Sap__ApiKey` | unset | server-only SAP sandbox credential |
| `Sap:TimeoutSeconds` | `15` | per-client timeout, constrained to 1–120 seconds |
| `Sap:MaxRetries` | `2` | retry count for rate-limit and server failures |
| `Sap:DemoProductMappings` | empty | explicit remote-SAP-to-synthetic-POS mapping |
| `OpenAI:ApiKey` / `OPENAI_API_KEY` | unset | server-only OpenAI Platform credential |
| `OpenAI:Model` | `gpt-4o-mini` | Responses API model |
| `OpenAI:TimeoutSeconds` | `15` | AI request timeout |

The checked-in `appsettings.json` is the public, non-secret configuration example. Environment variables and User Secrets override it locally.

## Testing and verified results

The executable suite is intentionally framework-free and deterministic. `dotnet test` validates that the solution builds as testable projects; the actual domain and contract assertions run with `dotnet run --project tests/RetailQ.DecisionLab.Tests`.

| Area | Verified evidence |
|---|---|
| Decision Engine | 7 rule-path checks |
| SAP contracts and mapping | 20 checks for OData V2/V4, paging, resilience and mapping |
| What-if and guardrails | 25 checks including effective simulation facts, validation, cancellation and provider failure |
| AI snapshot and validation | 7 checks including unavailable-provider isolation |
| Reference decision | baseline 36 units; controlled scenarios 52 / 49 / 26 |
| DL-06 hardening | 12/12 acceptance criteria recorded |

Recorded live integration milestones include an SAP sandbox Product response with three sample products, a successful earlier Material Stock response with 48 rows, a validated OpenAI explanation for the 36-unit baseline and three supported intent interpretations. Remote results are historical evidence, not a guarantee that external sandboxes are currently available. See [SAP integration](docs/sap-integration.md), [LLM foundation](docs/llm-foundation.md), [what-if analysis](docs/what-if-analysis.md) and [DL-06 hardening](docs/dl-06-release-hardening.md).

## Scope and limitations

- The SAP integration targets public SAP-hosted sandbox data, not a productive tenant.
- SAP access is read-only; purchase orders are never created or changed.
- POS history is synthetic. Remote SAP products are not replenishable unless explicitly mapped.
- AI needs a separately billed OpenAI Platform project; without it, core decisions still work.
- What-if analysis supports one bounded parameter per request and is not a general forecasting engine.
- The application is a local portfolio prototype without authentication, multi-tenancy, persistence, background jobs or production observability.
- External sandbox latency and sample content are outside the project's control.
- No production outcome such as reduced stock-outs has been measured.

## Security and release status

Keys are read only on the server from environment variables or .NET User Secrets. Public AI endpoints are deliberately absent, provider payloads are not logged, security headers are enabled and `.gitignore` excludes local credentials and build artifacts.

The completed 12/12 audit is tracked in [Release readiness](docs/release-readiness.md). The repository owner confirmed that credentials previously visible during development are invalid; replacement credentials must remain local and must never appear in release evidence.

## Usage rights

No open-source license is included. Public visibility alone does not grant permission to copy, modify or commercially reuse this code, project name or branding. The repository owner confirmed the publication rights recorded in [Third-party notices](THIRD-PARTY-NOTICES.md).

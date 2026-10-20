# DL-07A Release Readiness

Audit date: 2026-10-19  
Scope: public-repository preparation; no product features, publication, screenshots or video  
Decision: **Approved for DL-07B and public-release preparation. All 12 acceptance criteria are closed.**

## Executive result

The source tree is reproducible in Fixture mode, contains no detected credentials, has no third-party NuGet dependencies and now includes public-facing setup, architecture, security, limitation and rights documentation. The build and deterministic executable suite are the release evidence.

The repository owner confirmed on 2026-10-19 that all previously exposed credentials are invalid and that the source, project assets and RetailQ identity may be prepared for publication. The owner also reported that the RetailQ name is available according to their DPMA check. This record stores the confirmation only—never replacement credentials.

## Acceptance checklist

| # | DL-07A criterion | Status | Evidence |
|---:|---|---|---|
| 1 | Secrets, sensitive configuration and accidental publication content audited | Complete | credential-pattern and exact-known-secret scans; safe checked-in settings; `presentation/` excluded |
| 2 | Git history checked; compromised credentials rotated | Complete | no `.git` history existed at audit start; empty `main` repository initialized (0 commits); owner confirmed all exposed keys invalid on 2026-10-19 |
| 3 | `.gitignore` and public configuration examples checked | Complete | root `.gitignore`; non-secret `appsettings.json`; README environment/User Secrets examples |
| 4 | Dependencies and rights to code, assets and branding clarified | Complete | no third-party NuGet packages; `THIRD-PARTY-NOTICES.md`; owner confirmed publication rights and reported the RetailQ name available in their DPMA check |
| 5 | README covers business value, capabilities and architecture | Complete | English portfolio README |
| 6 | README documents SAP sandbox, OData V2/V4 and limits | Complete | README SAP and Scope sections |
| 7 | README explains LLM orchestration, deterministic calculation and guardrails | Complete | README Engineering decisions section |
| 8 | Fixture setup and test commands documented and verified | Complete | README Getting started; release validation below |
| 9 | Architecture diagram created and reconciled with code | Complete | `assets/architecture.svg`; `docs/architecture.md`; composition-root/provider review |
| 10 | Design decisions and security boundaries documented | Complete | `docs/architecture.md` |
| 11 | Test results, scope and residual risks documented | Complete | README, this report and DL-06 evidence |
| 12 | Readiness record created and DL-07B approved | Complete | all technical and owner-controlled release gates are closed |

## Security audit method and result

### Current tree

The audit covers source, tests, fixtures, configuration, documentation and existing working assets, excluding generated `bin/` and `obj/` files where noted. It checks common credential shapes, private-key markers, connection strings with passwords and the exact credentials known to have been used during development. Results are recorded without printing candidate values.

- No real SAP or OpenAI credential is stored in the checked-in source/configuration set.
- Test values such as `test-key` and `fake-model` are clearly synthetic.
- `appsettings.json` contains only public sandbox URLs, filters and operational defaults.
- User Secrets are enabled for the Web project and remain outside the repository.
- `.gitignore` excludes local settings, environment files, keys, certificates, logs, dumps, IDE state and generated outputs.
- Existing `presentation/` working material is excluded from DL-07A publication and must be deliberately curated in DL-07B.

### Git history

The supplied workspace was not a Git working tree at the start of DL-07A, so there were no local commits, refs, staged blobs or reflogs to scan. An empty repository with branch `main` was initialized as the clean publication baseline; it still contains 0 commits. This is not evidence about any separate or previously published repository. If this folder is later connected to an existing remote, run a history-aware scanner such as `gitleaks git --redact` against the complete cloned history before publication.

Because credentials appeared in chat or screenshots during development, absence from the workspace alone was not sufficient. The account owner confirmed on 2026-10-19 that all exposed values are now invalid. Any replacement credentials remain local and must not be included in release evidence.

## Dependencies and rights

- Projects reference only each other and the .NET/ASP.NET Core shared framework; there are no third-party NuGet `PackageReference` items.
- Google Fonts are loaded remotely; no font binaries are redistributed. Licensing/service considerations are listed in `THIRD-PARTY-NOTICES.md`.
- SAP and OpenAI are optional external services used with developer-owned credentials under their own terms.
- Fixtures are synthetic/sample contract data; no productive customer or tenant data was identified.
- No open-source license has been selected. Default copyright therefore applies, but it does not prove ownership.
- The owner confirmed permission to prepare the code, assets and RetailQ identity for publication on 2026-10-19 and reported the RetailQ name available in their DPMA check.

## Verified engineering evidence

| Gate | Result on 2026-10-19 |
|---|---|
| `dotnet restore RetailQ.DecisionLab.slnx` | successful for all four projects |
| `dotnet build RetailQ.DecisionLab.slnx -c Release --no-restore` | successful: 0 warnings, 0 errors |
| `dotnet test RetailQ.DecisionLab.slnx -c Release --no-build` | successful exit |
| executable regression suite | passed: 7 Decision Engine, 20 SAP, 22 scenario and 7 AI checks |
| Fixture-mode smoke test | Production-mode start without credentials; `/` HTTP 200; 15 products; source `Fixture`; healthy and non-remote |
| HTTP security smoke test | CSP present and `X-Frame-Options: DENY` |

Historical live evidence, already documented in the project, includes three SAP Product rows, 48 Material Stock rows on the successful run, one validated OpenAI explanation retaining the 36-unit order and 3/3 supported intent interpretations. A later SAP stock timeout also verified the controlled partial-failure path. These observations are snapshots of external services, not deterministic release gates.

## Known residual risks and limitations

| Risk or limitation | Release treatment |
|---|---|
| Previously exposed credentials | Closed: owner confirmed all exposed values invalid on 2026-10-19; replacements must remain local |
| Ownership of name/content cannot be inferred from files | Closed by owner confirmation on 2026-10-19; DPMA availability was owner-reported, not independently certified here |
| No Git history supplied | scan full history if an existing remote/history is introduced |
| External SAP sandbox may be slow or unavailable | visible failure/partial state; Fixture mode remains the reproducible demo |
| OpenAI requests require separate API billing | AI becomes unavailable without affecting deterministic decisions |
| Public prototype has no authentication or tenancy | local portfolio scope only; do not deploy as a public production service |
| Remote Google Fonts create a third-party request | documented; self-host or use system fonts for stricter production privacy |
| Executable assertion suite is not test-framework discovery | exact `dotnet run` command is part of the documented release gate |

## Closed owner actions

1. **Closed 2026-10-19:** all previously exposed SAP and OpenAI keys were confirmed invalid; no replacement values were recorded.
2. **Closed 2026-10-19:** the owner confirmed publication rights for the project source, RetailQ identity, architecture graphic and selected assets and reported a clear DPMA availability check for the name.

The codebase is cleared for DL-07B publication work. If an older Git remote is introduced later, its complete history must still be scanned before it is combined with this clean repository baseline.

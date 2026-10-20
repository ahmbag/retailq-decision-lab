# DL-07B.1 — Showcase material

Captured on 2026-10-19. The public deliverables are four original application screenshots,
an 85-second silent MP4 and the SVG architecture diagram.

## Deliverables

| Asset | Evidence |
|---|---|
| [Decision Dashboard](../assets/screenshots/01-decision-dashboard.png) | Real Fixture UI, risk-prioritized queue, 15 products and six open recommendations |
| [Product 360°](../assets/screenshots/02-product-360.png) | Vollmilch: 28 available units, 8/day, 3.5-day coverage, 36-unit recommendation and sales history |
| [AI What-if](../assets/screenshots/03-ai-what-if.png) | Real OpenAI interpretation; +2 delivery days; 36 → 52 units; +16 delta; explicit simulation badge |
| [SAP Remote Integration](../assets/screenshots/04-sap-remote-integration.png) | Actual remote product CH_C_104, available sandbox stock and explicit source labels; no synthetic POS mapping |
| [85-second video](../assets/video/retailq-decision-lab-showcase.mp4) | H.264 MP4, 1920 × 1080, 30 fps delivery, no audio required |
| [Architecture](../assets/architecture.svg) | Editable vector diagram; routes and text contrast reviewed for the closing scene |

The screenshots were requested at a 1920 × 1440 desktop viewport. The browser export
produces 1905 × 1429 pixels for the first three and 1920 × 1440 for SAP. All exceed
the required 1440-pixel width. PNG conversion preserves the captured pixels; it does
not replace numbers, explanations or UI elements. The browser's original capture is
compressed, so PNG conversion does not imply a lossless source capture.

## Video storyboard

| Time | Scene | Data source |
|---|---|---|
| 00–10 | Business problem and priority queue | Synthetic fixture data |
| 10–25 | Product decision and sales context | Synthetic fixture data |
| 25–40 | Real explanation request and response | OpenAI, grounded in the fixture decision |
| 40–60 | Delivery delay and deterministic comparison | OpenAI intent extraction; local Decision Engine |
| 60–75 | Remote overview and stock lineage | Live SAP-hosted API sandbox |
| 75–85 | Architecture and technology stack | Project architecture SVG |

This is an edited browser-frame screencast. Frames were sampled at 2 Hz and encoded
at 30 fps by duplication, not recorded as native 30 fps motion. Cuts separate the
functional steps. The only overlays are chapter headings and data-source captions;
no business results were composited into the app. The final stream was fully decoded
and representative frames were inspected for layout, readable key numbers and absence
of credentials, private filesystem paths and developer tooling.

## Live integration evidence

- SAP provider reported `Sandbox`, `isRemote=true`, `isHealthy=true`, three products and
  48 stock rows. These are current remote HTTP results, not the older release record.
- Product and description use OData V4; Material Stock uses V2. The screenshot shows
  the normalized UI, while the video caption identifies both contracts.
- The sandbox's large stock value is displayed unchanged. It is provider test data,
  not a realistic retail quantity or a productive S/4HANA result.
- The remote product has no POS mapping, so the UI correctly declines to invent demand
  or a replenishment recommendation.
- OpenAI returned genuine structured explanations and interpreted the two-day delay.
  The UI showed 36 baseline units, 52 simulated units and +16, with a seven-day lead time.
- Keys were supplied only to the server processes. No values are included in these assets.

## Release defect found during capture

Visual review exposed an inconsistent AI fact snapshot: scenario calculations retained
the original product, so the explanation could receive five baseline delivery days
alongside a seven-day simulated result. `DecisionSnapshotBuilder.FromScenario` now
copies the effective simulation inputs into the explanation snapshot. The baseline
and the Decision Engine output remain unchanged. The affected footage was replaced.

Three regression assertions cover effective lead time, stock and demand while preserving
the baseline. Release build: zero warnings/errors. Executable suite: 7 Decision Engine,
20 SAP, 25 scenario and 7 AI checks passed (59 total).

## Reproduction and publication

Working browser frames, captions, encoder and the commented rendering script are under
`presentation/` and remain ignored by Git. The selected assets above are under `assets/`.
New recordings require valid local provider credentials and an explicit live API budget.
No GitHub upload or social-media publication was performed as part of this work package.

The new successful SAP call establishes that the credential supplied for this recording
was valid at capture time. It supersedes any earlier blanket statement that every key
ever supplied in the conversation was already invalid. Provider-side credential status
is separate from the secret-free contents of the published assets.

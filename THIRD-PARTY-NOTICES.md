# Third-party notices and publication rights

Last reviewed: 2026-10-19

## Dependency inventory

The solution contains no third-party `PackageReference`. It compiles against the .NET 10 SDK and the `Microsoft.AspNetCore.App` shared framework. Use and redistribution of .NET remain subject to Microsoft's applicable licenses and notices.

The web UI requests **Manrope** and **DM Mono** from Google Fonts at runtime. The font files are not stored in this repository. Both font projects are published under the SIL Open Font License 1.1 ([Manrope source](https://github.com/google/fonts/tree/main/ofl/manrope), [DM Mono source](https://github.com/googlefonts/dm-mono)); Google Fonts' service terms and privacy behavior also apply when the page loads them. A production deployment may self-host reviewed font files or use system fonts if third-party network requests are undesirable.

The SAP Business Accelerator Hub sandbox and OpenAI Responses API are external services. Their SDKs or data are not redistributed here. Users must supply their own credentials and comply with the providers' current terms.

The OData JSON fixtures contain synthetic/sample contract data used only for deterministic tests and local demonstration. No productive customer, employee, sales or tenant data is included.

## Project assets and branding

Source files and hand-authored SVG/CSS assets in the public release are project-local work. No unapproved RetailQ OS assets are intended for inclusion. The `presentation/` working directory is excluded from the DL-07A public repository scope and must be curated separately during DL-07B.

No open-source license is granted by this repository at this stage. On 2026-10-19, the repository owner confirmed authorization to prepare the source code, project assets and **RetailQ** identity for publication and reported that the name was available in their DPMA check. This project record is not a legal opinion or trademark registration. If an open-source license is desired later, it must be selected deliberately and added as a separate `LICENSE` file.

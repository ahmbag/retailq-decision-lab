# DL-06 – Professional UX & Release Hardening

Abnahme: 18. Oktober 2026 · Feature Freeze aktiv · Zielrelease 22. Oktober 2026

## Ergebnis

Alle zwölf Pflichtpunkte der DL-06-Definition-of-Done sind umgesetzt. Es bestehen keine offenen
P0-Befunde. Die Anwendung ist für DL-07 (Dokumentation und Showcase) freigegeben.

## UX-Audit und behobene Befunde

| Priorität | Befund | Korrektur |
|---|---|---|
| P0 | `dotnet run -c Release` lieferte im Production-Modus keine Static Web Assets aus | Static-Web-Assets-Manifest wird am Host explizit aktiviert; CSS, scoped CSS und Blazor-JS liefern HTTP 200 |
| P0 | Nicht bewertbare Remote-Produkte konnten sprachlich wie „keine Aktion erforderlich“ wirken | Eigener Status „Nicht bewertbar“ und Aktion „Keine belastbare Empfehlung möglich“ |
| P1 | Header las die veraltete `RetailData:Provider`-Konfiguration und zeigte falsche Herkunft | Header verwendet ausschließlich `IRetailDataProvider.Status` |
| P1 | Verspätete AI-/Scenario-Antworten konnten nach Artikel-/Quellenwechsel eintreffen | CancellationToken, Request-Version, Baseline-Fingerprint und komponentenspezifische `@key`-Identität |
| P1 | AI-Routen waren als öffentliche REST-Endpunkte erreichbar | Öffentliche AI-Endpunkte entfernt; Interactive Server ruft Services nur serverseitig auf |
| P1 | Dashboard priorisierte den Gesamtbestand statt die Entscheidungslage | Kritische Entscheidungen, offene Empfehlungen und nicht bewertbare Produkte stehen im KPI-Fokus |
| P1 | Produktakte vermischte Kontext, Aktion und Detailtrace | Reihenfolge Product Context → Metrics → Action → AI → Decision Trace |
| P1 | Production-Fehlerseite enthielt generischen Framework-/Development-Text | Kontrollierter, deutschsprachiger Fehlerzustand ohne interne Details |

## Informationshierarchie und Datenkontext

- Dashboard sortiert `Critical`, dann `Reorder`, dann `NotEvaluable/DataError`, anschließend stabile
  und absatzlose Artikel.
- Bewertungszeitpunkt und 14-Tage-Auswertungsfenster sind sichtbar.
- Artikel-360° zeigt Produkt, Werk, Lagerort, Einheit und Bewertungsstatus vor der Entscheidung.
- Product Master, Stock, Sales History und lokale Decision Engine werden getrennt ausgewiesen.
- `SAP API SANDBOX` wird ausdrücklich als SAP-gehostete Testumgebung und nicht als Produktivtenant
  bezeichnet.
- Fehlender Bestand oder fehlende POS-Zuordnung erzeugt weder Nachfrage noch Bestellvorschlag.
- Decision Trace nennt Regelversion 1.0 und bleibt standardmäßig eingeklappt.

## AI- und What-if-Härtung

- Ladezustände verwenden `aria-live`/`aria-busy`; Buttons und Vorschläge sind während eines Requests
  deaktiviert, die Geschäftskennzahlen bleiben sichtbar.
- Provider-Timeouts begrenzen jede Anfrage. Navigation, Datenquellenwechsel oder ein neuer Request
  brechen den alten Request ab und erhöhen zusätzlich eine Request-Version. Nur die aktuelle Version
  darf das sichtbare Ergebnis setzen.
- Der Baseline-Fingerprint enthält Produkt-ID, Datenquelle, Bestand, Verkaufsreihe, Liefer-/Sicherheitszeit,
  Bestellmenge und Rule Version. Ein Quellenwechsel invalidiert daher alte Simulationen.
- Vergleichsansicht benennt aktuelle und simulierte Entscheidung sowie Delta in Text und Zahl;
  Farbe ist nicht die einzige Informationsquelle.
- „Zur Ausgangslage“ löscht nur die Simulation und nennt die unveränderte Baseline-Menge.
- Ohne AI-Key zeigt der Browser kontrolliert „AI nicht eingerichtet“ bzw.
  „AI Assistant ist nicht konfiguriert“. Die 36-Stück-Entscheidung bleibt sichtbar.

## Responsive und Accessibility

Geprüfte Viewports:

| Viewport | Geprüfte Ansicht | Ergebnis |
|---|---|---|
| 1440 × 1200 | Dashboard / Decision Queue | KPI-Hierarchie und Queue ohne horizontales Overflow |
| 768 × 1024 | Vollmilch Artikel-360° | Data Context und Kennzahlen wechseln sauber auf zwei Spalten |
| 390 × 844 | Dashboard | Einspaltige KPIs, reduzierte Queue und lesbare Herkunft |

Zusätzlich geprüft: semantische Überschriften, beschriftetes Textfeld, Tastatur-Fokusmarkierung,
ARIA-Status-/Fehlermeldungen, nicht ausschließlich farbliche Statusinformation und Reduced-Motion-Regel.
QA-Screenshots liegen unter `presentation/dl06-qa/`.

## Security-Sanity-Check

- Workspace-Scan findet weder den SAP-Sandbox-Key noch den früher verwendeten OpenAI-Key.
- Keys werden nur serverseitig aus Umgebungsvariablen/User Secrets gelesen.
- Keine Provider-Payloads oder Keys werden an Browser oder normale Logs weitergegeben.
- `/api/ai/*` ist nicht gemappt (verifiziert: HTTP 404 für GET).
- Security Header aktiv: CSP, `nosniff`, `DENY`, `no-referrer` und restriktive Permissions Policy.
- Öffentliche Fehlerseiten zeigen keine Exception oder Stack Trace.

Ein Schlüssel, der zuvor in Chat oder Screenshot sichtbar war, muss vor einer öffentlichen Demo beim
jeweiligen Provider rotiert werden. Diese organisatorische Rotation kann nicht durch den Quellcode
erzwungen werden.

## Automatisierte Regression

Ausgeführt im Release-Modus:

```powershell
dotnet build RetailQ.DecisionLab.slnx -c Release
dotnet test RetailQ.DecisionLab.slnx -c Release
dotnet run --project tests/RetailQ.DecisionLab.Tests -c Release
```

Ergebnis: Build mit 0 Fehlern und 0 Warnungen; Solution-Testbefehl erfolgreich. Die ausführbare
fachliche Prüfsuite meldet:

- Decision Engine: 7 Prüfungen erfolgreich.
- SAP OData, Paging, Resilience und Mapping: 20 Prüfungen erfolgreich.
- What-if, Guardrails, Cancellation und Resilience: 22 Prüfungen erfolgreich.
- AI Snapshot, Validation und Unavailable Provider: 7 Prüfungen erfolgreich.

## Manuelle E2E-Abnahme

### A – Fixture / Business Value

- Dashboard priorisiert Vollmilch als kritisch.
- Vollmilch: 28 ST Bestand, 8,0 ST/Tag, 3,5 Tage Reichweite, Baseline 36 ST.
- Browserinteraktion bestätigt kontrollierten AI- und Scenario-Fehlerzustand ohne konfigurierten Key.
- Deterministische Regression bestätigt Szenarien 52 / 49 / 26 sowie unveränderte Baseline 36.
- Interaktive Blazor-Events funktionieren mit den aktivierten Security Headern.

### B – SAP Sandbox / technische Integrität

- Product API lieferte drei Remote-Produkte.
- Der Material-Stock-Aufruf überschritt am 18.10. das konfigurierte 15-Sekunden-Limit. Das Dashboard
  zeigte korrekt `SAP SANDBOX · TEILWEISE`, null Bestandszeilen und keine künstliche Empfehlung.
- Der bereits dokumentierte DL-03B-Live-Nachweis vom 17.10. verarbeitete Product und 48 Stock-Zeilen
  erfolgreich. Zusammen belegen beide Läufe sowohl Happy Path als auch kontrollierten Timeout-Pfad.

## Akzeptierte Einschränkungen

- Die Sandbox ist ein externer Testdienst; Antwortzeiten und Beispieldaten liegen nicht unter Kontrolle
  von RetailQ. Der sichtbare Teil-/Fehlerstatus ist deshalb Bestandteil des Designs.
- Ohne serverseitige OpenAI-Konfiguration bleibt AI bewusst deaktiviert; Decision Engine und Trace
  bleiben vollständig funktionsfähig.
- Das MVP besitzt ein solides responsives Layout, aber kein eigenständiges Mobile-Produktkonzept.
- Die Test-Suite ist eine ausführbare, deterministische Contract-Suite; `dotnet test` baut die Solution,
  während die tatsächlichen Assertions über den dokumentierten `dotnet run`-Befehl ausgeführt werden.

## Release-Empfehlung

**Freigabe für DL-07.** Keine offenen P0-Befunde. Neue Business-Funktionen bleiben bis zum Release
eingefroren; zulässig sind nur Dokumentation, Showcase-Material und nachgewiesene Release-Defekte.

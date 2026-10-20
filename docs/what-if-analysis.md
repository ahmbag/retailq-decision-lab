# DL-05B: Natural Language What-if Analysis

## Lernziel und Verantwortungsgrenzen

Das LLM ist ausschließlich ein Interpreter an der Systemgrenze. Es übersetzt eine freie Frage in
einen `ScenarioIntent`; Bestands-, Bedarfs- und Bestellwerte werden weiterhin ausschließlich durch
die vorhandene `DecisionEngine` berechnet.

```text
Benutzerfrage -> OpenAI Structured Output -> ScenarioIntentValidator
                                              |
SAP-Baseline -> Fingerprint -> ScenarioOverlay -> DecisionEngine -> ScenarioComparison
                                                               \-> optionale AI-Erklärung
```

Diese Trennung ist wichtig: Ein syntaktisch korrektes Modellresultat ist noch keine fachliche
Freigabe. Wertebereich, Datentyp, genau ein Parameter und resultierende Geschäftsbedingungen werden
deshalb serverseitig erneut geprüft.

## Unterstützte Intents

| Parameter | Bereich | Vollmilch-Beispiel |
| --- | ---: | ---: |
| `LeadTimeDeltaDays` | -14 bis +14, resultierend 1 bis 30 Tage | +2 -> 52 Stück |
| `DemandChangePercent` | -50 % bis +100 % | +20 % -> 49 Stück |
| `StockDeltaUnits` | -200 bis +200, resultierender Bestand >= 0 | +10 -> 26 Stück |

Im MVP darf genau ein Parameter gesetzt sein. Unklare Fragen liefern
`ClarificationRequired`, fremde Themen oder kombinierte Änderungen `Unsupported`.

## Wichtige Implementierungsentscheidungen

- `ScenarioIntent` enthält keine Bestellmenge. Das Modell kann keine Geschäftsentscheidung liefern.
- Das strikte JSON-Schema deklariert alle Felder als `required`; fachlich optionale Felder sind nullable.
- Benutzertext wird als JSON-Datenobjekt übertragen und im Prompt ausdrücklich als nicht vertrauenswürdig behandelt.
- `ScenarioOverlay` wird nur als Argument an die bestehende `DecisionEngine` übergeben. Das immutable
  `ProductSnapshot` und die SAP-Fixtures werden nie geändert.
- `ScenarioCalculationInputs` macht die effektiven Simulationswerte sichtbar, ohne sie als echte SAP-Daten auszugeben.
- Der Baseline-Fingerprint bindet eine Simulation an Bestand, Absatzhistorie, Lieferzeit, Sicherheitstage,
  Quelle und Regelversion. Veraltete Clients erhalten `BaselineConflict`.
- Die zweite AI-Erklärung ist optional. Scheitert sie, bleibt der deterministische Vergleich erhalten.

## API

```http
POST /api/ai/scenarios/evaluate/{productId}
Content-Type: application/json

{
  "question": "Was passiert, wenn die Lieferung zwei Tage später kommt?",
  "expectedBaselineFingerprint": "..."
}
```

Produkt und Baseline werden immer serverseitig geladen. Der Client kann keine eigenen Bestands- oder
Entscheidungswerte einschleusen. Enum-Werte werden im JSON als lesbare Strings ausgegeben.

## Tests und lokaler Betrieb

```powershell
dotnet build RetailQ.DecisionLab.slnx --configuration Release
dotnet run --project tests/RetailQ.DecisionLab.Tests --configuration Release
```

Ohne Schlüssel laufen sämtliche deterministischen, Contract-, Guardrail- und Fehlerfalltests.
Mit einem temporären serverseitigen Schlüssel führt dasselbe Testprogramm zusätzlich vier echte
OpenAI-Aufrufe aus: die bestehende DL-05A-Erklärung und die drei DL-05B-Intents.

```powershell
$env:OPENAI_API_KEY = "<temporärer Schlüssel>"
dotnet run --project tests/RetailQ.DecisionLab.Tests --configuration Release
Remove-Item Env:OPENAI_API_KEY
```

Die Artikel-360° enthält drei Vorschläge und ein Freitextfeld. Das Ergebnis zeigt Baseline,
Simulation, Delta, effektive Berechnungseingaben, Datenherkunft und die Kennzeichnung
`SIMULATED – NOT APPLIED`. „Zur Ausgangslage zurückkehren“ entfernt nur die Anzeige der Simulation.

## Acceptance-Nachweis

- 52, 49 und 26 Stück sind automatisierte Regressionstests.
- Mehrdeutigkeit, Unsupported, Bereichsverletzung, Mehrfachparameter, unvollständige Daten,
  Prompt-Injection-Grenze, Timeout und Baseline-Konflikt sind kontrolliert getestet.
- DL-05A-Erklärung, SAP-Mapping und die ursprüngliche 36-Stück-Entscheidung bleiben Bestandteil derselben Prüfsuite.

Structured Outputs werden gemäß der offiziellen OpenAI-Dokumentation für schematreue Antworten
eingesetzt: <https://developers.openai.com/api/docs/guides/structured-outputs?api-mode=responses>

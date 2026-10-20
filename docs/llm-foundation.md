# DL-05: LLM Foundation und Explainable AI

## Was die KI darf – und was nicht

Die Decision Engine ist die alleinige Quelle für Bestellmenge, Zielbestand und Status. Das LLM erhält einen fertigen [DecisionSnapshot](../src/RetailQ.DecisionLab.Core/AI/DecisionSnapshot.cs) und erklärt ihn lediglich. Eine Antwort kann keine Entscheidung überschreiben.

```text
Produkt + SAP-Bestand → Decision Engine → unveränderlicher Snapshot
                                              ↓
UI ← validierte Erklärung ← Response Validator ← OpenAI Responses API
```

Die Architektur trennt bewusst drei Verantwortlichkeiten:

1. `DecisionSnapshotBuilder` übergibt nur notwendige Fakten, nicht vollständige SAP-Antworten.
2. `OpenAiDecisionAssistant` fordert mit `store: false` eine strukturierte JSON-Antwort an.
3. `DecisionResponseValidator` prüft Inhalt gegen die Fakten und verwirft unbekannte Treiber oder geänderte Aktionen.

## Strukturierte Antwort

Der Server fordert exakt diese Felder an: `summary`, `driverCodes`, `riskNote`, `recommendedAction`. Das JSON-Schema erlaubt nur die Codes `COVERAGE_BELOW_LEAD_TIME`, `SAFETY_BUFFER`, `NO_CURRENT_DEMAND` und `STABLE_STOCK`.

Structured Outputs sichern das Format, ersetzen aber keine fachliche Prüfung. Deshalb akzeptiert der Validator etwa `COVERAGE_BELOW_LEAD_TIME` nur bei einem tatsächlich kritischen Snapshot und `REORDER` nur dann, wenn die Decision Engine eine positive Bestellmenge berechnet hat.

## Echter OpenAI-Aufruf

Der Adapter verwendet `POST /v1/responses` mit einem strikten JSON Schema und einem konfigurierbaren 15-Sekunden-Timeout. Die offizielle OpenAI-Dokumentation beschreibt Structured Outputs als JSON-Schema-Ausgabe und empfiehlt sie gegenüber JSON Mode, wenn Schema-Treue benötigt wird. [OpenAI Structured Outputs](https://developers.openai.com/api/docs/guides/structured-outputs?api-mode=responses)

Der API-Schlüssel wird ausschließlich serverseitig gesetzt – bevorzugt als Umgebungsvariable:

```powershell
$env:OPENAI_API_KEY = "<dein-api-key>"
dotnet run --project src/RetailQ.DecisionLab.Web
```

Alternativ darf für die lokale Entwicklung ein .NET User Secret unter `OpenAI:ApiKey` verwendet werden. Nie `appsettings.json` ändern oder einen Schlüssel committen. Das Modell kann bei `OpenAI:Model` konfiguriert werden; der Standard ist `gpt-4o-mini`.

Bei fehlendem Schlüssel zeigt die UI kontrolliert „AI nicht eingerichtet“. Bei Timeout, HTTP- oder Validierungsfehler bleibt die Nachbestellempfehlung sichtbar und die AI-Karte zeigt nur ihren eigenen Fehlerzustand.

## Getestete Fälle

Die ausführbare Prüfsuite belegt ohne API-Kosten:

- Vollmilch-Snapshot: 28 frei verwendbar, 12 gesperrt, 8/Tag, 3,5 Tage Reichweite, Zielbestand 64 und Bestellung 36.
- gültige strukturierte Erklärung wird akzeptiert;
- unbekannter Driver-Code wird abgelehnt;
- eine KI-Antwort mit `NO_ACTION` bei der 36-Stück-Entscheidung wird abgelehnt;
- ohne Schlüssel liefert der Provider kontrolliert `Unavailable` und die Decision Engine bleibt nutzbar.

Der echte End-to-End-Aufruf erfordert absichtlich einen vom Nutzer konfigurierten, abrechenbaren API-Key. Er wird nicht durch Fixture-Tests behauptet.

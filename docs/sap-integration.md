# SAP S/4HANA Data Integration – Fixture und Remote Sandbox

## Betriebsmodi

`Sap:Mode` bestimmt die Quelle am Composition Root in `Program.cs`:

| Modus | Verhalten |
|---|---|
| `Fixture` | Versionierte lokale OData-V4-/V2-Antworten; vollständig offline |
| `Sandbox` | Echte GET-Aufrufe an `sandbox.api.sap.com` mit dem Header `APIKey` |
| `Tenant` | Vorbereitet, aber bewusst nicht aktiviert |
| `Demo` | Ursprünglicher rein synthetischer Provider |

Es gibt **keinen automatischen Fallback**. Schlägt Sandbox fehl, bleiben Modus und Fehler im
Dashboard sichtbar und die Produktliste bleibt leer bzw. bei einem reinen Stock-Fehler teilweise
verfügbar. Fixture-Daten würden sonst einen funktionierenden Remote-Zugriff vortäuschen.

## Schlüssel sicher setzen

Der Sandbox-Key steht nie in `appsettings.json`, HTML, JavaScript oder Logs. Für einen lokalen Lauf:

```powershell
$env:Sap__Mode = 'Sandbox'
$env:Sap__ApiKey = '<KEY AUS SAP BUSINESS ACCELERATOR HUB>'
dotnet run --project src/RetailQ.DecisionLab.Web
```

Alternativ kann `Sap:ApiKey` als .NET User Secret gesetzt werden. `appsettings.json` enthält nur
unproblematische Endpunkte, Limits, Filter und die ausgewählte Bestandszuordnung.

## Remote-Datenfluss

```text
Product API V2 (OData V4 value) ───────┐
ProductDescription (OData V4 value) ──┼─> SapProductMapper ─> ProductSnapshot
Material Stock (OData V2 d.results) ──┘          │
                                                 └─> Decision Engine nur bei explizitem POS-Mapping
```

- `SapProductClient` sendet `$top` und optional `$filter`, folgt `@odata.nextLink` und lädt
  Beschreibungen gebündelt statt einmal pro Produkt.
- `SapMaterialStockClient` sendet Werk/Lagerort als serverseitigen Filter, folgt `d.__next` und
  deserialisiert SAP-Dezimalwerte zunächst verlustfrei als Text.
- Beide Clients verwenden `IHttpClientFactory`, ein 15-Sekunden-Timeout und höchstens zwei Retries
  für HTTP 429/5xx. 401/403, Timeout, Netzwerk- und JSON-Vertragsfehler werden unterscheidbar gemeldet.
- Der Key wird pro Request serverseitig als `APIKey`-Header gesetzt.

## Bestandsregeln

1. Nur Bestandsart `01`/`UnrestrictedUse` zählt zum verfügbaren Bestand.
2. `02`/`BlockedStock` wird getrennt ausgewiesen und nie verfügbar gerechnet.
3. Unbekannte Bestandsarten werden nicht als verfügbar behandelt.
4. Fehlen passende Bestandszeilen, bleibt der Bestand `null`, nicht `0`.
5. Aggregiert werden ausschließlich Zeilen der konfigurierten Werk-/Lagerort-Zuordnung.
6. Produkt- und Bestandsmengeneinheit müssen übereinstimmen; Bruchteile werden nicht gerundet.

Die Sandbox-Konfiguration selektiert für den reproduzierbaren Nachweis Werk `1010` und Lagerort
`101B`. Der erfolgreiche Abruf beweist eine Sandbox-Verbindung, nicht den Zugang zu einem
produktiven S/4HANA-System.

## Produktstamm versus POS-Nachfrage

Remote-SAP-IDs werden niemals über Name, Ergebnisposition oder zufällige ID-Gleichheit mit den
15 Demoartikeln verbunden. Nur `Sap:DemoProductMappings` erlaubt eine bewusste Zuordnung:

```json
"DemoProductMappings": {
  "REMOTE_SAP_ID": "100012"
}
```

Ohne Eintrag werden Produktstamm und vorhandener Bestand angezeigt, aber `SalesHistory` bleibt
leer, `SalesDataSource` lautet `NOT_MAPPED`, und die Decision Engine liefert *Nicht bewertbar*.
Serviceprodukte (`ProductType = SERV`) und Mengeneinheit Stunden (`H`) bleiben auch mit Mapping
nicht disponierbar.

## Tests und Integrationsnachweis

```powershell
dotnet build RetailQ.DecisionLab.slnx
dotnet run --project tests/RetailQ.DecisionLab.Tests
```

Die kostenfreien Contract-Tests prüfen beide OData-Hüllen, V4-/V2-Paging, Query-Filter,
Bestandsarten, Werk/Lagerort, fehlenden Bestand, Serviceartikel und das Verbot erfundener POS-Daten.
Der Live-Smoke-Test wird separat mit `Sap__Mode=Sandbox` und `Sap__ApiKey` gestartet; dadurch bleiben
CI und lokale Regressionen ohne Secret reproduzierbar.

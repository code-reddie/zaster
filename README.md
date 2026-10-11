# Zaster

Zaster ist eine Web-App, mit der man die eigenen Finanzen kategorisiert und visualisiert. Buchungen kommen per FinTS von der Bank (bisher die ING), per CSV-Import (ING-Format) oder werden von Hand angelegt und dann über Kategorien und Regeln eingeordnet.

> **Zaster ist noch in Arbeit.** Die Grundlage steht und läuft im Alltag, einige Funktionen fehlen aber noch. Die Liste unten zeigt den aktuellen Stand.

> **Update auf diese Version:** Kategorien und Regeln gehören jetzt zum Konto statt zum Nutzer. Beim ersten Start migriert Zaster die Datenbank: Nutzer, Konten, Buchungen und gespeicherte PINs bleiben, die bisherigen Kategorien, Regeln und Zuordnungen fallen weg. Alle vorhandenen Buchungen stehen danach unter „Ohne Kategorie“ und lassen sich mit „Alle nicht kategorisieren“ in einem Schritt abhaken. Vor dem Update das Volume `/data` sichern.

## Funktionen

Die vollständigen Anforderungen mit Umsetzungsstand und Entscheidungen stehen in [docs/anforderungen.md](docs/anforderungen.md).

### Schon verfügbar

- Oberfläche für PC, iPad und iPhone: am PC und auf dem iPad quer eine Seitenleiste, auf dem Handy Kontowahl oben, Navigation unten und Dialoge als Sheet von unten. Hell oder dunkel nach Systemeinstellung.
- Mehrere Nutzer mit eigener Anmeldung (Registrierung, Login per JWT); jeder sieht nur seine eigenen Konten.
- Bankkonten anlegen (IBAN ist Pflicht und wird geprüft), umbenennen und löschen.
- Buchungen pro Monat oder für alle Zeiträume, nach Tagen gruppiert, mit Suche, Kategoriefilter und Summen für Einnahmen, Ausgaben und Saldo. Ein Tipp auf eine Buchung zeigt alle Details.
- Buchungen von Hand anlegen und löschen.
- CSV-Import von Kontoumsätzen im ING-Format, mit Vorschau vor dem Übernehmen.
- Abruf der Umsätze per FinTS von der ING, pro Konto. Die PIN kann verschlüsselt gespeichert werden; dann ruft Zaster die Konten einmal täglich automatisch ab (Uhrzeit einstellbar). Der Abruf ist inkrementell, die ING liefert höchstens 90 Tage. Übernommen werden nur gebuchte Umsätze; vorgemerkte (z. B. frische Kartenzahlungen) kommen erst mit der Buchung.
- Duplikat-Abgleich zwischen CSV-Import und FinTS-Abruf, damit dieselbe Buchung nicht doppelt auftaucht.
- Kategorien pro Konto als Baum mit beliebig vielen Ebenen, mit Farbe; anlegen, umbenennen, verschieben (auch unter eine andere Kategorie) und löschen.
- Buchungen auf mehrere Kategorien aufteilen, z. B. ein Einkauf mit Lebensmitteln und einer Bohrmaschine: Teilbeträge eingeben, die erste Kategorie bekommt den Rest. Kategoriefilter und Summen rechnen mit den Teilbeträgen.
- Regeln pro Konto zur automatischen Kategorisierung (Text kommt in Auftraggeber, Buchungstext, Verwendungszweck oder einem davon vor). Die Reihenfolge ist einstellbar, die erste passende Regel gewinnt. Neue Buchungen bekommen die passende Kategorie automatisch, und die Regeln lassen sich nachträglich auf alle Buchungen ohne Kategorie anwenden. Aus einer Buchung lässt sich direkt eine Regel machen.
- Seite „Ohne Kategorie“ mit allen Buchungen, die noch nicht geprüft sind: Kategorie mit einem Tipp setzen (zuletzt benutzte Kategorien stehen direkt an der Buchung), direkt aus einer Buchung eine Regel machen, einzelne Buchungen mit „Nicht kategorisieren“ als geprüft abhaken oder „Alle nicht kategorisieren“. Eine Buchung mit Kategorie gilt automatisch als geprüft.
- Benachrichtigungen über Home Assistant einrichten: Jeder Nutzer wählt unter „Einstellungen“ seine Geräte mit der Home-Assistant-App aus und kann eine Testnachricht schicken; ein Tipp darauf öffnet Zaster.

### Geplant

- Geteilte Konten (z. B. Gemeinschaftskonto), alle Mitglieder gleichberechtigt. Das Datenmodell kann es schon, es fehlt der Weg, ein Konto zu teilen.
- Buchungen als CSV exportieren.
- Automatische Push-Benachrichtigung über Home Assistant, sobald neue Buchungen ohne Kategorie da sind; ein Tipp öffnet die Seite „Ohne Kategorie“. Geräteauswahl und Testnachricht gibt es schon.
- Mehrere FinTS-Abrufzeiten pro Tag (z. B. 09:00 und 14:00) statt einer.
- Sankey-Diagramme für Ein- und Ausgaben, pro Konto.

## Starten mit Docker Compose

Das Image wird bei jedem Merge auf `main` als `ghcr.io/code-reddie/zaster:latest` gebaut (amd64 und arm64).

Vorlagen liegen im Ordner [`deploy/`](deploy/): [`docker-compose.yml`](deploy/docker-compose.yml) und [`.env.example`](deploy/.env.example). Beide Dateien in einen eigenen Ordner auf dem Server kopieren, `.env.example` in `.env` umbenennen und ausfüllen (mindestens `JWT_KEY`, die übrigen Werte sind optional). Die `.env` enthält Geheimnisse und gehört nicht ins Git.

```bash
cp deploy/docker-compose.yml deploy/.env.example /pfad/zu/zaster/
cd /pfad/zu/zaster
mv .env.example .env
openssl rand -base64 64 | tr -d '\n'   # Ergebnis als JWT_KEY in .env eintragen
docker compose up -d
```

| Variable in `.env` | Pflicht | Wirkung |
|---|---|---|
| `JWT_KEY` | ja | Signierschlüssel für die Login-Tokens, mindestens 64 Zeichen |
| `FINTS_PRODUCT_ID` | nein | Eigene FinTS-Produktregistrierungsnummer; leer = Nummer von libfintx |
| `HA_URL` | nein | Adresse von Home Assistant, wie Zaster sie erreicht |
| `HA_TOKEN` | nein | Long-Lived Access Token aus Home Assistant |
| `ZASTER_URL` | nein | Adresse von Zaster, wie das Handy sie erreicht (Ziel der Benachrichtigungen) |

Zaster ist danach unter http://localhost:8080 erreichbar; dort legt man sich zuerst ein Konto über „Registrieren“ an. Die Datenbank-Migrationen laufen beim Start automatisch.

Wichtig: Das Volume `/data` sichern. Gehen die Schlüssel unter `/data/keys` verloren, sind gespeicherte FinTS-PINs unbrauchbar und müssen neu eingegeben werden.

## Konfiguration (appsettings.json)

Die Konfiguration kommt aus `backend/Zaster/appsettings.json` und kann über Umgebungsvariablen überschrieben werden. Im Docker-Betrieb setzt man in der Regel nur Umgebungsvariablen. Verschachtelte Schlüssel werden dabei mit doppeltem Unterstrich `__` geschrieben, z. B. wird `JwtSettings:Key` zu `JwtSettings__Key`.

Die vollständige Struktur mit allen Schlüsseln, die Zaster liest (Beispielwerte, keine echten Schlüssel):

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore.Database.Command": "Warning"
    }
  },
  "AllowedHosts": "*",
  "DataDirectory": "/data",
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=/data/zaster.db"
  },
  "JwtSettings": {
    "Key": "<mindestens 64 Zeichen, geheim>",
    "Issuer": "zaster",
    "Audience": "zaster"
  },
  "FinTS": {
    "ProductId": "",
    "Url": "https://fints.ing.de/fints/",
    "Blz": 50010517,
    "Bic": "INGDDEFFXXX",
    "MaxWaitForApprovalSeconds": 180,
    "KeyRingPath": "/data/keys",
    "NightlySyncTime": "04:00",
    "TimeZone": "Europe/Berlin"
  },
  "HomeAssistant": {
    "Url": "http://homeassistant.local:8123",
    "Token": "<Long-Lived Access Token, geheim>",
    "ZasterUrl": "https://zaster.example.de"
  }
}
```

Die mitgelieferte `appsettings.json` enthält nur `Logging` und `AllowedHosts`. `JwtSettings` fehlt dort, weil der Schlüssel geheim ist; er wird per Umgebungsvariable gesetzt (lokal per `dotnet user-secrets`, siehe [Lokal starten](#lokal-starten-macos)). `DataDirectory`, `ConnectionStrings` und `FinTS` haben Standardwerte im Code (`backend/Zaster/DataPaths.cs`, `backend/Zaster/FinTs/FinTsOptions.cs`). Auch `HomeAssistant` fehlt dort; ohne ihn gibt es einfach keine Benachrichtigungen (`backend/Zaster/Notifications/HomeAssistantOptions.cs`).

### JwtSettings (Pflicht)

| Schlüssel | Umgebungsvariable | Pflicht | Standard | Bedeutung |
|---|---|---|---|---|
| `Key` | `JwtSettings__Key` | ja | – | Signierschlüssel für die Login-Tokens (HMAC-SHA512). Mindestens 64 Zeichen, sonst startet die Anmeldung nicht. Ändert man ihn, müssen sich alle neu anmelden. |
| `Issuer` | `JwtSettings__Issuer` | ja | – | Aussteller im Token, z. B. `zaster`. |
| `Audience` | `JwtSettings__Audience` | ja | – | Zielgruppe im Token, z. B. `zaster`. |

Login-Tokens gelten 7 Tage (fest im Code).

### Datenordner und Datenbank

| Schlüssel | Umgebungsvariable | Standard | Bedeutung |
|---|---|---|---|
| `DataDirectory` | `DataDirectory` | `/data`, in Development `data` (also `backend/Zaster/data`) | Ordner für Datenbank und Schlüssel, wenn diese nicht einzeln gesetzt sind. Wird angelegt, falls er fehlt. |
| `ConnectionStrings:DefaultConnection` | `ConnectionStrings__DefaultConnection` | `Data Source=<DataDirectory>/zaster.db` | SQLite-Datenbank. |

Relative Pfade (in `DataDirectory`, im `Data Source` und in `FinTS:KeyRingPath`) gelten ab dem Projektordner `backend/Zaster` bzw. ab dem Ordner, aus dem Zaster gestartet wird. Im Container ändert sich nichts: Zaster läuft dort als Production und nutzt `/data`.

### FinTS (optional)

| Schlüssel | Umgebungsvariable | Standard | Bedeutung |
|---|---|---|---|
| `ProductId` | `FinTS__ProductId` | leer | Eigene Produktregistrierungsnummer der Deutschen Kreditwirtschaft. Leer = Nummer von libfintx, damit funktioniert die ING. |
| `Url` | `FinTS__Url` | `https://fints.ing.de/fints/` | FinTS-Adresse der Bank. |
| `Blz` | `FinTS__Blz` | `50010517` | Bankleitzahl; die IBAN eines Kontos muss dazu passen. |
| `Bic` | `FinTS__Bic` | `INGDDEFFXXX` | BIC der Bank. |
| `MaxWaitForApprovalSeconds` | `FinTS__MaxWaitForApprovalSeconds` | `180` | So lange wartet Zaster auf eine Freigabe in der Banking-App. |
| `KeyRingPath` | `FinTS__KeyRingPath` | `<DataDirectory>/keys` | Ordner für die Schlüssel, mit denen gespeicherte PINs verschlüsselt werden. Muss dauerhaft gespeichert und gesichert werden. |
| `NightlySyncTime` | `FinTS__NightlySyncTime` | `04:00` | Uhrzeit (`HH:mm`) für den täglichen automatischen Abruf aller Konten mit gespeicherter PIN. Leer schaltet ihn ab. |
| `TimeZone` | `FinTS__TimeZone` | `Europe/Berlin` | Zeitzone für `NightlySyncTime`. Unbekannte Zeitzone = UTC. |

### HomeAssistant (optional)

Benachrichtigungen schickt Zaster über die REST-API von Home Assistant an den Dienst `notify.mobile_app_<gerät>`, die Home-Assistant-App auf dem Handy zeigt sie an. Welche Geräte eine Nachricht bekommen, stellt jeder Nutzer in Zaster unter „Einstellungen“ ein; dort gibt es auch die Taste „Testnachricht senden“.

| Schlüssel | Umgebungsvariable | Standard | Bedeutung |
|---|---|---|---|
| `Url` | `HomeAssistant__Url` | leer | Adresse von Home Assistant, wie der Zaster-Container sie erreicht, z. B. `http://homeassistant.local:8123`. Leer = keine Benachrichtigungen. |
| `Token` | `HomeAssistant__Token` | leer | Long-Lived Access Token, anzulegen in Home Assistant unter Profil → Sicherheit. Geheim: Zaster gibt ihn nie an die Oberfläche und schreibt ihn nicht ins Log. |
| `ZasterUrl` | `HomeAssistant__ZasterUrl` | leer | Adresse von Zaster, wie das Handy sie erreicht, z. B. `https://zaster.example.de`. Ein Tipp auf die Nachricht öffnet sie. Ohne sie lehnt Zaster das Senden mit einer Meldung ab. |

### Logging und AllowedHosts

Standardeinstellungen von ASP.NET Core. `Logging__LogLevel__Default` usw. steuern die Ausführlichkeit der Logs, `AllowedHosts` die erlaubten Hostnamen.

## Lokal starten (macOS)

Für die Entwicklung laufen Backend und Frontend getrennt: das Backend auf http://localhost:5080, das Frontend auf http://localhost:4200. Der Angular-Dev-Server leitet `/api` an das Backend weiter (`frontend/proxy.conf.json`). Port 5000 wird bewusst nicht benutzt, weil macOS ihn für den AirPlay-Empfänger belegt.

1. Werkzeuge installieren (z. B. mit [Homebrew](https://brew.sh)):

   ```bash
   brew install --cask dotnet-sdk   # .NET 10 SDK
   brew install node                # Node 26 (mindestens 22.22.3 oder 24.15)
   ```

   Prüfen mit `dotnet --version` (10.x) und `node -v`.

2. Geheimnisse einmalig per `dotnet user-secrets` hinterlegen. Sie liegen dann unter `~/.microsoft/usersecrets/zaster/secrets.json`, also außerhalb des Repos, und werden nur in Development gelesen:

   ```bash
   cd backend/Zaster
   dotnet user-secrets set "JwtSettings:Key" "$(openssl rand -base64 64 | tr -d '\n')"   # mindestens 64 Zeichen
   dotnet user-secrets set "JwtSettings:Issuer" zaster
   dotnet user-secrets set "JwtSettings:Audience" zaster
   # optional, eigene Nummer der Deutschen Kreditwirtschaft:
   dotnet user-secrets set "FinTS:ProductId" "<Produktregistrierungsnummer>"
   dotnet user-secrets list
   ```

   Geheimnisse gehören nicht in `appsettings.Development.json`; die Datei ist zwar per `.gitignore` ausgeschlossen, landet aber leicht doch in einem Commit oder Backup.

3. Backend starten:

   ```bash
   cd backend/Zaster
   dotnet run
   ```

   Beim ersten Start legt Zaster den Ordner `backend/Zaster/data` an, darin die Datenbank `zaster.db` und beim ersten Speichern einer FinTS-PIN den Unterordner `keys`. Der Ordner ist per `.gitignore` ausgeschlossen. Wer ihn löscht, fängt mit einer leeren Datenbank an. Die API-Beschreibung gibt es unter http://localhost:5080/swagger.

4. Frontend in einem zweiten Terminal starten:

   ```bash
   cd frontend
   npm ci
   npm start
   ```

   Dann http://localhost:4200 öffnen und über „Registrieren“ einen Nutzer anlegen.

Andere Pfade oder Ports: `DataDirectory`, `ConnectionStrings:DefaultConnection` und `FinTS:KeyRingPath` lassen sich wie oben beschrieben per user-secrets oder Umgebungsvariable setzen; der Backend-Port steht in `backend/Zaster/Properties/launchSettings.json` und muss zu `frontend/proxy.conf.json` passen.

Tests laufen mit `dotnet test backend/Zaster.slnx`. Migrationen anlegen und die Konventionen im Projekt: siehe [CLAUDE.md](CLAUDE.md).

## Lizenz

Siehe [LICENSE](LICENSE).

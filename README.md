# Zaster

Zaster ist eine Web-App, mit der man die eigenen Finanzen kategorisiert und visualisiert. Buchungen kommen per FinTS von der Bank (bisher die ING), per CSV-Import (ING-Format) oder werden von Hand angelegt und dann über Kategorien und Regeln eingeordnet.

> **Zaster ist noch in Arbeit.** Vieles funktioniert schon und läuft im Alltag, einige Funktionen fehlen aber noch, und das Datenmodell wird sich noch ändern (z. B. ziehen Kategorien und Regeln vom Nutzer auf das Konto um). Die Liste unten zeigt den aktuellen Stand.

## Funktionen

Die vollständigen Anforderungen mit Umsetzungsstand und Entscheidungen stehen in [docs/anforderungen.md](docs/anforderungen.md).

### Schon verfügbar

- Mehrere Nutzer mit eigener Anmeldung (Registrierung, Login per JWT); jeder sieht nur seine eigenen Konten.
- Bankkonten anlegen (IBAN ist Pflicht und wird geprüft) und löschen.
- Buchungen von Hand anlegen und löschen.
- CSV-Import von Kontoumsätzen im ING-Format.
- Abruf der Umsätze per FinTS von der ING, pro Konto. Die PIN kann verschlüsselt gespeichert werden; dann ruft Zaster die Konten einmal täglich automatisch ab (Uhrzeit einstellbar). Der Abruf ist inkrementell, die ING liefert höchstens 90 Tage.
- Duplikat-Abgleich zwischen CSV-Import und FinTS-Abruf, damit dieselbe Buchung nicht doppelt auftaucht.
- Kategorien anlegen und löschen, Buchungen einer Kategorie zuordnen und die Zuordnung später ändern.
- Regeln zur automatischen Kategorisierung (Text kommt in Auftraggeber, Buchungstext, Verwendungszweck oder einem davon vor). Neue Buchungen bekommen die passende Kategorie automatisch, und die Regeln lassen sich nachträglich auf alle Buchungen ohne Kategorie anwenden.

### Teilweise umgesetzt

- Responsives Design: Tailwind ist im Einsatz, auf Tablet und Handy ist die Oberfläche aber noch nicht gezielt geprüft.
- Konten und Kategorien bearbeiten: Anlegen und Löschen geht, Umbenennen und Bearbeiten noch nicht.
- Kategorien und Regeln gehören heute dem Nutzer; geplant ist, dass jedes Konto eigene Kategorien und Regeln hat.

### Geplant

- Geteilte Konten (z. B. Gemeinschaftskonto), alle Mitglieder gleichberechtigt.
- Kategorien als Baum mit beliebig vielen Ebenen.
- Buchungen als CSV exportieren.
- Eigene Seite mit allen Buchungen ohne Kategorie, Merkmal „geprüft“ pro Buchung und Taste „Alle als geprüft markieren“.
- Push-Benachrichtigung über Home Assistant, sobald neue Buchungen ohne Kategorie da sind; ein Tipp öffnet die Seite „ohne Kategorie“.
- Mehrere FinTS-Abrufzeiten pro Tag (z. B. 09:00 und 14:00) statt einer.
- Sankey-Diagramme für Ein- und Ausgaben, pro Konto.

## Starten mit Docker Compose

Das Image wird bei jedem Merge auf `main` als `ghcr.io/code-reddie/zaster:latest` gebaut (amd64 und arm64).

`docker-compose.yml`:

```yaml
services:
  zaster:
    image: ghcr.io/code-reddie/zaster:latest
    container_name: zaster
    restart: unless-stopped
    ports:
      - "8080:8080"
    environment:
      # Signierschlüssel für die Login-Tokens, mindestens 64 Zeichen (HMAC-SHA512).
      JwtSettings__Key: ${JWT_KEY:?JWT_KEY fehlt in .env}
      JwtSettings__Issuer: zaster
      JwtSettings__Audience: zaster
      # Optional: eigene FinTS-Produktregistrierungsnummer. Leer = Nummer von libfintx.
      FinTS__ProductId: ${FINTS_PRODUCT_ID:-}
      TZ: Europe/Berlin
    volumes:
      # SQLite-Datenbank (/data/zaster.db) und Schlüssel für gespeicherte PINs (/data/keys)
      - zaster-data:/data

volumes:
  zaster-data:
```

`.env` daneben (gehört nicht ins Git):

```bash
# Erzeugen mit: openssl rand -base64 64 | tr -d '\n'
JWT_KEY=

# Optional, aus der Mail der Deutschen Kreditwirtschaft
FINTS_PRODUCT_ID=
```

Dann:

```bash
docker compose up -d
```

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
  }
}
```

Die mitgelieferte `appsettings.json` enthält nur `Logging` und `AllowedHosts`. `JwtSettings` fehlt dort, weil der Schlüssel geheim ist; er wird per Umgebungsvariable gesetzt (lokal per `dotnet user-secrets`, siehe [Lokal starten](#lokal-starten-macos)). `DataDirectory`, `ConnectionStrings` und `FinTS` haben Standardwerte im Code (`backend/Zaster/DataPaths.cs`, `backend/Zaster/FinTs/FinTsOptions.cs`).

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

Migrationen anlegen und die Konventionen im Projekt: siehe [CLAUDE.md](CLAUDE.md).

## Lizenz

Siehe [LICENSE](LICENSE).

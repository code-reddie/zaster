# Zaster

Web-App, mit der man die eigenen Finanzen kategorisiert und visualisiert. Buchungen kommen per FinTS (ING), per CSV-Import (ING-Format) oder werden von Hand angelegt.

Die fachlichen Anforderungen, ihr Umsetzungsstand und offene Fragen stehen in [docs/anforderungen.md](docs/anforderungen.md). Vor jeder neuen Funktion dort nachlesen und die Datei mitpflegen.

Sprache im Projekt ist Deutsch: Oberfläche, Kommentare, Commit-Nachrichten, PR-Beschreibungen und Fehlermeldungen an Nutzer. Fachbegriffe im Datenmodell sind ebenfalls deutsch (`Buchung`, `Valuta`, `Auftragsgeber`, `Buchungstext`, `Verwendungszweck`, `Betrag`).

## Stack

- **Backend** `backend/Zaster`: .NET 10, ASP.NET Core Controller, EF Core mit SQLite, JWT-Login (BCrypt für Passwörter), Swagger. FinTS über `libfintx.FinTS` 1.4.0.
- **Frontend** `frontend`: Angular 22 (Standalone-Komponenten, Signals, `@ngrx/signals`-Stores), Tailwind CSS 4, `@angular/cdk` für Dialoge. Prettier ist konfiguriert.
- **Auslieferung**: ein `Dockerfile` baut beides; das Angular-Build landet in `wwwroot` des Backends. Port 8080, Daten unter `/data`.
- **CI** `.github/workflows/docker-image.yml`: baut bei PRs nur das Image (amd64 + arm64); erst nach Merge auf `main` wird `ghcr.io/code-reddie/zaster:latest` gepusht.

## Lokal bauen und starten

Backend (lauscht auf http://localhost:5000, Profil `http`):

```bash
cd backend/Zaster
export JwtSettings__Key="$(openssl rand -base64 64 | tr -d '\n')"   # mindestens 64 Zeichen (HMAC-SHA512)
export JwtSettings__Issuer=zaster JwtSettings__Audience=zaster
export ConnectionStrings__DefaultConnection="Data Source=zaster.db" # Standard ist /data/zaster.db
export FinTS__KeyRingPath=./keys                                     # Standard ist /data/keys
dotnet run
```

Frontend (Dev-Server mit Proxy `/api` → `localhost:5000`, siehe `frontend/proxy.conf.json`):

```bash
cd frontend
npm ci
npm start          # ng serve
npm run build      # Produktions-Build
```

Docker:

```bash
docker build -t zaster .
```

Betrieb per `docker compose` mit `.env` (`JWT_KEY`, optional `FINTS_PRODUCT_ID`); Volume für `/data`.

## Datenbank und Migrationen

- Migrationen liegen in `backend/Zaster/Migrations` und werden beim Start automatisch angewendet (`db.Database.Migrate()`).
- Neue Migration: `cd backend/Zaster && dotnet ef migrations add <Name>`. `dotnet ef` startet die App-Konfiguration und braucht dafür `JwtSettings__Key` (≥ 64 Zeichen) in der Umgebung, sonst bricht es ab.
- Bestehende Daten beim Migrieren immer mitnehmen (z. B. Standardwerte für neue Pflichtspalten), die App läuft produktiv mit echten Buchungen.

## Konventionen

- Controller holen die Nutzer-ID aus dem JWT und filtern jede Abfrage darauf; Konten gehören Nutzern über die n:m-Beziehung `Account.Users`. Fremde Daten liefern 404, nicht 403.
- Entities sind `record`s mit `Entity`-Basis (`Id`); nach außen gehen nur DTOs (`*Dto`, Eingaben `Create*`).
- Ein Feature pro Branch und PR, Branch ab `main`. Christoph merged selbst; GitHub löscht Branches beim Merge automatisch, also nicht manuell löschen.
- Es gibt noch keine Tests. Wer Logik mit Randfällen ändert (Duplikat-Abgleich, Regeln, FinTS-Parsing), sollte Tests dafür mitbringen.
- Keine persönlichen Daten ins Repo: keine eigene FinTS-Produkt-ID, keine Zugangsnummern, PINs, IBANs oder echten Buchungen.

## Fachliche Regeln und Stolperfallen

### Konten
- IBAN ist Pflicht beim Anlegen und wird geprüft (Länge, Aufbau, Prüfziffer Modulo 97), siehe `Models/Iban.cs`.

### Import und Duplikate
- CSV und FinTS laufen beide über `POST /api/transaction/import` (`Import/TransactionImporter.cs`).
- Duplikat-Schlüssel (`Import/TransactionMatcher.cs`): Buchungstag (UTC-Datum) + Betrag + die ersten 16 Buchstaben/Ziffern des Auftraggebers, normalisiert (Großbuchstaben, ohne Akzente, Umlaute einheitlich). Grund: FinTS/MT940 kürzt Namen auf 27 Zeichen, die CSV enthält den vollen Namen; Valuta und Verwendungszweck sind je Quelle unterschiedlich formatiert.
- Gleiche Schlüssel werden gezählt, damit zwei identische Buchungen am selben Tag nicht als Duplikat verloren gehen.
- Nach dem Import werden die Kategorisierungsregeln auf neue Buchungen angewendet.

### Kategorien und Regeln
- Heute gehören Kategorien und Regeln dem Nutzer (`Category.UserId`), nicht dem Konto. Die Anforderung ist „pro Konto“, siehe docs/anforderungen.md.
- `Category.ParentCategoryId` existiert im Modell, die Oberfläche legt aber nur oberste Kategorien an.
- Regeln (`Categorization/RuleEngine.cs`): Teilstring-Vergleich ohne Groß-/Kleinschreibung auf Auftraggeber, Buchungstext, Verwendungszweck oder alle drei. Die Regel mit der kleinsten Id gewinnt. Regeln überschreiben nie eine schon gesetzte Kategorie. `POST /api/categorizationrule/apply` wendet sie auf alte Buchungen ohne Kategorie an.

### FinTS (ING)
- Konfiguration in `FinTs/FinTsOptions.cs`, Abschnitt `FinTS` (Umgebungsvariablen `FinTS__...`): URL, BLZ und BIC der ING sind voreingestellt.
- `FinTS__ProductId` ist optional. Ohne eigene Produktregistrierungsnummer nimmt libfintx seine eigene; damit funktioniert die ING. Eine neue DK-Nummer wirkt bei Banken erst nach einigen Werktagen.
- libfintx 1.4.0 maskiert Sonderzeichen (`?`, `@`, `'`, `+`, `:`) in PIN und Kennung nicht. Das führt bei der ING zu Fehler 9030 „Die Daten konnten nicht entschlüsselt werden“. Zaster maskiert selbst (`EscapeFinTs` in `FinTs/FinTsService.cs`) und lehnt Zeichen außerhalb von ASCII ab, weil libfintx sie zu `?` macht.
- libfintx erkennt Fehler in HIRMG nicht zuverlässig (zusätzlicher Doppelpunkt im Segmentkopf). Zaster liest HIRMG/HIRMS selbst aus. Nur Codes `9xxx` gelten als Ablehnung; `3xxx` sind Warnungen (z. B. 3010: nur die letzten 90 Tage), die Umsätze sind trotzdem da.
- Die ING liefert höchstens 90 Tage. Der Abruf ist inkrementell ab dem letzten Abruf mit 7 Tagen Überlappung; der Duplikat-Abgleich fängt die Überschneidung ab.
- Die PIN wird mit ASP.NET Data Protection verschlüsselt in `Account.FinTsPin` gespeichert, Schlüssel unter `FinTS__KeyRingPath` (Standard `/data/keys`). Sie wird nur nach einem erfolgreichen Abruf gespeichert, bei einer Ablehnung durch die Bank sofort gelöscht und verlässt das Backend nie (DTO zeigt nur `HasFinTsPin`). Gehen die Schlüssel verloren, sind alle gespeicherten PINs unbrauchbar.
- Nächtlicher Abruf (`FinTs/NightlySyncService.cs`) für alle Konten mit gespeicherter PIN um `FinTS__NightlySyncTime` (Standard `04:00`, Zeitzone `FinTS__TimeZone`, Standard `Europe/Berlin`; leer = aus). Fehler landen in `Account.LastSyncError`.
- Nie mit ausgedachten Zugangsdaten gegen die echte ING testen: Fehlversuche können den Zugang einer fremden Person sperren.

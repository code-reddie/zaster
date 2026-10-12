# Zaster

Web-App, mit der man die eigenen Finanzen kategorisiert und visualisiert. Buchungen kommen per FinTS (ING), per CSV-Import (ING-Export oder eigene Umsatzliste) oder werden von Hand angelegt.

Die fachlichen Anforderungen, ihr Umsetzungsstand und offene Fragen stehen in [docs/anforderungen.md](docs/anforderungen.md). Vor jeder neuen Funktion dort nachlesen und die Datei mitpflegen.

Sprache im Projekt ist Deutsch: Oberfläche, Kommentare, Commit-Nachrichten, PR-Beschreibungen und Fehlermeldungen an Nutzer. Fachbegriffe im Datenmodell sind ebenfalls deutsch (`Buchung`, `Valuta`, `Auftraggeber`, `Buchungstext`, `Verwendungszweck`, `Betrag`).

## Stack

- **Backend** `backend/Zaster`: .NET 10, ASP.NET Core Controller, EF Core mit SQLite, JWT-Login (BCrypt für Passwörter), Swagger. FinTS über `libfintx.FinTS` 1.4.0.
- **Frontend** `frontend`: Angular 22 (Standalone-Komponenten, Signals, zoneless, OnPush), Tailwind CSS 4 mit eigenen Design-Tokens, `@angular/cdk` für Dialoge. Kein UI-Framework, keine Store-Bibliothek. Prettier ist konfiguriert.
- **Auslieferung**: ein `Dockerfile` baut beides; das Angular-Build landet in `wwwroot` des Backends. Port 8080, Daten unter `/data` (Container läuft als Production).
- **CI** `.github/workflows/docker-image.yml`: baut bei PRs nur das Image (amd64 + arm64); erst nach Merge auf `main` wird `ghcr.io/code-reddie/zaster:latest` gepusht. `.github/workflows/tests.yml` führt die Backend-Tests aus.
- **Tests** `backend/Zaster.Tests`: xUnit, `dotnet test backend/Zaster.slnx`. Das Testprojekt sieht `internal`-Klassen (`InternalsVisibleTo`).

## Lokal bauen und starten

Schritt-für-Schritt-Anleitung für macOS: README, Abschnitt „Lokal starten (macOS)“.

Backend (lauscht auf http://localhost:5080, Profil `http`; nicht 5000, den belegt macOS mit AirPlay):

```bash
cd backend/Zaster
dotnet user-secrets set "JwtSettings:Key" "$(openssl rand -base64 64 | tr -d '\n')"   # einmalig, mindestens 64 Zeichen (HMAC-SHA512)
dotnet user-secrets set "JwtSettings:Issuer" zaster
dotnet user-secrets set "JwtSettings:Audience" zaster
dotnet run
```

Datenordner (`DataDirectory`): in Development `backend/Zaster/data` (Datenbank `zaster.db`, Schlüssel unter `keys`), sonst `/data`. Fehlt der Ordner, wird er angelegt; er ist per `.gitignore` ausgeschlossen. Pfade sind über `DataDirectory`, `ConnectionStrings__DefaultConnection` und `FinTS__KeyRingPath` überschreibbar, relative Pfade gelten ab dem Content Root (`DataPaths.cs`). Keine Container-Pfade fest in den Code schreiben. Geheimnisse lokal immer per `dotnet user-secrets` (UserSecretsId `zaster`), nie in `appsettings.Development.json`. Der Angular-CLI braucht Node ≥ 22.22.3 (Docker baut mit Node 26).

Frontend (Dev-Server mit Proxy `/api` → `localhost:5080`, siehe `frontend/proxy.conf.json`):

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

Betrieb per `docker compose` mit `.env` (`JWT_KEY`, optional `FINTS_PRODUCT_ID`, `HA_URL`, `HA_TOKEN`, `ZASTER_URL`, `BACKUP_DIR` für die Sicherung); Volume für `/data`. Vorlagen: `deploy/docker-compose.yml` und `deploy/.env.example`.

## Datenbank und Migrationen

- Migrationen liegen in `backend/Zaster/Migrations` und werden beim Start automatisch angewendet (`db.Database.Migrate()`).
- Neue Migration: `cd backend/Zaster && dotnet ef migrations add <Name>`. `dotnet ef` startet die App-Konfiguration und braucht dafür `JwtSettings:Key` (≥ 64 Zeichen, per user-secrets oder Umgebungsvariable), sonst bricht es ab.
- Bestehende Daten beim Migrieren immer mitnehmen (z. B. Standardwerte für neue Pflichtspalten), die App läuft produktiv mit echten Buchungen.

## README aktuell halten

Die [README.md](README.md) beschreibt den Funktionsumfang (verfügbar, teilweise, geplant) und die Struktur der Konfiguration (`appsettings.json` mit allen Abschnitten, Schlüsseln, Standardwerten und Umgebungsvariablen). Christoph möchte, dass sie immer stimmt. Deshalb gilt für jeden PR:

- Ändert sich die Konfiguration (neuer, geänderter oder entfernter Schlüssel in `appsettings.json` oder einer Options-Klasse wie `FinTsOptions`, geänderter Standardwert, neue Pflicht-Umgebungsvariable, Änderung an `deploy/docker-compose.yml`/`deploy/.env.example`), werden die Vorlagen in `deploy/`, der Abschnitt „Konfiguration“ und bei Bedarf „Starten mit Docker Compose“ der README im **selben PR** angepasst.
- Ändert sich der Funktionsumfang (Funktion fertig, neu, teilweise oder entfernt), werden die Funktionslisten der README im selben PR angepasst und der Stand in `docs/anforderungen.md` (✅/🟡/⬜) nachgezogen.
- In der PR-Beschreibung kurz erwähnen, was an README und Anforderungen geändert wurde, oder dass nichts zu ändern war.

Das ist eine Arbeitsanweisung für Claude, keine technische Prüfung: Nichts im Build oder in der CI schlägt fehl, wenn die README veraltet ist. Wer ohne Claude ändert, muss selbst daran denken.

## Aufbau des Backends

Ordner nach Fachbereich: `Accounts/`, `Transactions/`, `Categories/` (Kategorien, Regeln, `RuleEngine`), `Users/`, `Auth/`, `FinTs/`, `Notifications/`, `Backup/`, dazu `Common/` und `Database/` (DbContext, `Entity`). Pro Bereich liegen Entity, DTOs (`*Models.cs`) und Controller zusammen.

API (alle Routen klein geschrieben, Swagger unter `/swagger`):

- `/api/auth/login`, `/api/auth/register` (ohne Anmeldung), `/api/auth/refresh` (neuer Token, das Frontend holt ihn, wenn der alte älter als einen Tag ist)
- `/api/me`, `/api/me/notifications…`, `/api/backup` (Datenbank herunterladen), `/api/backup/schedule` (+ `run`)
- `/api/accounts`, `/api/accounts/{accountId}`, `…/members` (Person per Anmeldename hinzufügen), `…/members/{userId}` (entfernen; die eigene Id heißt verlassen)
- `/api/accounts/{accountId}/transactions` (Filter `from`, `to`, `search`, `categoryId` inkl. Unterkategorien, `withoutCategory`, `open`, `offset`, `limit`), `…/import`, `…/balance-check`, `…/review`, `…/{id}/category`, `…/{id}/splits`, `…/{id}/reviewed`
- `/api/accounts/{accountId}/categories` (+ `order`), `/api/accounts/{accountId}/rules` (+ `order`, `apply`)
- `/api/accounts/{accountId}/fints/sync`, `…/fints/times`, `…/fints/pin`

## Aufbau des Frontends

- `core/`: Anmeldung (`auth/`: Token, Interceptor, Guards), Layout (`layout/`: `Shell` mit Seitenleiste bzw. Navigationsleiste unten, Kontowahl, `PageHeader`) und der Baukasten `ui/` (`zButton`, `z-icon`, `DialogService` mit `z-dialog`/`z-dialog-footer`, `ToastService`, Leerzustand, Spinner). `core/errors.ts` holt die deutsche Meldung (`detail`) aus ProblemDetails.
- `shared/format.ts`: Betrag (`amount`), IBAN, Datumsangaben.
- `features/<bereich>/`: Seiten (`*.page.ts`), Sheets/Dialoge (`*-sheet.ts`), Services und Stores. Stores sind einfache `@Injectable`-Klassen mit Signals: `AccountStore` (Konten, gewähltes Konto), `CategoryStore` (Kategorien und Regeln des gewählten Kontos; `Shell` lädt sie beim Kontowechsel).
- Routen sind deutsch und hängen am Konto: `/konten/:accountId/buchungen`, `…/offen`, `…/kategorien`, `…/konto`, dazu `/konten/neu`, `/einstellungen`, `/anmelden`, `/registrieren`. `/` führt zum zuletzt genutzten Konto.
- Farben nur über die Tokens aus `styles.css` (`bg-surface`, `text-muted`, `text-positive`, `bg-accent` …), nie feste Tailwind-Farben wie `bg-gray-100`: die Tokens schalten hell/dunkel nach Systemeinstellung um. Wiederkehrende Formularteile: `z-input`, `z-label`, `z-hint`, `z-card`, `z-checkbox`, Beträge mit Klasse `amount` (Ziffern gleich breit).
- Breakpoints: unter 640 px öffnen Dialoge als Sheet von unten, ab 1024 px gibt es die Seitenleiste statt Kopf- und Navigationsleiste. Neue Seiten in allen drei Breiten prüfen (iPhone 390 px, iPad hoch 820 px, PC), am besten per Playwright mit Screenshot.
- Formulare mit `FormsModule` und Signals (`[(ngModel)]` auf ein `signal`), keine Reactive Forms.

## Konventionen

- Controller erben von `Common/ApiControllerBase` (`UserId` aus dem JWT, Fehler als ProblemDetails mit deutscher Meldung in `detail`). Alles, was zu einem Konto gehört, hängt unter `api/accounts/{accountId}/…` und trägt `[AccountMember]`: der Filter prüft die Mitgliedschaft und liefert sonst 404 (nie 403), der Controller muss es nicht selbst tun. Abfragen filtern trotzdem immer auf `AccountId`.
- Konten gehören Nutzern über die n:m-Tabelle `AccountUsers` (`Account.Users`); alle Mitglieder sind gleichberechtigt.
- Entities sind `record`s mit `Entity`-Basis (`Id`); nach außen gehen nur DTOs (`*Dto`, Eingaben `Create*`/`Save*`/`Update*`).
- Datum von Buchungen ist `DateOnly` (SQLite-Text `yyyy-MM-dd`, sortier- und vergleichbar). Kein `DateTimeOffset` in Spalten, nach denen gefiltert oder sortiert wird: SQLite kann das nicht vergleichen. Zeitpunkte als `DateTime` in UTC. `decimal` kann SQLite nicht summieren, Summen entstehen im Speicher.
- Uhrzeit immer über `TimeProvider` holen, damit Tests sie festlegen können.
- Ein Feature pro Branch und PR, Branch ab `main`. Christoph merged selbst; GitHub löscht Branches beim Merge automatisch, also nicht manuell löschen.
- Wer Logik mit Randfällen ändert (Duplikat-Abgleich, Regeln, FinTS-Parsing, Abrufzeiten), bringt Tests dafür mit (`backend/Zaster.Tests`). Testdaten sind ausgedacht, nie echte Buchungen oder IBANs.
- Keine persönlichen Daten ins Repo: keine eigene FinTS-Produkt-ID, keine Zugangsnummern, PINs, IBANs oder echten Buchungen.

## Fachliche Regeln und Stolperfallen

### Konten
- IBAN ist Pflicht beim Anlegen und wird geprüft (Länge, Aufbau, Prüfziffer Modulo 97), siehe `Accounts/Iban.cs`. Sie lässt sich danach nicht ändern, nur Name und FinTS-Kennung.
- Ein Konto löschen löscht es für alle Mitglieder, mit Buchungen, Kategorien und Regeln.
- Teilen: Jedes Mitglied fügt einen registrierten Nutzer über den genauen Anmeldenamen hinzu (sofort Mitglied) und kann jedes Mitglied entfernen, auch sich selbst. Das letzte Mitglied kann nicht gehen (409), sonst bliebe ein Konto, das niemand sieht. `AccountDto.Members` enthält Id und Name; das Frontend erkennt sich selbst am Namen (Anmeldenamen sind eindeutig).

### Import und Duplikate
- CSV (`POST /api/accounts/{accountId}/transactions/import`) und FinTS laufen beide über `Transactions/TransactionImporter.cs`; jede Buchung merkt sich ihre Herkunft (`Source`: `Manuell`, `Csv`, `FinTs`, `Unbekannt` für Buchungen von vor dem Refactoring).
- Duplikat-Schlüssel (`Transactions/TransactionMatcher.cs`): Buchungstag + Betrag + die ersten 16 Buchstaben/Ziffern des Auftraggebers, normalisiert (Großbuchstaben, ohne Akzente, Umlaute einheitlich). Grund: FinTS/MT940 kürzt Namen auf 27 Zeichen, die CSV enthält den vollen Namen; Valuta und Verwendungszweck sind je Quelle unterschiedlich formatiert.
- Die CSV liest das Frontend (`features/import/csv-parser.ts`) und schickt die Buchungen als JSON. Spalten werden über die Kopfzeile `Buchung;…` gefunden, damit passen der ING-Export (Kopfbereich, `Wertstellungsdatum`, `Saldo` vor `Betrag`, Windows-1252 oder UTF-8) und Christophs eigene Liste (`Valuta`, `Betrag;Saldo`, Beträge teils mit „€“ und geschütztem Leerzeichen, Verwendungszweck teils über mehrere Zeilen in Anführungszeichen). Deshalb wird die ganze Datei mit Anführungszeichen zerlegt, nicht Zeile für Zeile.
- Gleiche Schlüssel werden gezählt, damit zwei identische Buchungen am selben Tag nicht als Duplikat verloren gehen.
- Nach dem Import werden die Regeln des Kontos auf neue Buchungen angewendet.
- Kontostand (`Transaction.Saldo`, nullable): Stand nach der Buchung laut Bank. Die CSV hat ihn pro Zeile (Spalte „Saldo“). MT940 liefert ihn **nicht** pro Buchung, nur Anfangs- (`:60F:`) und Endsaldo (`:62F:`) je Kontoauszug; `FinTsService.ToTransactions` rechnet ihn in der Reihenfolge des Auszugs hoch und lässt ihn leer, wenn die Summe nicht im Endsaldo aufgeht. Bei einem Duplikat trägt der Import einen fehlenden Kontostand nach. Buchungen von Hand haben keinen.
- Lückenprüfung (`Transactions/BalanceCheck.cs`, `GET …/transactions/balance-check`): pro Tag die Kette aus Stand vorher (`Saldo − Betrag`) und nachher suchen, unabhängig von der Reihenfolge (CSV absteigend, FinTS aufsteigend, die Reihenfolge innerhalb eines Tages ist nicht gespeichert); dann Endstand des Vortags gegen Anfangsstand vergleichen. Bankbuchungen ohne Kontostand werden zwischen zwei bekannten Ständen mitgezählt.

### Kategorien, Regeln und „geprüft“
- Kategorien und Regeln gehören dem Konto (`AccountId`), nicht dem Nutzer. Beim Umzug (Migration `Refactoring`, 11.10.2026) wurden die alten Nutzer-Kategorien, Regeln und Zuordnungen gelöscht.
- Kategorien bilden einen Baum beliebiger Tiefe (`ParentId`, Reihenfolge unter Geschwistern `SortOrder`). Die API liefert eine flache Liste, den Baum baut das Frontend. Verschieben unter sich selbst oder eigene Unterkategorien lehnt das Backend ab; Namen sind pro Ebene eindeutig. Löschen nimmt Unterkategorien und deren Regeln mit, die Buchungen werden wieder offen.
- Regeln (`Categories/RuleEngine.cs`): Teilstring-Vergleich ohne Groß-/Kleinschreibung auf Auftraggeber, Buchungstext, Verwendungszweck oder alle drei. Geprüft wird nach `SortOrder`, die erste passende Regel gewinnt. Regeln überschreiben nie eine schon gesetzte Kategorie oder Aufteilung. `POST …/rules/apply` wendet sie auf alle Buchungen des Kontos ohne Kategorie an.
- Aufteilen: Eine Buchung hat 0..n Teilbeträge (`TransactionSplit`: Kategorie + Betrag mit dem Vorzeichen der Buchung, jede Kategorie höchstens einmal). Leer = ohne Kategorie, sonst ergeben die Teile zusammen genau `Betrag` (`Transaction.ValidateSplit`); halb kategorisierte Buchungen gibt es nicht. `Transaction.Categorize` legt einen Teil über den ganzen Betrag an, `Transaction.Split` ersetzt alle (Teile müssen per `Include` geladen sein). Auswertungen nach Kategorie (Filter, Summen, später Sankey) rechnen mit den Teilbeträgen, nicht mit `Betrag`. Wird eine Kategorie gelöscht, verlieren betroffene Buchungen alle Teile und sind wieder offen.
- „Geprüft“ (`Transaction.Reviewed`): Kategorie setzen (von Hand oder per Regel) macht die Buchung geprüft, Kategorie entfernen macht sie wieder offen (`Transaction.Categorize`). „Offen“ heißt: ohne Kategorie und nicht geprüft; nur diese zählen für die Seite „Ohne Kategorie“, `AccountDto.OpenTransactions` und die Benachrichtigung. Nach der Migration sind alle alten Buchungen offen.

### FinTS (ING)
- Konfiguration in `FinTs/FinTsOptions.cs`, Abschnitt `FinTS` (Umgebungsvariablen `FinTS__...`): URL, BLZ und BIC der ING sind voreingestellt.
- `FinTS__ProductId` ist optional. Ohne eigene Produktregistrierungsnummer nimmt libfintx seine eigene; damit funktioniert die ING. Eine neue DK-Nummer wirkt bei Banken erst nach einigen Werktagen.
- libfintx 1.4.0 maskiert Sonderzeichen (`?`, `@`, `'`, `+`, `:`) in PIN und Kennung nicht. Das führt bei der ING zu Fehler 9030 „Die Daten konnten nicht entschlüsselt werden“. Zaster maskiert selbst (`FinTs/FinTsMessages.cs`) und lehnt Zeichen außerhalb von ASCII ab, weil libfintx sie zu `?` macht.
- libfintx erkennt Fehler in HIRMG nicht zuverlässig (zusätzlicher Doppelpunkt im Segmentkopf). Zaster liest HIRMG/HIRMS selbst aus (`FinTsMessages.ParseResultMessages`). Nur Codes `9xxx` gelten als Ablehnung; `3xxx` sind Warnungen (z. B. 3010: nur die letzten 90 Tage), die Umsätze sind trotzdem da.
- Die ING liefert höchstens 90 Tage. Der Abruf ist inkrementell ab dem letzten Abruf mit 7 Tagen Überlappung; der Duplikat-Abgleich fängt die Überschneidung ab.
- Die PIN wird mit ASP.NET Data Protection verschlüsselt in `Account.FinTsPin` gespeichert, Schlüssel unter `FinTS__KeyRingPath` (Standard `<DataDirectory>/keys`, im Container `/data/keys`). Sie wird nur nach einem erfolgreichen Abruf gespeichert, bei einer Ablehnung durch die Bank sofort gelöscht und verlässt das Backend nie (DTO zeigt nur `HasFinTsPin`). Gehen die Schlüssel verloren, sind alle gespeicherten PINs unbrauchbar.
- Automatischer Abruf (`FinTs/ScheduledSyncService.cs`): Jedes Konto hat eine Liste von Uhrzeiten (`Account.SyncTimes`, „HH:mm“, sortiert, leer = aus), einstellbar unter „Konto & Abruf“ (`PUT …/fints/times`). Der Dienst prüft jede Minute, welche Uhrzeiten seit der letzten Prüfung erreicht wurden (Zeitzone `FinTS__TimeZone`, Standard `Europe/Berlin`), und ruft diese Konten ab, sofern eine PIN gespeichert ist. Fehler landen in `Account.LastSyncError`. Konten von vor der Migration `AccountSyncTimes` haben `04:00`, neue Konten keine Uhrzeit.
- Nie mit ausgedachten Zugangsdaten gegen die echte ING testen: Fehlversuche können den Zugang einer fremden Person sperren.

### Benachrichtigungen (Home Assistant)
- Konfiguration in `Notifications/HomeAssistantOptions.cs`, Abschnitt `HomeAssistant`: `Url` (wie Zaster HA erreicht), `Token` (Long-Lived Access Token), `ZasterUrl` (wie das Handy Zaster erreicht; Ziel des Links in der Nachricht). Ohne `Url`/`Token` gibt es keine Benachrichtigungen, ohne `ZasterUrl` lehnt Zaster das Senden ab.
- Der Token verlässt das Backend nie (DTO zeigt nur `HomeAssistantConfigured`) und wird nicht geloggt. Fehlermeldungen von `HomeAssistantClient` dürfen ihn nicht enthalten.
- Zaster ruft `POST /api/services/notify/<dienst>` auf, mit `data.url` (iOS) und `data.clickAction` (Android) auf dieselbe absolute Adresse. Die Geräteliste kommt aus `GET /api/services` (Dienste `notify.mobile_app_*`).
- Jeder Nutzer speichert seine Geräte in `User.NotificationDevices` (JSON-Liste von Dienstnamen ohne `notify.`, nur `[a-z0-9_]`). `POST /api/me/notifications/test` schickt eine Testnachricht an alle.
- Automatische Nachricht (`Notifications/OpenTransactionsNotifier.cs`): `TransactionImporter` (CSV und FinTS) zählt nach dem Anwenden der Regeln die neuen offenen Buchungen und legt sie in die `OpenTransactionsQueue`. Ein Hintergrunddienst schickt sie an die Geräte aller Mitglieder des Kontos (doppelte Geräte nur einmal), Titel „Zaster · <Konto>“, Link `<ZasterUrl>/konten/<id>/offen`. So hält ein langsames Home Assistant keinen Import auf; Fehler landen nur im Log. Buchungen von Hand lösen nichts aus.
- Ein Link in die App ohne Anmeldung führt zu `/anmelden?weiter=<pfad>`, nach der Anmeldung geht es dorthin zurück (nur Pfade innerhalb von Zaster).
- Lokal testen mit einem Fake-Server, der `/api/services` und `/api/services/notify/<dienst>` beantwortet; gegen ein echtes Home Assistant nur mit eigenem Token.

### Sicherung
- `GET /api/backup` (`Backup/BackupController.cs`) liefert die ganze Datenbank als SQLite-Datei. `DatabaseBackup.WriteAsync` schreibt per `VACUUM INTO` eine konsistente Kopie in eine temporäre Datei, die nach dem Ausliefern gelöscht wird; nie die laufende Datei direkt ausliefern.
- Erlaubt nur dem Nutzer mit der kleinsten Id (wer Zaster eingerichtet hat), sonst 403. Es gibt keine Rollen; die Registrierung ist offen, und die Datei enthält alle Nutzer samt Passwort-Hashes. `UserDto.CanDownloadBackup` sagt dem Frontend, ob es die Taste zeigt.
- Die Data-Protection-Schlüssel (`/data/keys`) sind nicht in der Sicherung.
- Automatische Sicherung (`Backup/NasBackup.cs`, `Backup/ScheduledBackupService.cs`): Zielordner aus `Backup__Directory` (leer = aus, dann läuft der Dienst gar nicht). Uhrzeit, letzte Sicherung und letzter Fehler stehen in der einzigen Zeile der Tabelle `BackupSettings` (Id 1, Standard `03:00`, null = aus), einstellbar über `PUT /api/backup/schedule`, sofort sichern mit `POST /api/backup/schedule/run`; beides nur für den, der Zaster eingerichtet hat. Die Kopie entsteht lokal per `VACUUM INTO` (SQLite direkt auf einer SMB-Freigabe ist unzuverlässig), wird als `zaster.db.tmp` kopiert und dann auf `zaster.db` umbenannt, damit im Ziel nie eine halbe Datei liegt. Schlägt die zeitgesteuerte Sicherung fehl, schickt `ScheduledBackupService` eine Nachricht „Zaster · Sicherung“ an die `NotificationDevices` des ersten Nutzers, Link `<ZasterUrl>/einstellungen` („Jetzt sichern“ meldet nichts, der Fehler steht ja auf der Seite).
- Zaster selbst spricht kein SMB. Die FritzBox-Freigabe bindet der Server per `/etc/fstab` ein, `docker compose` reicht den Ordner als `/backup` weiter (`deploy/docker-compose.yml`, auskommentiert). Kein CIFS-Volume in Docker: Läuft Docker in einem unprivilegierten LXC (Proxmox), darf es nichts einbinden; dort bindet der Proxmox-Host ein und reicht per `pct set … -mp0` durch (README).

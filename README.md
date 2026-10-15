# Zaster

Zaster ist eine Web-App, mit der man die eigenen Finanzen kategorisiert und visualisiert. Buchungen kommen per FinTS von der Bank (bisher die ING), per CSV-Import (ING-Export, Transaktionsexport von Trade Republic oder eigene Umsatzliste) oder werden von Hand angelegt und dann über Kategorien und Regeln eingeordnet.

> **Zaster ist noch in Arbeit.** Die Grundlage steht und läuft im Alltag, einige Funktionen fehlen aber noch. Die Liste unten zeigt den aktuellen Stand.

> **Update auf diese Version:** Kategorien und Regeln gehören jetzt zum Konto statt zum Nutzer. Beim ersten Start migriert Zaster die Datenbank: Nutzer, Konten, Buchungen und gespeicherte PINs bleiben, die bisherigen Kategorien, Regeln und Zuordnungen fallen weg. Alle vorhandenen Buchungen stehen danach unter „Ohne Kategorie“ und lassen sich mit „Alle nicht kategorisieren“ in einem Schritt abhaken. Vor dem Update das Volume `/data` sichern.

## Funktionen

Die vollständigen Anforderungen mit Umsetzungsstand und Entscheidungen stehen in [docs/anforderungen.md](docs/anforderungen.md).

### Schon verfügbar

- Oberfläche für PC, iPad und iPhone: am PC und auf dem iPad quer eine Seitenleiste, auf dem Handy Kontowahl oben, Navigation unten und Dialoge als Sheet von unten. Hell oder dunkel nach Systemeinstellung.
- Mehrere Nutzer mit eigener Anmeldung (Registrierung, Login per JWT); jeder sieht nur seine eigenen und mit ihm geteilten Konten. Wer Zaster mindestens einmal pro Woche öffnet, bleibt angemeldet.
- Bankkonten anlegen (IBAN ist Pflicht, wird geprüft und darf in Zaster nur einmal vorkommen), umbenennen und löschen.
- Reihenfolge der Konten unter Einstellungen → „Konten“ mit Pfeiltasten festlegen. Sie gilt für Seitenleiste und Kontoauswahl auf allen Geräten, jeder Nutzer hat seine eigene (auch bei geteilten Konten). Neue Konten kommen ans Ende; wer nie sortiert hat, sieht die Konten nach Name.
- Konten teilen (z. B. Gemeinschaftskonto): Unter „Konto & Abruf“ → „Mitglieder“ fügt jedes Mitglied eine schon registrierte Person über ihren Anmeldenamen hinzu; sie sieht das Konto sofort. Alle Mitglieder sind gleichberechtigt (Buchungen, Kategorien, Regeln, Abruf, Teilen, Löschen) und bekommen die Benachrichtigungen des Kontos. Jedes Mitglied kann andere entfernen oder das Konto verlassen; das letzte Mitglied kann es nur löschen. Geteilte Konten haben in der Kontoliste ein Personen-Symbol.
- Buchungen pro Monat oder für alle Zeiträume, nach Tagen gruppiert, Kategorien als farbige Badges (bei aufgeteilten Buchungen eins pro Teil), mit Suche (Text oder Betrag, z. B. „12,50“; aufgeteilte Buchungen auch über Teilbeträge), Kategoriefilter und Summen für Einnahmen, Ausgaben und Saldo. Ein Tipp auf eine Buchung zeigt alle Details.
- Buchungen von Hand anlegen und löschen.
- CSV-Import von Kontoumsätzen, mit Vorschau vor dem Übernehmen. Zaster erkennt den Aufbau selbst: den Umsatzexport der ING (mit Kopfbereich, Spalten `Buchung;Wertstellungsdatum;…;Saldo;Währung;Betrag;Währung`) und eine eigene Umsatzliste ohne Kopfbereich (`Buchung;Valuta;Auftraggeber/Empfänger;Buchungstext;Verwendungszweck;Betrag;Saldo`, Beträge auch mit „€“, mehrzeilige Felder in Anführungszeichen). So lassen sich auch alte Buchungen nachtragen, die die ING nicht mehr exportiert.
- Import des „Transaktionsexports“ von Trade Republic (App: Service → Kontoauszüge, Komma-getrennt, Kopfzeile `"datetime","date",…`) in ein eigenes Konto für das Verrechnungskonto. Jede Zeile wird eine Buchung: Betrag ist die Bewegung auf dem Konto (`amount + fee + tax`), Gebühr, Steuer, Art, Anlageklasse, ISIN, Stückzahl, Kurs, Fremdwährung, IBAN der Gegenseite, Händlerkategorie und Uhrzeit stehen zusätzlich in den Details. Kapitalmaßnahmen ohne Geldbewegung (Split, Abspaltung) kommen mit 0 € und gelten als geprüft. Jede Spalte des Exports landet in einem Feld der Buchung, damit nichts verloren geht. Duplikate erkennt Zaster an der Transaktions-ID, den Kontostand rechnet es ab der Kontoeröffnung hoch. Trade Republic bietet kein FinTS, ein automatischer Abruf ist daher nicht möglich.
- Abruf der Umsätze per FinTS von der ING, pro Konto. Die PIN kann verschlüsselt gespeichert werden; dann ruft Zaster das Konto automatisch zu den Uhrzeiten ab, die unter „Konto & Abruf“ eingestellt sind (eine oder mehrere pro Tag, z. B. 09:00 und 14:00). Der Abruf ist inkrementell, die ING liefert höchstens 90 Tage. Übernommen werden nur gebuchte Umsätze; vorgemerkte (z. B. frische Kartenzahlungen) kommen erst mit der Buchung.
- Duplikat-Abgleich zwischen CSV-Import und FinTS-Abruf, damit dieselbe Buchung nicht doppelt auftaucht.
- Kontostand nach jeder Buchung (aus der Spalte „Saldo“ der CSV bzw. beim FinTS-Abruf aus dem Anfangssaldo des Kontoauszugs berechnet). Unter „Konto & Abruf“ zeigt Zaster den letzten Kontostand laut Bank und prüft darüber, ob Buchungen fehlen: Passen die Stände nicht zusammen, nennt es den Zeitraum und den fehlenden Betrag. Ein CSV-Import trägt den Kontostand auch bei schon vorhandenen Buchungen nach.
- Kategorien pro Konto als Baum mit beliebig vielen Ebenen, mit Farbe (24 zur Auswahl); anlegen, umbenennen, verschieben (auch unter eine andere Kategorie) und löschen.
- Eigene Bemerkung zu jeder Buchung, z. B. worum es ging, wenn der Verwendungszweck es nicht verrät. Eintragen in den Details einer Buchung oder direkt auf „Ohne Kategorie“; sie steht in den Listen unter dem Auftraggeber, und die Suche findet sie mit. Regeln werten sie nicht aus, Import und Abruf überschreiben sie nie.
- Buchungen auf mehrere Kategorien aufteilen, z. B. ein Einkauf mit Lebensmitteln und einer Bohrmaschine: Teilbeträge eingeben, die erste Kategorie bekommt den Rest. Kategoriefilter und Summen rechnen mit den Teilbeträgen.
- Regeln pro Konto zur automatischen Kategorisierung. Eine Regel hat eine oder mehrere Bedingungen (Text kommt in Auftraggeber, Buchungstext, Verwendungszweck oder einem davon vor), die alle zutreffen müssen, z. B. Auftraggeber „Hausverwaltung“ und Verwendungszweck „Miete“ zu Miete, „Nebenkosten“ zu Nebenkosten. Jede Regel gilt für Einnahmen, Ausgaben oder beide (Standard), z. B. „Amazon“ bei Ausgaben zu Einkaufen und bei Einnahmen zu Erstattungen. Die Reihenfolge ist einstellbar, die erste passende Regel gewinnt. Kategorien sind alphabetisch sortiert. Neue Buchungen bekommen die passende Kategorie automatisch, und die Regeln lassen sich nachträglich auf alle Buchungen ohne Kategorie anwenden. Aus einer Buchung lässt sich direkt eine Regel machen; sie startet mit dem Auftraggeber als Bedingung, die Richtung ist passend zur Buchung vorgewählt, weitere Bedingungen lassen sich hinzufügen und sind mit dem Text der Buchung vorausgefüllt (z. B. dem ganzen Verwendungszweck, den man kürzen kann). Wechselt man das Feld, bevor man den Text ändert, kommt der Text des neuen Felds. Bei Konten mit Wertpapierbuchungen (Trade Republic) gibt es zusätzlich die Bedingung „Anlageklasse“ (Aktie, Fonds / ETF, Krypto …), ausgewählt aus einer Liste und genau passend, z. B. Anlageklasse „Fonds / ETF“ bei Ausgaben zu ETF-Sparplan; aus einer Buchung ist die Anlageklasse der Buchung vorausgewählt.
- Seite „Ohne Kategorie“ mit allen Buchungen, die noch nicht geprüft sind: Kategorie mit einem Tipp setzen (die vier zuletzt benutzten Kategorien stehen direkt an der Buchung, der Browser merkt sie sich pro Nutzer und Konto), direkt aus einer Buchung eine Regel machen, einzelne Buchungen mit „Nicht kategorisieren“ als geprüft abhaken oder „Alle nicht kategorisieren“. Eine Buchung mit Kategorie gilt automatisch als geprüft. Wie viele Buchungen offen sind, steht als Zahl neben jedem Konto in der Seitenleiste und in der Kontoauswahl am Handy; sie wird beim Öffnen der Kontoauswahl und bei der Rückkehr in die App neu geladen.
- „Rückgängig“: Nach Kategorie zuordnen, Aufteilen, „Nicht kategorisieren“, Bemerkung, „Alle nicht kategorisieren“ oder „Regeln anwenden“ steht in der Meldung unten acht Sekunden lang „Rückgängig“; das setzt die Buchungen auf den Stand davor zurück (Kategorien, geprüft, Bemerkung). Nur die letzte Aktion; Löschen, Import und Abruf lassen sich nicht zurücknehmen.
- Benachrichtigungen über Home Assistant: Bringt ein FinTS-Abruf (von Hand oder automatisch) oder ein CSV-Import Buchungen, die nach den Regeln noch ohne Kategorie sind, bekommen alle Mitglieder des Kontos eine Nachricht, z. B. „3 neue Buchungen ohne Kategorie (insgesamt 12 offen)“. Ein Tipp darauf öffnet die Seite „Ohne Kategorie“ des Kontos, bei Bedarf nach der Anmeldung. Von Hand angelegte Buchungen lösen keine Nachricht aus. Jeder Nutzer wählt unter „Einstellungen“ seine Geräte mit der Home-Assistant-App aus und kann eine Testnachricht schicken.
- Sicherung: Unter „Einstellungen“ lädt „Datenbank herunterladen“ die komplette Datenbank als Datei herunter (`zaster-<datum>.db`). Die Taste sieht nur, wer Zaster eingerichtet hat (der zuerst registrierte Nutzer), weil die Datei die Daten aller Nutzer enthält. Wiederherstellen: siehe unten.
- Automatische Sicherung, z. B. auf das NAS der FritzBox: Zaster legt jeden Tag zur eingestellten Uhrzeit (Standard 03:00) eine Kopie der Datenbank als `zaster.db` in einem Ordner ab und überschreibt sie jedes Mal. Schlägt sie fehl, schickt Zaster über Home Assistant eine Nachricht an die Geräte dessen, der Zaster eingerichtet hat. Uhrzeit, letzte Sicherung, Fehler und „Jetzt sichern“ stehen unter „Einstellungen“ → „Sicherung“ (nur für den, der Zaster eingerichtet hat). Den Ordner bindet man auf dem Server ein und reicht ihn per `docker compose` an Zaster weiter, siehe [Sicherung auf die FritzBox](#sicherung-auf-die-fritzbox).
- Seite „Verteilung“ pro Konto: Sankey-Diagramm, woher das Geld kam (Einnahmen nach Kategorie, links) und wohin es ging (Ausgaben nach Kategorie, rechts), mit dem Konto in der Mitte. Zeitraum wählbar (Monat, Jahr, alles oder von–bis, voreingestellt das laufende Jahr). Am PC und iPad sind alle Unterkategorien von Anfang an aufgeklappt, am iPhone zugeklappt; ein Tipp auf eine Kategorie mit Unterkategorien klappt sie auf oder zu. Darunter dieselben Zahlen als Liste mit Anteil in Prozent. Aufgeteilte Buchungen zählen mit ihren Teilbeträgen, Erstattungen werden mit den Ausgaben derselben Kategorie verrechnet, Buchungen ohne Kategorie stehen als „Ohne Kategorie“ auf beiden Seiten. Die Differenz zeigt „Überschuss“ bzw. „Aus dem Kontostand“.

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
| `BACKUP_DIR` | nein | Ordner im Container für die tägliche Sicherung, z. B. `/backup`; leer = keine |

Zaster ist danach unter http://localhost:8080 erreichbar; dort legt man sich zuerst ein Konto über „Registrieren“ an. Die Datenbank-Migrationen laufen beim Start automatisch.

Wichtig: Das Volume `/data` sichern. Gehen die Schlüssel unter `/data/keys` verloren, sind gespeicherte FinTS-PINs unbrauchbar und müssen neu eingegeben werden.

### Sicherung auf die FritzBox

Zaster kann jeden Tag eine Sicherung auf das NAS der FritzBox legen (USB-Speicher oder interner Speicher). Der Server bindet dazu die Freigabe `FRITZ.NAS` per SMB als Ordner ein, z. B. `/mnt/fritzbox`, `docker compose` reicht diesen Ordner als `/backup` an den Container weiter. Zaster schreibt dort `zaster.db` und überschreibt sie bei jeder Sicherung.

1. In der FritzBox unter „Heimnetz → USB / Speicher → Heimnetzfreigabe“ den Haken bei „Zugriff über ein Netzlaufwerk (SMB)“ setzen (Name `FRITZ.NAS`, SMBv1 bleibt aus) und unter „System → FRITZ!Box-Benutzer“ einen Benutzer mit Zugang zu NAS-Inhalten (Lesen und Schreiben) anlegen.
2. Auf dem NAS einen Ordner anlegen, z. B. `Zaster` (bei USB-Speicher liegt er unter dem Namen des Sticks, z. B. `USB-Stick/Zaster`).
3. Auf dem Server, auf dem Docker läuft, `cifs-utils` installieren (`apt install cifs-utils`) und in `/etc/fstab` eine Zeile anfügen (eine Zeile, Benutzer, Passwort und Ordner anpassen; das Passwort darf kein Komma enthalten):
   ```
   //192.168.178.1/FRITZ.NAS/Zaster /mnt/fritzbox cifs username=zaster,password=geheim,vers=3.0,file_mode=0660,dir_mode=0770,_netdev,nofail 0 0
   ```
   Dann `mkdir /mnt/fritzbox`, `systemctl daemon-reload`, `mount /mnt/fritzbox`. `ls /mnt/fritzbox` zeigt danach ohne Fehler den Ordner auf dem NAS.
4. In `docker-compose.yml` die Zeile `- /mnt/fritzbox:/backup` einkommentieren, in der `.env` `BACKUP_DIR=/backup` setzen, dann `docker compose up -d`.
5. In Zaster unter „Einstellungen“ → „Sicherung“ auf „Jetzt sichern“ tippen. Klappt es, steht dort die Uhrzeit der letzten Sicherung, sonst die Fehlermeldung. Auf dem NAS liegt dann `zaster.db`.

**Docker in einem LXC-Container (Proxmox):** Ein unprivilegierter LXC darf keine SMB-Freigaben einbinden (`mount` meldet „operation not permitted“; prüfen mit `systemd-detect-virt`, Ausgabe `lxc`). Dann Schritt 3 in der Shell des Proxmox-Hosts ausführen, mit `uid=100000,gid=100000` zusätzlich in den Optionen (das ist root im LXC), und den Ordner an den LXC durchreichen: `pct set <ID> -mp0 /mnt/fritzbox,mp=/mnt/fritzbox`, danach den LXC neu starten. Schritt 4 wie oben im LXC.

Ist die FritzBox zur Uhrzeit der Sicherung nicht erreichbar, fällt nur diese Sicherung aus: Die vorige `zaster.db` auf dem NAS bleibt erhalten, der Fehler steht in den Einstellungen, und mit eingerichtetem Home Assistant ([Benachrichtigungen](#homeassistant-optional)) bekommt, wer Zaster eingerichtet hat, eine Nachricht auf seine Geräte. Ein Tipp darauf öffnet die Einstellungen. Der nächste Versuch kommt am nächsten Tag. Ist die Freigabe nach einem Neustart des Servers nicht eingebunden (FritzBox war nicht erreichbar, `nofail` lässt den Server trotzdem starten), schreibt Zaster in den leeren Ordner `/mnt/fritzbox` auf dem Server statt auf das NAS; dann `mount /mnt/fritzbox` ausführen.

Statt der FritzBox geht jeder Ordner, den man nach `/backup` einbindet, z. B. ein anderes NAS oder eine zweite Festplatte.

Auch diese Sicherung enthält die Schlüssel unter `/data/keys` nicht.

### Sicherung wiederherstellen

Die Datei aus „Datenbank herunterladen“ (oder `zaster.db` vom NAS) ist eine vollständige SQLite-Datenbank. Zum Wiederherstellen:

```bash
docker compose down
# Datei als zaster.db ins Volume legen und alte WAL-Dateien entfernen
docker run --rm -v zaster_zaster-data:/data -v "$PWD":/backup alpine \
  sh -c 'cp /backup/zaster-2026-10-11.db /data/zaster.db && rm -f /data/zaster.db-wal /data/zaster.db-shm'
docker compose up -d
```

Der Volume-Name hängt vom Ordner ab, in dem `docker-compose.yml` liegt (`docker volume ls`). Die Schlüssel unter `/data/keys` stecken nicht in der Datei: Bleiben sie im Volume erhalten, funktionieren gespeicherte FinTS-PINs weiter, sonst müssen sie neu eingegeben werden.

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
    "TimeZone": "Europe/Berlin"
  },
  "HomeAssistant": {
    "Url": "http://homeassistant.local:8123",
    "Token": "<Long-Lived Access Token, geheim>",
    "ZasterUrl": "https://zaster.example.de"
  },
  "Backup": {
    "Directory": "/backup"
  }
}
```

Die mitgelieferte `appsettings.json` enthält nur `Logging` und `AllowedHosts`. `JwtSettings` fehlt dort, weil der Schlüssel geheim ist; er wird per Umgebungsvariable gesetzt (lokal per `dotnet user-secrets`, siehe [Lokal starten](#lokal-starten-macos)). `DataDirectory`, `ConnectionStrings` und `FinTS` haben Standardwerte im Code (`backend/Zaster/DataPaths.cs`, `backend/Zaster/FinTs/FinTsOptions.cs`). Auch `HomeAssistant` fehlt dort; ohne ihn gibt es einfach keine Benachrichtigungen (`backend/Zaster/Notifications/HomeAssistantOptions.cs`). Ebenso `Backup`; ohne ihn gibt es keine automatische Sicherung (`backend/Zaster/Backup/BackupOptions.cs`).

### JwtSettings (Pflicht)

| Schlüssel | Umgebungsvariable | Pflicht | Standard | Bedeutung |
|---|---|---|---|---|
| `Key` | `JwtSettings__Key` | ja | – | Signierschlüssel für die Login-Tokens (HMAC-SHA512). Mindestens 64 Zeichen, sonst startet die Anmeldung nicht. Ändert man ihn, müssen sich alle neu anmelden. |
| `Issuer` | `JwtSettings__Issuer` | ja | – | Aussteller im Token, z. B. `zaster`. |
| `Audience` | `JwtSettings__Audience` | ja | – | Zielgruppe im Token, z. B. `zaster`. |

Login-Tokens gelten 7 Tage (fest im Code). Ist der Token älter als einen Tag, holt sich die Oberfläche beim nächsten Aufruf still einen neuen (`POST /api/auth/refresh`); neu anmelden muss man sich also erst nach 7 Tagen ohne Nutzung.

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
| `TimeZone` | `FinTS__TimeZone` | `Europe/Berlin` | Zeitzone für die Abrufzeiten der Konten. Unbekannte Zeitzone = UTC. |

Die Uhrzeiten für den automatischen Abruf stehen nicht in der Konfiguration, sondern werden pro Konto in der App unter „Konto & Abruf“ eingestellt. `FinTS__NightlySyncTime` gibt es nicht mehr.

### HomeAssistant (optional)

Benachrichtigungen schickt Zaster über die REST-API von Home Assistant an den Dienst `notify.mobile_app_<gerät>`, die Home-Assistant-App auf dem Handy zeigt sie an. Welche Geräte eine Nachricht bekommen, stellt jeder Nutzer in Zaster unter „Einstellungen“ ein; dort gibt es auch die Taste „Testnachricht senden“. Neue Buchungen ohne Kategorie melden sich automatisch bei allen Mitgliedern des Kontos. Ist Home Assistant nicht erreichbar, läuft der Abruf trotzdem durch, der Fehler steht im Log.

| Schlüssel | Umgebungsvariable | Standard | Bedeutung |
|---|---|---|---|
| `Url` | `HomeAssistant__Url` | leer | Adresse von Home Assistant, wie der Zaster-Container sie erreicht, z. B. `http://homeassistant.local:8123`. Leer = keine Benachrichtigungen. |
| `Token` | `HomeAssistant__Token` | leer | Long-Lived Access Token, anzulegen in Home Assistant unter Profil → Sicherheit. Geheim: Zaster gibt ihn nie an die Oberfläche und schreibt ihn nicht ins Log. |
| `ZasterUrl` | `HomeAssistant__ZasterUrl` | leer | Adresse von Zaster, wie das Handy sie erreicht, z. B. `https://zaster.example.de`. Ein Tipp auf die Nachricht öffnet sie bzw. die Seite „Ohne Kategorie“ darunter (`<ZasterUrl>/konten/<id>/offen`). Ohne sie lehnt Zaster das Senden mit einer Meldung ab. |

### Backup (optional)

| Schlüssel | Umgebungsvariable | Standard | Bedeutung |
|---|---|---|---|
| `Directory` | `Backup__Directory` | leer | Ordner, in den Zaster jeden Tag die Datenbank als `zaster.db` sichert (überschreibt sie jedes Mal). Leer = keine automatische Sicherung. Im Container z. B. `/backup`, siehe [Sicherung auf die FritzBox](#sicherung-auf-die-fritzbox). |

Die Uhrzeit (Standard 03:00, Zeitzone `FinTS:TimeZone`) steht nicht in der Konfiguration, sondern wird in der App unter „Einstellungen“ → „Sicherung“ eingestellt; leer = aus.

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

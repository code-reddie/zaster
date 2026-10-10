# Anforderungen

Christophs Anforderungen an Zaster (Stand 10.10.2026, Entscheidungen vom selben Tag eingearbeitet), geordnet nach Bereichen. Hinter jedem Punkt steht der Umsetzungsstand:

- ✅ umgesetzt
- 🟡 teilweise umgesetzt oder weicht ab
- ⬜ fehlt noch

## Design

- 🟡 Web-App mit responsivem Design, gut bedienbar auf PC, iPad und iPhone. Tailwind ist im Einsatz; auf Tablet und Handy ist die Oberfläche bisher nicht gezielt geprüft.
- 🟡 Modern, übersichtlich und aufgeräumt, ohne Schnickschnack.

## Bankkonten

- 🟡 Bankkonten anlegen, verwalten und löschen. Anlegen (mit Pflicht-IBAN) und Löschen gehen; Umbenennen oder Bearbeiten gibt es noch nicht.

## Benutzer

- ✅ Mehrere Benutzer mit eigener Anmeldung; jeder sieht nur seine eigenen Konten.
- ⬜ Geteilte Konten (z. B. Gemeinschaftskonto). Alle Mitglieder eines Kontos sind gleichberechtigt: Buchungen, Kategorien, Regeln, Teilen und Löschen. Das Datenmodell erlaubt schon mehrere Nutzer pro Konto (`Account.Users`), es gibt aber keinen Weg, ein Konto mit jemandem zu teilen.

## Kontoauszüge

- ✅ Buchungen über FinTS abrufen (ING), pro Konto, mit gespeicherter PIN und nächtlichem Abruf.
- ✅ Buchungen per CSV importieren (ING-Format), mit Duplikat-Abgleich gegen FinTS-Buchungen.
- ⬜ Buchungen als CSV exportieren.
- ✅ Buchungen von Hand anlegen.

## Kategorien

- 🟡 Kategorien anlegen und verwalten, Buchungen zuordnen. Anlegen, Löschen und Zuordnen gehen; Bearbeiten (Name, Farbe, Lage im Baum) fehlt.
- ⬜ Baumstruktur mit beliebig vielen Ebenen (z. B. Autos → Passat, Corsa; Lebensmittel → Süßkram, Brot). `Category.ParentCategoryId` existiert im Modell, Oberfläche und Regeln nutzen es nicht.
- 🟡 Jedes Konto hat seine eigenen Kategorien, damit geteilte Konten nicht im Chaos enden. **Abweichung:** heute gehören Kategorien dem Nutzer (PR #5/#6). Beim Umbau startet jedes Konto leer: die heutigen Nutzer-Kategorien und -Regeln werden nicht übernommen, die Zuordnungen der Buchungen fallen damit weg.

## Regeln

- ✅ Buchungen werden anhand von Regeln automatisch kategorisiert (Teilstring in Auftraggeber, Buchungstext, Verwendungszweck oder allen).
- 🟡 Jedes Konto hat seine eigenen Regeln. **Abweichung:** Regeln hängen heute an Nutzer-Kategorien; sie ziehen mit den Kategorien auf die Konten um.
- ✅ Regeln nachträglich auf alle alten Buchungen ohne Kategorie anwenden.
- ✅ Neue Buchungen (manuell, CSV, FinTS) bekommen automatisch die passenden Regeln.

## Push-Benachrichtigungen

- 🟡 Push auf Christophs Geräten, sobald eine oder mehrere neue Buchungen ohne Kategorie da sind, über Home Assistant (siehe Entscheidungen). Die Verbindung steht: Home Assistant ist konfigurierbar (`HomeAssistant__Url`, `__Token`, `__ZasterUrl`), jeder Nutzer wählt unter „Benachrichtigungen“ seine Geräte aus und kann eine Testnachricht schicken. Die automatische Nachricht bei neuen Buchungen ohne Kategorie fehlt noch.
- 🟡 Ein Tipp auf die Benachrichtigung öffnet die Seite, auf der man den Buchungen Kategorien zuweist. Heute öffnet die Testnachricht die Startseite von Zaster, weil es die Seite „ohne Kategorie“ noch nicht gibt.
- 🟡 Buchungen werden regelmäßig per FinTS geladen; bleibt danach eine Buchung ohne Kategorie, kommt eine Push-Nachricht. Die Abrufzeiten sind konfigurierbar, auch mehrere pro Tag (z. B. 09:00 und 14:00). Heute gibt es nur eine Uhrzeit (`FinTS__NightlySyncTime`, Standard 04:00); die Liste und die Push-Nachricht fehlen.

## Diagramme

- ⬜ Verteilung der Ein- und Ausgaben als Sankey-Flussdiagramm, pro Konto.

## Allgemein

- ⬜ Eigene Seite mit allen Buchungen ohne Kategorie.
- ⬜ Taste „Alle als geprüft markieren“; Buchungen dürfen auch ohne Kategorie bleiben. So lassen sich alle alten Buchungen ohne Kategorie abhaken. Jede Buchung hat dafür ein Merkmal „geprüft“; eine Buchung mit Kategorie gilt automatisch als geprüft. Seite „ohne Kategorie“ und Push zählen nur Buchungen, die nicht geprüft sind.
- ✅ Kategorien einer Buchung lassen sich nachträglich setzen und ändern.

## Offene Fragen

Keine. Neue Fragen hier mit Vorschlag sammeln und nach Christophs Antwort unter „Entscheidungen“ eintragen.

## Entscheidungen

Am 10.10.2026 von Christoph entschieden:

1. **Geteilte Konten:** alle Mitglieder sind gleichberechtigt.
2. **Push über Home Assistant** statt Web-Push. Zaster ruft über die REST-API von Home Assistant den Dienst `notify.mobile_app_<gerät>` auf; die Home-Assistant-App zeigt die Nachricht, ein Tipp öffnet die Seite „ohne Kategorie“. Adresse und Token (Long-Lived Access Token) von Home Assistant kommen in die Konfiguration (`.env`), das Zielgerät stellt jeder Nutzer in Zaster selbst ein. Zaster muss vom Handy aus erreichbar sein, damit der Link funktioniert. Kein MQTT nötig.
3. **„Geprüft“** (statt „erledigt“): Merkmal pro Buchung; eine Buchung mit Kategorie gilt automatisch als geprüft. Taste „Alle als geprüft markieren“.
4. **Kategorien beim Umbau auf „pro Konto“:** jedes Konto startet leer.
5. **FinTS-Abruf:** Uhrzeiten konfigurierbar, als Liste mit mehreren Zeitpunkten (z. B. 09:00 und 14:00).
6. **Sankey:** pro Konto.

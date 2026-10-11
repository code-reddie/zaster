# Anforderungen

Christophs Anforderungen an Zaster (Stand 10.10.2026, Entscheidungen vom selben Tag eingearbeitet; Umsetzungsstand nach dem Refactoring vom 11.10.2026), geordnet nach Bereichen. Hinter jedem Punkt steht der Umsetzungsstand:

- ✅ umgesetzt
- 🟡 teilweise umgesetzt oder weicht ab
- ⬜ fehlt noch

## Design

- ✅ Web-App mit responsivem Design, gut bedienbar auf PC, iPad und iPhone. Ab 1024 px Breite Seitenleiste, darunter Kontowahl oben, Navigation unten und Dialoge als Sheet von unten. Geprüft in den Breiten von iPhone, iPad hoch und PC, hell und dunkel.
- ✅ Modern, übersichtlich und aufgeräumt, ohne Schnickschnack.

## Bankkonten

- ✅ Bankkonten anlegen (mit Pflicht-IBAN, jede IBAN gibt es in Zaster nur einmal), umbenennen, FinTS-Zugangsnummer ändern und löschen.

## Benutzer

- ✅ Mehrere Benutzer mit eigener Anmeldung; jeder sieht nur seine eigenen Konten.
- ⬜ Geteilte Konten (z. B. Gemeinschaftskonto). Alle Mitglieder eines Kontos sind gleichberechtigt: Buchungen, Kategorien, Regeln, Teilen und Löschen. Das Datenmodell erlaubt schon mehrere Nutzer pro Konto (`Account.Users`), und die Kontoseite zeigt die Mitglieder; es gibt aber noch keinen Weg, ein Konto mit jemandem zu teilen. Weil jede IBAN nur einmal vorkommen darf, kann ein zweiter Nutzer dasselbe Bankkonto nicht selbst anlegen, sondern kommt später nur über das Teilen dazu.

## Kontoauszüge

- ✅ Buchungen über FinTS abrufen (ING), pro Konto, mit gespeicherter PIN und automatischem Abruf zu einstellbaren Uhrzeiten.
- ✅ Buchungen per CSV importieren (ING-Format), mit Duplikat-Abgleich gegen FinTS-Buchungen.
- ✅ Kontostand nach jeder Buchung speichern (CSV-Spalte „Saldo“, bei FinTS aus Anfangs- und Endsaldo des Kontoauszugs berechnet) und damit prüfen, ob Buchungen fehlen (Christoph, 11.10.2026). Anzeige unter „Konto & Abruf“.
- ⬜ Buchungen als CSV exportieren.
- ✅ Buchungen von Hand anlegen.

## Kategorien

- ✅ Kategorien anlegen und verwalten (Name, Farbe, Lage im Baum, Reihenfolge), Buchungen zuordnen.
- ✅ Baumstruktur mit beliebig vielen Ebenen (z. B. Autos → Passat, Corsa; Lebensmittel → Süßkram, Brot). Löschen nimmt die Unterkategorien mit, ihre Buchungen sind danach wieder ohne Kategorie.
- ✅ Jedes Konto hat seine eigenen Kategorien, damit geteilte Konten nicht im Chaos enden. Beim Umbau hat jedes Konto leer angefangen (Entscheidung 4).
- ✅ Eine Buchung lässt sich auf mehrere Kategorien aufteilen (z. B. Einkauf im Kaufland: Lebensmittel und eine Bohrmaschine unter „Werkzeuge“). Die Teilbeträge ergeben zusammen immer den ganzen Buchungsbetrag (Entscheidung 7). Kategoriefilter und Summen der Buchungsliste rechnen mit den Teilbeträgen; Sankey und CSV-Export müssen das später auch tun.

## Regeln

- ✅ Buchungen werden anhand von Regeln automatisch kategorisiert (Teilstring in Auftraggeber, Buchungstext, Verwendungszweck oder allen).
- ✅ Jedes Konto hat seine eigenen Regeln. Die Reihenfolge ist einstellbar, die erste passende Regel gewinnt.
- ✅ Regeln nachträglich auf alle alten Buchungen ohne Kategorie anwenden.
- ✅ Neue Buchungen (manuell, CSV, FinTS) bekommen automatisch die passenden Regeln.

## Push-Benachrichtigungen

- 🟡 Push auf Christophs Geräten, sobald eine oder mehrere neue Buchungen ohne Kategorie da sind, über Home Assistant (siehe Entscheidungen). Die Verbindung steht: Home Assistant ist konfigurierbar (`HomeAssistant__Url`, `__Token`, `__ZasterUrl`), jeder Nutzer wählt unter „Einstellungen“ seine Geräte aus und kann eine Testnachricht schicken. Die automatische Nachricht bei neuen Buchungen ohne Kategorie fehlt noch.
- 🟡 Ein Tipp auf die Benachrichtigung öffnet die Seite, auf der man den Buchungen Kategorien zuweist. Die Seite gibt es jetzt (`/konten/<id>/offen`); die Testnachricht öffnet noch die Startseite, den Link auf die Seite bekommt die automatische Nachricht.
- 🟡 Buchungen werden regelmäßig per FinTS geladen; bleibt danach eine Buchung ohne Kategorie, kommt eine Push-Nachricht. Die Abrufzeiten stellt man pro Konto unter „Konto & Abruf“ ein, auch mehrere pro Tag (z. B. 09:00 und 14:00); das ist fertig. Die Push-Nachricht danach fehlt noch.

## Diagramme

- ⬜ Verteilung der Ein- und Ausgaben als Sankey-Flussdiagramm, pro Konto.

## Allgemein

- ✅ Eigene Seite mit allen Buchungen ohne Kategorie („Ohne Kategorie“, mit Zähler in der Navigation).
- ✅ Taste „Alle nicht kategorisieren“ (pro Buchung „Nicht kategorisieren“); Buchungen dürfen auch ohne Kategorie bleiben. So lassen sich alle alten Buchungen ohne Kategorie abhaken. Jede Buchung hat dafür ein Merkmal „geprüft“; eine Buchung mit Kategorie gilt automatisch als geprüft. Seite „ohne Kategorie“ und Push zählen nur Buchungen, die nicht geprüft sind.
- ✅ Kategorien einer Buchung lassen sich nachträglich setzen und ändern.

## Offene Fragen

Keine. Neue Fragen hier mit Vorschlag sammeln und nach Christophs Antwort unter „Entscheidungen“ eintragen.

## Entscheidungen

Am 10.10.2026 von Christoph entschieden:

1. **Geteilte Konten:** alle Mitglieder sind gleichberechtigt.
2. **Push über Home Assistant** statt Web-Push. Zaster ruft über die REST-API von Home Assistant den Dienst `notify.mobile_app_<gerät>` auf; die Home-Assistant-App zeigt die Nachricht, ein Tipp öffnet die Seite „ohne Kategorie“. Adresse und Token (Long-Lived Access Token) von Home Assistant kommen in die Konfiguration (`.env`), das Zielgerät stellt jeder Nutzer in Zaster selbst ein. Zaster muss vom Handy aus erreichbar sein, damit der Link funktioniert. Kein MQTT nötig.
3. **„Geprüft“** (statt „erledigt“): Merkmal pro Buchung; eine Buchung mit Kategorie gilt automatisch als geprüft. Tasten heißen „Nicht kategorisieren“ und „Alle nicht kategorisieren“ (Christoph, 11.10.2026).
4. **Kategorien beim Umbau auf „pro Konto“:** jedes Konto startet leer.
5. **FinTS-Abruf:** Uhrzeiten konfigurierbar, als Liste mit mehreren Zeitpunkten (z. B. 09:00 und 14:00). Eingestellt werden sie pro Konto in der App unter „Konto & Abruf“, nicht in `appsettings.json` (Christoph, 11.10.2026). Leere Liste = kein automatischer Abruf.
6. **Sankey:** pro Konto.
7. **Aufteilen von Buchungen** (Christoph wünscht es am 11.10.2026, Details von Claude festgelegt): Eine Buchung hat keine, eine oder mehrere Kategorien mit je einem Teilbetrag. Die Teile ergeben zusammen genau den Buchungsbetrag, halb kategorisierte Buchungen gibt es nicht; im Dialog bekommt die erste Kategorie automatisch den Rest. Eine Kategorie wählen (von Hand, per Schnellwahl oder Regel) weist den ganzen Betrag zu. Regeln überschreiben keine Aufteilung. Wird eine Kategorie gelöscht, verliert eine aufgeteilte Buchung alle Teile und ist wieder offen.

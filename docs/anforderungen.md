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

- ✅ Mehrere Benutzer mit eigener Anmeldung; jeder sieht nur seine eigenen und mit ihm geteilten Konten. Die Anmeldung verlängert sich beim Benutzen von selbst; neu anmelden muss man sich erst nach 7 Tagen ohne Nutzung.
- ✅ Geteilte Konten (z. B. Gemeinschaftskonto). Alle Mitglieder eines Kontos sind gleichberechtigt: Buchungen, Kategorien, Regeln, Teilen und Löschen. Unter „Konto & Abruf“ → „Mitglieder“ fügt jedes Mitglied eine schon registrierte Person über ihren Anmeldenamen hinzu (sofort Mitglied, ohne Einladung zum Annehmen), entfernt andere oder verlässt das Konto selbst. Das letzte Mitglied kann nicht austreten, nur das Konto löschen. Weil jede IBAN nur einmal vorkommen darf, kommt ein zweiter Nutzer nur über das Teilen an dasselbe Bankkonto.

## Kontoauszüge

- ✅ Buchungen über FinTS abrufen (ING), pro Konto, mit gespeicherter PIN und automatischem Abruf zu einstellbaren Uhrzeiten.
- ✅ Buchungen per CSV importieren, mit Duplikat-Abgleich gegen FinTS-Buchungen. Zwei Aufbauten, Zaster erkennt sie selbst über die Kopfzeile: der Umsatzexport der ING und Christophs eigene Liste mit allen alten Buchungen (`Buchung;Valuta;Auftraggeber/Empfänger;Buchungstext;Verwendungszweck;Betrag;Saldo`) (Christoph, 12.10.2026).
- ✅ Kontostand nach jeder Buchung speichern (CSV-Spalte „Saldo“, bei FinTS aus Anfangs- und Endsaldo des Kontoauszugs berechnet) und damit prüfen, ob Buchungen fehlen (Christoph, 11.10.2026). Anzeige unter „Konto & Abruf“.
- ✅ Buchungen von Hand anlegen.
- ✅ Zu jeder Buchung eine eigene Bemerkung eintragen, wenn der Verwendungszweck nicht verrät, worum es ging (Christoph, 12.10.2026; Entscheidung 11).

## Kategorien

- ✅ Kategorien anlegen und verwalten (Name, Farbe, Lage im Baum; je Ebene alphabetisch sortiert), Buchungen zuordnen.
- ✅ Baumstruktur mit beliebig vielen Ebenen (z. B. Autos → Passat, Corsa; Lebensmittel → Süßkram, Brot). Löschen nimmt die Unterkategorien mit, ihre Buchungen sind danach wieder ohne Kategorie.
- ✅ Jedes Konto hat seine eigenen Kategorien, damit geteilte Konten nicht im Chaos enden. Beim Umbau hat jedes Konto leer angefangen (Entscheidung 4).
- ✅ Eine Buchung lässt sich auf mehrere Kategorien aufteilen (z. B. Einkauf im Kaufland: Lebensmittel und eine Bohrmaschine unter „Werkzeuge“). Die Teilbeträge ergeben zusammen immer den ganzen Buchungsbetrag (Entscheidung 7). Kategoriefilter, Summen der Buchungsliste und das Sankey-Diagramm rechnen mit den Teilbeträgen.

## Regeln

- ✅ Buchungen werden anhand von Regeln automatisch kategorisiert (Teilstring in Auftraggeber, Buchungstext, Verwendungszweck oder allen).
- ✅ Jedes Konto hat seine eigenen Regeln. Die Reihenfolge ist einstellbar, die erste passende Regel gewinnt.
- ✅ Regeln nachträglich auf alle alten Buchungen ohne Kategorie anwenden.
- ✅ Pro Regel festlegen, ob sie für eingehende, ausgehende oder beide Arten von Buchungen gilt (Christoph, 12.10.2026). Standard und Stand bestehender Regeln: beide. „Regel daraus machen“ wählt die Richtung der Buchung vor; in der Regel-Liste steht „nur Einnahmen“ bzw. „nur Ausgaben“. Buchungen mit Betrag 0 passen nur zu Regeln für beide Richtungen.
- ✅ Neue Buchungen (manuell, CSV, FinTS) bekommen automatisch die passenden Regeln.

## Push-Benachrichtigungen

- ✅ Push auf Christophs Geräten, sobald eine oder mehrere neue Buchungen ohne Kategorie da sind, über Home Assistant (siehe Entscheidungen). Home Assistant ist konfigurierbar (`HomeAssistant__Url`, `__Token`, `__ZasterUrl`), jeder Nutzer wählt unter „Einstellungen“ seine Geräte aus und kann eine Testnachricht schicken. Nach jedem FinTS-Abruf und CSV-Import bekommen alle Mitglieder des Kontos eine Nachricht mit der Zahl der neuen Buchungen, die nach den Regeln noch ohne Kategorie sind (und wie viele insgesamt offen sind). Von Hand angelegte Buchungen lösen keine Nachricht aus.
- ✅ Ein Tipp auf die Benachrichtigung öffnet die Seite „Ohne Kategorie“ des Kontos (`/konten/<id>/offen`). Ist man nicht angemeldet, geht es nach der Anmeldung dorthin. Die Testnachricht öffnet die Startseite.
- ✅Buchungen werden regelmäßig per FinTS geladen; bleibt danach eine Buchung ohne Kategorie, kommt eine Push-Nachricht. Die Abrufzeiten stellt man pro Konto unter „Konto & Abruf“ ein, auch mehrere pro Tag (z. B. 09:00 und 14:00).

## Diagramme

- ✅ Verteilung der Ein- und Ausgaben als Sankey-Flussdiagramm, pro Konto (Seite „Verteilung“, `/konten/<id>/verteilung`): Einnahmen links, Konto in der Mitte, Ausgaben rechts, Unterkategorien per Tipp aufklappbar; Zeitraum Monat, Jahr (Standard: laufendes Jahr), alles oder von–bis. Erstattungen werden innerhalb einer Kategorie verrechnet, Buchungen ohne Kategorie erscheinen als „Ohne Kategorie“, die Differenz als „Überschuss“ bzw. „Aus dem Kontostand“.

## Allgemein

- ✅ Eigene Seite mit allen Buchungen ohne Kategorie („Ohne Kategorie“, mit Zähler in der Navigation).
- ✅ Schnellwahl „Schnell zuordnen:“ an jeder Buchung mit den vier zuletzt benutzten Kategorien. Der Browser speichert sie im Local Storage pro Nutzer und Konto, sie bleiben also beim Seitenwechsel und nach dem Neuladen erhalten. Gelöschte Kategorien fallen heraus.
- ✅ Taste „Alle nicht kategorisieren“ (pro Buchung „Nicht kategorisieren“); Buchungen dürfen auch ohne Kategorie bleiben. So lassen sich alle alten Buchungen ohne Kategorie abhaken. Jede Buchung hat dafür ein Merkmal „geprüft“; eine Buchung mit Kategorie gilt automatisch als geprüft. Seite „ohne Kategorie“ und Push zählen nur Buchungen, die nicht geprüft sind.
- ✅ Kategorien einer Buchung lassen sich nachträglich setzen und ändern.
- ✅ Taste zum Herunterladen der kompletten Datenbank als Sicherung, unter „Einstellungen“ (Christoph, 11.10.2026, ersetzt den CSV-Export; Entscheidung 8).
- ✅ Automatische Sicherung auf das NAS der FritzBox, einmal am Tag, überschreibt immer dieselbe Datei; schlägt sie fehl, kommt eine Nachricht über Home Assistant (Christoph, 11.10.2026; Entscheidung 9).

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
8. **Sicherung statt CSV-Export** (Christoph, 11.10.2026): Der CSV-Export entfällt. Stattdessen lädt eine Taste unter „Einstellungen“ die komplette Datenbank als SQLite-Datei herunter, eine konsistente Kopie der laufenden Datenbank. Weil die Datei die Daten aller Nutzer samt Passwort-Hashes enthält, sieht die Taste nur der Nutzer, der Zaster eingerichtet hat (der zuerst registrierte). Die Schlüssel für die FinTS-PINs (`/data/keys`) sind nicht dabei.
9. **Automatische Sicherung auf die FritzBox** (Christoph wünscht es am 11.10.2026, Details von Claude festgelegt): Zaster schreibt jeden Tag zur eingestellten Uhrzeit (Standard 03:00, einstellbar unter „Einstellungen“ → „Sicherung“, leer = aus) eine Kopie der Datenbank als `zaster.db` in einen Ordner und überschreibt sie jedes Mal. Den Ordner (`Backup__Directory`) bindet der Server als SMB-Freigabe der FritzBox ein, `docker compose` reicht ihn an Zaster weiter; Zaster selbst kennt kein SMB. Ohne Ordner gibt es keine automatische Sicherung. Einstellen und „Jetzt sichern“ darf nur, wer Zaster eingerichtet hat, wie beim Herunterladen. Schlägt die automatische Sicherung fehl (z. B. FritzBox nicht erreichbar), bleibt die vorige Datei erhalten, und Zaster schickt über Home Assistant eine Nachricht an die Geräte dieses Nutzers (Christoph, 11.10.2026); ein Tipp öffnet die Einstellungen. Kein zweiter Versuch am selben Tag.
10. **Konten teilen** (Christoph wünscht es am 12.10.2026, Details von Claude festgelegt): Ein Mitglied fügt eine schon registrierte Person über ihren genauen Anmeldenamen hinzu; sie ist sofort Mitglied, ohne Einladung zum Annehmen. Jedes Mitglied kann andere entfernen oder das Konto selbst verlassen. Das letzte Mitglied kann nicht austreten, nur das Konto löschen.
11. **Bemerkung zu Buchungen** (Christoph wünscht es am 12.10.2026, Details von Claude festgelegt): Jede Buchung hat ein optionales Textfeld „Bemerkung“ (höchstens 500 Zeichen). Eintragen lässt es sich in den Details einer Buchung und auf „Ohne Kategorie“, ohne dass die Buchung dadurch geprüft wird; neue Buchungen von Hand können gleich eine bekommen. In den Listen steht sie unter dem Auftraggeber, die Suche in den Buchungen findet sie mit. Regeln werten sie nicht aus. Import und Abruf lassen sie unverändert, auch wenn dieselbe Buchung noch einmal kommt. Alle Mitglieder eines geteilten Kontos sehen und ändern dieselbe Bemerkung.

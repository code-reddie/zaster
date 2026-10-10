# Anforderungen

Christophs Anforderungen an Zaster (Stand 10.10.2026), geordnet nach Bereichen. Hinter jedem Punkt steht der Umsetzungsstand:

- ✅ umgesetzt
- 🟡 teilweise umgesetzt oder weicht ab
- ⬜ fehlt noch
- ❓ offene Frage, Antwort von Christoph steht aus (siehe [Offene Fragen](#offene-fragen))

## Design

- 🟡 Web-App mit responsivem Design, gut bedienbar auf PC, iPad und iPhone. Tailwind ist im Einsatz; auf Tablet und Handy ist die Oberfläche bisher nicht gezielt geprüft.
- 🟡 Modern, übersichtlich und aufgeräumt, ohne Schnickschnack.

## Bankkonten

- 🟡 Bankkonten anlegen, verwalten und löschen. Anlegen (mit Pflicht-IBAN) und Löschen gehen; Umbenennen oder Bearbeiten gibt es noch nicht.

## Benutzer

- ✅ Mehrere Benutzer mit eigener Anmeldung; jeder sieht nur seine eigenen Konten.
- ⬜ ❓ Geteilte Konten (z. B. Gemeinschaftskonto). Das Datenmodell erlaubt schon mehrere Nutzer pro Konto (`Account.Users`), es gibt aber keinen Weg, ein Konto mit jemandem zu teilen. Offen: Rechte der Mitglieder.

## Kontoauszüge

- ✅ Buchungen über FinTS abrufen (ING), pro Konto, mit gespeicherter PIN und nächtlichem Abruf.
- ✅ Buchungen per CSV importieren (ING-Format), mit Duplikat-Abgleich gegen FinTS-Buchungen.
- ⬜ Buchungen als CSV exportieren.
- ✅ Buchungen von Hand anlegen.

## Kategorien

- 🟡 Kategorien anlegen und verwalten, Buchungen zuordnen. Anlegen, Löschen und Zuordnen gehen; Bearbeiten (Name, Farbe, Lage im Baum) fehlt.
- ⬜ Baumstruktur mit beliebig vielen Ebenen (z. B. Autos → Passat, Corsa; Lebensmittel → Süßkram, Brot). `Category.ParentCategoryId` existiert im Modell, Oberfläche und Regeln nutzen es nicht.
- 🟡 ❓ Jedes Konto hat seine eigenen Kategorien, damit geteilte Konten nicht im Chaos enden. **Abweichung:** heute gehören Kategorien dem Nutzer (PR #5/#6). Offen: wie bestehende Kategorien auf Konten übergehen.

## Regeln

- ✅ Buchungen werden anhand von Regeln automatisch kategorisiert (Teilstring in Auftraggeber, Buchungstext, Verwendungszweck oder allen).
- 🟡 Jedes Konto hat seine eigenen Regeln. **Abweichung:** Regeln hängen heute an Nutzer-Kategorien; sie ziehen mit den Kategorien auf die Konten um.
- ✅ Regeln nachträglich auf alle alten Buchungen ohne Kategorie anwenden.
- ✅ Neue Buchungen (manuell, CSV, FinTS) bekommen automatisch die passenden Regeln.

## Push-Benachrichtigungen

- ⬜ ❓ Push auf Christophs Geräten, sobald eine oder mehrere neue Buchungen ohne Kategorie da sind. Offen: Technik (Web-Push) und was das fürs iPhone heißt.
- ⬜ Ein Tipp auf die Benachrichtigung öffnet die Seite, auf der man den Buchungen Kategorien zuweist.
- 🟡 ❓ Buchungen werden regelmäßig per FinTS geladen; bleibt danach eine Buchung ohne Kategorie, kommt eine Push-Nachricht. Der regelmäßige Abruf läuft heute einmal pro Nacht (04:00, `FinTS__NightlySyncTime`); die Push-Nachricht fehlt. Offen: ob einmal pro Nacht reicht.

## Diagramme

- ⬜ ❓ Verteilung der Ein- und Ausgaben als Sankey-Flussdiagramm. Offen: pro Konto oder über alle Konten.

## Allgemein

- ⬜ Eigene Seite mit allen Buchungen ohne Kategorie.
- ⬜ ❓ Taste, um alle Buchungen als erledigt zu markieren; Buchungen dürfen auch ohne Kategorie bleiben. So lassen sich alle alten Buchungen ohne Kategorie abhaken. Offen: was „erledigt“ genau bewirkt.
- ✅ Kategorien einer Buchung lassen sich nachträglich setzen und ändern.

## Offene Fragen

Jede Frage mit Vorschlag. Nach Christophs Antwort hier die Entscheidung eintragen und das ❓ oben entfernen.

1. **Rechte bei geteilten Konten.** Vorschlag: alle Mitglieder eines Kontos sind gleichberechtigt (Buchungen, Kategorien, Regeln, Teilen, Löschen). Alternative: ein Besitzer, die anderen nur lesen oder kategorisieren.
2. **Push-Technik.** Vorschlag: Web-Push über eine installierbare Web-App (PWA). Auf dem iPhone und iPad klappt das erst, wenn Zaster über „Zum Home-Bildschirm“ hinzugefügt wurde (ab iOS 16.4), und nur über HTTPS mit gültigem Zertifikat. Alternative: Benachrichtigung über einen Dienst wie ntfy oder Telegram.
3. **Bedeutung von „erledigt“.** Vorschlag: jede Buchung bekommt ein Merkmal „erledigt“. Die Seite „ohne Kategorie“ und die Push-Nachricht zählen nur Buchungen, die weder eine Kategorie haben noch erledigt sind. Wer einer Buchung eine Kategorie gibt, erledigt sie automatisch.
4. **Übergang der heutigen Kategorien.** Vorschlag: die Kategorien und Regeln eines Nutzers werden in jedes seiner Konten kopiert; Zuordnungen der Buchungen bleiben erhalten. Alternative: jedes Konto startet leer.
5. **Häufigkeit des FinTS-Abrufs.** Vorschlag: einmal pro Nacht reicht. Häufiger erhöht das Risiko, dass die ING eine Freigabe in der App verlangt.
6. **Sankey über mehrere Konten.** Vorschlag: pro Konto, weil jedes Konto eigene Kategorien hat; eine Gesamtansicht bräuchte gemeinsame Kategorien.

## Entscheidungen

Noch keine.

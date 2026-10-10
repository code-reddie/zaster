# Anforderungen

Christophs Anforderungen an Zaster (Stand 10.10.2026, Entscheidungen vom selben Tag eingearbeitet), geordnet nach Bereichen. Hinter jedem Punkt steht der Umsetzungsstand:

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

- ⬜ ❓ Push auf Christophs Geräten, sobald eine oder mehrere neue Buchungen ohne Kategorie da sind. Geplant über Home Assistant (siehe Entscheidungen), Bestätigung steht aus.
- ⬜ Ein Tipp auf die Benachrichtigung öffnet die Seite, auf der man den Buchungen Kategorien zuweist.
- 🟡 Buchungen werden regelmäßig per FinTS geladen; bleibt danach eine Buchung ohne Kategorie, kommt eine Push-Nachricht. Die Abrufzeiten sind konfigurierbar, auch mehrere pro Tag (z. B. 09:00 und 14:00). Heute gibt es nur eine Uhrzeit (`FinTS__NightlySyncTime`, Standard 04:00); die Liste und die Push-Nachricht fehlen.

## Diagramme

- ⬜ Verteilung der Ein- und Ausgaben als Sankey-Flussdiagramm, pro Konto.

## Allgemein

- ⬜ Eigene Seite mit allen Buchungen ohne Kategorie.
- ⬜ ❓ Taste, um alle Buchungen als erledigt zu markieren; Buchungen dürfen auch ohne Kategorie bleiben. So lassen sich alle alten Buchungen ohne Kategorie abhaken. Jede Buchung hat dafür ein Merkmal „erledigt“; eine Buchung mit Kategorie gilt automatisch als erledigt. Seite „ohne Kategorie“ und Push zählen nur Buchungen, die nicht erledigt sind. Offen: der Name des Merkmals.
- ✅ Kategorien einer Buchung lassen sich nachträglich setzen und ändern.

## Offene Fragen

1. **Push über Home Assistant.** Vorschlag: Zaster ruft den Benachrichtigungsdienst von Home Assistant auf (`notify.mobile_app_<gerät>`); die Home-Assistant-App zeigt die Nachricht auf iPhone und iPad, ein Tipp öffnet die Zaster-Seite „ohne Kategorie“. Adresse und Token von Home Assistant kommen in die Konfiguration, welches Gerät benachrichtigt wird, stellt jeder Nutzer selbst ein. Wartet auf Christophs Bestätigung.
2. **Name für „erledigt“.** Vorschlag: „geprüft“ (Taste „Alle als geprüft markieren“). Wartet auf Christophs Antwort.

## Entscheidungen

Am 10.10.2026 von Christoph entschieden:

1. **Geteilte Konten:** alle Mitglieder sind gleichberechtigt.
2. **Push:** Christoph nutzt zu Hause Home Assistant und bringt es statt Web-Push ins Spiel; noch nicht endgültig (siehe Offene Fragen 1).
3. **Erledigt:** Merkmal pro Buchung; eine Buchung mit Kategorie gilt automatisch als erledigt.
4. **Kategorien beim Umbau auf „pro Konto“:** jedes Konto startet leer.
5. **FinTS-Abruf:** Uhrzeiten konfigurierbar, als Liste mit mehreren Zeitpunkten (z. B. 09:00 und 14:00).
6. **Sankey:** pro Konto.

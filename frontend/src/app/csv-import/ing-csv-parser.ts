import { CreateTransaction } from '../transaction/transaction.models';

export function parseIngCsv(content: string, accountId: number): CreateTransaction[] {
  const lines = content.split('\n');

  // Daten starten nach der Zeile die mit "Buchung" beginnt
  const headerIndex = lines.findIndex((l) => l.startsWith('Buchung;'));
  if (headerIndex === -1) return [];

  return lines
    .slice(headerIndex + 1)
    .filter((line) => line.trim().length > 0)
    .map((line) => {
      // Spalten: Buchung;Wertstellung;Auftraggeber;Buchungstext;Verwendungszweck;Saldo;Währung;Betrag;Währung
      const columns = line.split(';');

      return {
        buchung: parseIngDate(columns[0]),
        valuta: parseIngDate(columns[1]),
        auftragsgeber: columns[2]?.trim() ?? '',
        buchungstext: columns[3]?.trim() ?? '',
        verwendungszweck: columns[4]?.trim() ?? '',
        betrag: parseIngBetrag(columns[7]), // Spalte 7 ist der Betrag (nicht Saldo!)
        accountId,
      };
    });
}

function parseIngDate(value: string): string {
  // ING Format: 04.05.2026 → ISO 8601
  const [day, month, year] = value.trim().split('.');
  return new Date(`${year}-${month}-${day}`).toISOString();
}

function parseIngBetrag(value: string): number {
  // ING Format: -1.000,00 → -1000.00
  return parseFloat(value.trim().replace(/\./g, '').replace(',', '.'));
}

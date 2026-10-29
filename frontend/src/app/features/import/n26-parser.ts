import { ImportTransaction } from '../transactions/transaction.models';
import { ImportFileError } from './trade-republic-parser';

/** Spaltennamen im CSV-Export von N26, je Bedeutung: der aktuelle Aufbau zuerst, dann der ältere. */
const COLUMNS = {
  buchung: ['Booking Date', 'Date'],
  valuta: ['Value Date'],
  partner: ['Partner Name', 'Payee'],
  iban: ['Partner Iban', 'Account number'],
  type: ['Type', 'Transaction type'],
  reference: ['Payment Reference', 'Payment reference'],
  betrag: ['Amount (EUR)'],
  fremdbetrag: ['Original Amount', 'Amount (Foreign Currency)'],
  fremdwaehrung: ['Original Currency', 'Type Foreign Currency'],
  devisenkurs: ['Exchange Rate'],
};

/** Buchungstext je Art der Buchung (Spalte `Type`). Unbekannte Arten bleiben, wie sie sind. */
const TYPE_LABELS: Record<string, string> = {
  'Credit Transfer': 'Gutschrift',
  'Debit Transfer': 'Überweisung',
  'Direct Debit': 'Lastschrift',
  'Direct Debit Reversal': 'Rücklastschrift',
  Presentment: 'Kartenzahlung',
  'Presentment Refund': 'Kartenerstattung',
  'ATM Withdrawal': 'Bargeldabhebung',
  Reward: 'Prämie',
  'N26 Referral': 'Empfehlungsprämie',
  Fee: 'Gebühr',
  // Älterer Export
  Income: 'Gutschrift',
  'Outgoing Transfer': 'Überweisung',
  'MasterCard Payment': 'Kartenzahlung',
  MoneyBeam: 'MoneyBeam',
};

/**
 * Erkennt den CSV-Export von N26 an der Kopfzeile: Komma, Spalte „Amount (EUR)“ und ein Buchungstag
 * („Booking Date“, im älteren Export „Date“).
 */
export function isN26(header: string[] | undefined): boolean {
  const names = header?.map((h) => h.trim()) ?? [];
  return names.includes('Amount (EUR)') && COLUMNS.buchung.some((name) => names.includes(name));
}

/**
 * Liest den CSV-Export von N26 (App oder Web: Umsätze → CSV-Export): Komma, Felder teils in
 * Anführungszeichen, Datum ISO, Beträge in Euro mit Punkt.
 *
 * - Der Export hat keinen Kontostand und keine Kennung der Buchung. Duplikate erkennt Zaster daher
 *   wie bei der ING über Buchungstag, Betrag und Auftraggeber.
 * - Den Kontostand rechnet Zaster nicht hoch: Der Zeitraum des Exports ist frei wählbar, er beginnt
 *   also nicht sicher bei der Kontoeröffnung.
 * - Fremdwährung und Kurs werden nur übernommen, wenn die Zahlung nicht in Euro war (N26 schreibt bei
 *   Kartenzahlungen in Euro „EUR“ und Kurs 1 dazu).
 * - `Account Name` (der Space bei N26) wird nicht gespeichert.
 */
export function parseN26(records: string[][]): ImportTransaction[] {
  const header = records[0].map((h) => h.trim());
  const index = (names: string[]) => header.findIndex((h) => names.includes(h));
  const columns = Object.fromEntries(
    Object.entries(COLUMNS).map(([key, names]) => [key, index(names)]),
  ) as Record<keyof typeof COLUMNS, number>;
  const value = (record: string[], column: number) =>
    column === -1 ? '' : (record[column]?.trim() ?? '');

  return records.slice(1).map((record) => {
    const buchung = value(record, columns.buchung);
    if (!/^\d{4}-\d{2}-\d{2}$/.test(buchung)) {
      throw new ImportFileError(`Unbekanntes Datum „${buchung}“ in der Datei.`);
    }
    const valuta = value(record, columns.valuta);
    const type = value(record, columns.type);
    const betrag = number(value(record, columns.betrag), 'Amount (EUR)');
    if (betrag === null) {
      throw new ImportFileError(`Die Buchung vom ${germanDate(buchung)} hat keinen Betrag.`);
    }
    const fremdwaehrung = value(record, columns.fremdwaehrung);
    const foreign = !!fremdwaehrung && fremdwaehrung !== 'EUR';

    return {
      buchung,
      valuta: /^\d{4}-\d{2}-\d{2}$/.test(valuta) ? valuta : buchung,
      auftraggeber: value(record, columns.partner) || 'N26',
      buchungstext: TYPE_LABELS[type] ?? type,
      verwendungszweck: value(record, columns.reference).replace(/\s+/g, ' '),
      betrag,
      saldo: null,
      fremdbetrag: foreign ? number(value(record, columns.fremdbetrag), 'Original Amount') : null,
      fremdwaehrung: foreign ? fremdwaehrung : null,
      devisenkurs: foreign ? number(value(record, columns.devisenkurs), 'Exchange Rate') : null,
      gegenIban: value(record, columns.iban).replace(/\s/g, '') || null,
      art: type || null,
    };
  });
}

function number(value: string, column: string): number | null {
  if (!value) {
    return null;
  }
  const parsed = Number(value);
  if (!Number.isFinite(parsed)) {
    throw new ImportFileError(`Unbekannte Zahl „${value}“ in Spalte ${column}.`);
  }
  return parsed;
}

function germanDate(iso: string): string {
  return iso.split('-').reverse().join('.');
}

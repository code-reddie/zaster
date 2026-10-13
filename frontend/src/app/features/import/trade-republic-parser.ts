import { ImportTransaction } from '../transactions/transaction.models';

/** Erste Spalten der Kopfzeile im Transaktionsexport von Trade Republic. */
export const TRADE_REPUBLIC_HEADER = ['datetime', 'date', 'account_type', 'category', 'type'];

/** Buchungstext je Art der Buchung (Spalte `type`). Unbekannte Arten bleiben, wie sie sind. */
const TYPE_LABELS: Record<string, string> = {
  BUY: 'Kauf',
  SELL: 'Verkauf',
  DIVIDEND: 'Dividende',
  EARNINGS: 'Ertrag',
  INTEREST_PAYMENT: 'Zinsen',
  CUSTOMER_INBOUND: 'Einzahlung',
  CUSTOMER_OUTBOUND_REQUEST: 'Auszahlung',
  TRANSFER_INBOUND: 'Überweisung',
  TRANSFER_INSTANT_INBOUND: 'Echtzeitüberweisung',
  TRANSFER_OUTBOUND: 'Überweisung',
  TRANSFER_INSTANT_OUTBOUND: 'Echtzeitüberweisung',
  TAX_OPTIMIZATION: 'Steuerausgleich',
  EXCHANGE: 'Umtausch',
  GIFT: 'Geschenk',
  STOCKPERK: 'Aktienprämie',
  REFERRAL: 'Empfehlungsprämie',
  SPLIT: 'Aktiensplit',
  REVERSE_SPLIT: 'Zusammenlegung',
  UNBUNDLING: 'Abspaltung',
  CARD_TRANSACTION: 'Kartenzahlung',
  CARD_TRANSACTION_INTERNATIONAL: 'Kartenzahlung',
  CARD_REFUND: 'Kartenerstattung',
  CARD_ORDERING_FEE: 'Kartengebühr',
};

/** Texte, die Trade Republic einsetzt, wenn es keinen Verwendungszweck gibt. */
const EMPTY_DESCRIPTIONS = new Set(['No SEPA description provided']);

/** Fehler in der Datei, die Meldung ist für Nutzer gedacht. */
export class ImportFileError extends Error {}

/**
 * Liest den „Transaktionsexport“ von Trade Republic (App: Service → Kontoauszüge): Komma, Felder in
 * Anführungszeichen, Datum ISO, Zahlen mit Punkt. Er enthält Konto und Depot seit Kontoeröffnung.
 *
 * - Betrag ist die Bewegung auf dem Verrechnungskonto: `amount + fee + tax` (bei Käufen steht die
 *   Ordergebühr nur in `fee`, beim Steuerausgleich steht der ganze Betrag in `tax`). Gebühr und
 *   Steuer werden zusätzlich einzeln übernommen.
 * - Kapitalmaßnahmen ohne Geldbewegung (Split usw.) kommen mit 0 € mit, damit nichts fehlt.
 * - Der Kontostand wird ab 0 € hochgerechnet, weil der Export bei der Kontoeröffnung beginnt.
 * - Jede Spalte landet in einem Feld der Buchung; nur `account_type` (immer `DEFAULT`) und `category`
 *   (folgt aus `type`) werden nicht gespeichert.
 */
export function parseTradeRepublic(records: string[][]): ImportTransaction[] {
  const header = records[0].map((h) => h.trim());
  const rows = records.slice(1).map((record) => {
    const row: Record<string, string> = {};
    header.forEach((name, i) => (row[name] = record[i]?.trim() ?? ''));
    return row;
  });

  for (const row of rows) {
    if (!/^\d{4}-\d{2}-\d{2}$/.test(row['date'] ?? '')) {
      throw new ImportFileError(`Unbekanntes Datum „${row['date']}“ in der Datei.`);
    }
    if (row['currency'] && row['currency'] !== 'EUR') {
      throw new ImportFileError(
        `Die Buchung vom ${germanDate(row['date'])} ist in ${row['currency']}. Zaster übernimmt nur Euro-Buchungen.`,
      );
    }
  }

  // Ältere zuerst, damit sich der Kontostand Buchung für Buchung ergibt.
  const sorted = rows
    .map((row, index) => ({ row, index }))
    .sort(
      (a, b) =>
        a.row['date'].localeCompare(b.row['date']) ||
        a.row['datetime'].localeCompare(b.row['datetime']) ||
        a.index - b.index,
    );

  let saldo = 0;
  return sorted.map(({ row }) => {
    const gebuehr = number(row, 'fee');
    const steuer = number(row, 'tax');
    const betrag = cents((number(row, 'amount') ?? 0) + (gebuehr ?? 0) + (steuer ?? 0));
    saldo = cents(saldo + betrag);

    return {
      buchung: row['date'],
      valuta: row['date'],
      auftraggeber: row['counterparty_name'] || row['name'] || 'Trade Republic',
      buchungstext: TYPE_LABELS[row['type']] ?? row['type'],
      verwendungszweck: purpose(row),
      betrag,
      saldo,
      externeId: row['transaction_id'] || null,
      zeitpunkt: row['datetime'] || null,
      gebuehr,
      steuer,
      isin: row['symbol'] || null,
      stueck: number(row, 'shares'),
      kurs: number(row, 'price'),
      fremdbetrag: number(row, 'original_amount'),
      fremdwaehrung: row['original_currency'] || null,
      devisenkurs: number(row, 'fx_rate'),
      gegenIban: row['counterparty_iban'] || null,
      art: row['type'] || null,
      anlageklasse: row['asset_class'] || null,
      mcc: row['mcc_code'] || null,
    };
  });
}

/** Beschreibung von Trade Republic, bei Wertpapieren ergänzt um Stückzahl und ISIN. */
function purpose(row: Record<string, string>): string {
  const parts: string[] = [];
  const description = row['description'];
  if (description && !EMPTY_DESCRIPTIONS.has(description)) {
    parts.push(description);
  }
  // Steht die ISIN schon in der Beschreibung (Verkäufe, Kapitalmaßnahmen), ist alles gesagt.
  if (row['symbol'] && !description?.includes(row['symbol'])) {
    const shares = number(row, 'shares');
    if (shares !== null) {
      parts.push(`${shareFormat.format(Math.abs(shares))} Stück`);
    }
    parts.push(`ISIN ${row['symbol']}`);
  }
  // Name und Gegenseite sind bei Überweisungen gleich; falls nicht, geht der Name hier mit.
  if (row['counterparty_name'] && row['name'] && row['name'] !== row['counterparty_name']) {
    parts.push(row['name']);
  }
  if (row['payment_reference']) {
    parts.push(row['payment_reference']);
  }
  return parts.join(' · ');
}

const shareFormat = new Intl.NumberFormat('de-DE', { maximumFractionDigits: 10 });

function number(row: Record<string, string>, name: string): number | null {
  const value = row[name];
  if (!value) {
    return null;
  }
  const parsed = Number(value);
  if (!Number.isFinite(parsed)) {
    throw new ImportFileError(`Unbekannte Zahl „${value}“ in Spalte ${name}.`);
  }
  return parsed;
}

/** Auf Cent runden, damit sich Gleitkommafehler beim Addieren nicht aufsummieren. */
function cents(value: number): number {
  return Math.round(value * 100) / 100;
}

function germanDate(iso: string): string {
  return iso.split('-').reverse().join('.');
}

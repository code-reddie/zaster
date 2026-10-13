export type TransactionSource = 'Unbekannt' | 'Manuell' | 'Csv' | 'FinTs';

export interface Transaction extends TransactionDetails {
  id: number;
  accountId: number;
  /** Buchungstag als yyyy-MM-dd. */
  buchung: string;
  valuta: string;
  auftraggeber: string;
  buchungstext: string;
  verwendungszweck: string;
  betrag: number;
  /** Kontostand nach der Buchung laut Bank; null bei Buchungen von Hand und älteren Abrufen. */
  saldo: number | null;
  /** Eigene Bemerkung, z. B. worum es ging, wenn der Verwendungszweck es nicht verrät. */
  bemerkung: string | null;
  /**
   * Kategorien mit Teilbetrag (Vorzeichen wie `betrag`), größter Teil zuerst. Leer = ohne
   * Kategorie; sonst ergeben die Teile zusammen genau `betrag`.
   */
  splits: TransactionSplit[];
  /** Abgehakt. Mit Kategorie immer true. */
  reviewed: boolean;
  source: TransactionSource;
}

/** Höchstlänge der Bemerkung, wie im Backend. */
export const MAX_BEMERKUNG = 500;

export interface TransactionSplit {
  categoryId: number;
  betrag: number;
}

/** Die Kategorie, wenn die Buchung genau eine hat (ganzer Betrag), sonst null. */
export function singleCategoryId(t: Transaction): number | null {
  return t.splits.length === 1 ? t.splits[0].categoryId : null;
}

export interface TransactionPage {
  items: Transaction[];
  total: number;
  einnahmen: number;
  ausgaben: number;
}

export interface TransactionQuery {
  from?: string;
  to?: string;
  search?: string;
  categoryId?: number;
  withoutCategory?: boolean;
  open?: boolean;
  offset?: number;
  limit?: number;
}

export interface CreateTransaction {
  buchung: string;
  valuta: string | null;
  auftraggeber: string;
  buchungstext: string | null;
  verwendungszweck: string | null;
  betrag: number;
  bemerkung: string | null;
}

/** Zusatzangaben, die nur manche Quellen liefern (z. B. der Transaktionsexport von Trade Republic). */
export interface TransactionDetails {
  /** Kennung der Buchung bei der Bank; daran erkennt der Import Duplikate. */
  externeId: string | null;
  /** Genauer Zeitpunkt (ISO, UTC). */
  zeitpunkt: string | null;
  /** Gebühr, schon in `betrag` enthalten (negativ = bezahlt). */
  gebuehr: number | null;
  /** Steuer, schon in `betrag` enthalten (negativ = abgeführt, positiv = erstattet). */
  steuer: number | null;
  isin: string | null;
  /** Stückzahl, negativ = abgegeben. */
  stueck: number | null;
  /** Kurs je Stück in Euro. */
  kurs: number | null;
  fremdbetrag: number | null;
  fremdwaehrung: string | null;
  devisenkurs: number | null;
  gegenIban: string | null;
  /** Art laut Bank, z. B. `BUY` (Trade Republic: Spalte `type`); `buchungstext` ist die deutsche Fassung. */
  art: string | null;
  /** Anlageklasse laut Bank, z. B. `STOCK`, `FUND`, `CRYPTO`. */
  anlageklasse: string | null;
  /** Händlerkategorie bei Kartenzahlungen. */
  mcc: string | null;
}

export interface ImportTransaction extends Partial<TransactionDetails> {
  buchung: string;
  valuta: string;
  auftraggeber: string;
  buchungstext: string;
  verwendungszweck: string;
  betrag: number;
  /** Kontostand nach der Buchung (Spalte „Saldo“ der ING-CSV). */
  saldo: number | null;
}

export interface ImportResult {
  imported: number;
  skipped: number;
}

/** Lückenprüfung über die Kontostände der Bank. */
export interface BalanceCheck {
  /** Letzter bekannter Kontostand laut Bank, am Tag `stand`. */
  kontostand: number | null;
  stand: string | null;
  /** Buchungen mit Kontostand. */
  geprueft: number;
  /** Buchungen von Bank oder CSV ohne Kontostand (aus älteren Abrufen). */
  ohneKontostand: number;
  /** Neueste zuerst. */
  luecken: BalanceGap[];
}

export interface BalanceGap {
  /** Letzter Tag davor mit stimmigem Kontostand, null wenn es keinen gibt. */
  von: string | null;
  bis: string;
  /** Fehlender Betrag (positiv: fehlende Einnahmen); null, wenn es mitten am Tag nicht aufgeht. */
  differenz: number | null;
}

export const SOURCE_LABELS: Record<TransactionSource, string> = {
  Unbekannt: 'Vor dem Umbau übernommen',
  Manuell: 'Von Hand angelegt',
  Csv: 'CSV-Import',
  FinTs: 'Online-Abruf (FinTS)',
};

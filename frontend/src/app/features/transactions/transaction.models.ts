export type TransactionSource = 'Unbekannt' | 'Manuell' | 'Csv' | 'FinTs';

export interface Transaction {
  id: number;
  accountId: number;
  /** Buchungstag als yyyy-MM-dd. */
  buchung: string;
  valuta: string;
  auftraggeber: string;
  buchungstext: string;
  verwendungszweck: string;
  betrag: number;
  /**
   * Kategorien mit Teilbetrag (Vorzeichen wie `betrag`), größter Teil zuerst. Leer = ohne
   * Kategorie; sonst ergeben die Teile zusammen genau `betrag`.
   */
  splits: TransactionSplit[];
  /** Abgehakt. Mit Kategorie immer true. */
  reviewed: boolean;
  source: TransactionSource;
}

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
}

export interface ImportTransaction {
  buchung: string;
  valuta: string;
  auftraggeber: string;
  buchungstext: string;
  verwendungszweck: string;
  betrag: number;
}

export interface ImportResult {
  imported: number;
  skipped: number;
}

export const SOURCE_LABELS: Record<TransactionSource, string> = {
  Unbekannt: 'Vor dem Umbau übernommen',
  Manuell: 'Von Hand angelegt',
  Csv: 'CSV-Import',
  FinTs: 'Online-Abruf (FinTS)',
};

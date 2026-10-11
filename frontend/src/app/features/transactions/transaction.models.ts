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
  categoryId: number | null;
  /** Abgehakt. Mit Kategorie immer true. */
  reviewed: boolean;
  source: TransactionSource;
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

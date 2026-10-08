export interface Transaction {
  id: number;
  buchung: string;
  valuta: string;
  auftragsgeber: string;
  buchungstext: string;
  verwendungszweck: string;
  betrag: number;
  accountId: number;
  categoryId: number | null;
}

export interface CreateTransaction {
  buchung: string;
  valuta: string;
  auftragsgeber: string;
  buchungstext: string;
  verwendungszweck: string | null;
  betrag: number;
  accountId: number;
}

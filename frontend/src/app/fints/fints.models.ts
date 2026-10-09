export interface FinTsTestRequest {
  iban: string;
  userId: string | null;
  pin: string;
  days: number | null;
}

export interface FinTsBankMessage {
  code: string;
  message: string;
}

export interface FinTsTransactionPreview {
  buchung: string;
  valuta: string;
  auftragsgeber: string;
  buchungstext: string;
  verwendungszweck: string;
  betrag: number;
}

export interface FinTsTestResult {
  success: boolean;
  error: string | null;
  messages: FinTsBankMessage[];
  transactions: FinTsTransactionPreview[];
}

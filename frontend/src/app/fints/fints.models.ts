import { Account } from '../account/account.models';
import { Transaction } from '../transaction/transaction.models';

export interface FinTsBankMessage {
  code: string;
  message: string;
}

export interface FinTsSyncRequest {
  userId: string | null;
  pin: string | null;
  savePin: boolean;
}

export interface FinTsSyncResult {
  success: boolean;
  error: string | null;
  messages: FinTsBankMessage[];
  diagnostics: string[];
  imported: number;
  skipped: number;
  transactions: Transaction[];
  account: Account;
}

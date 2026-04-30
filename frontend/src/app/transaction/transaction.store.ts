import { computed, inject } from '@angular/core';
import { patchState, signalStore, withComputed, withMethods, withState } from '@ngrx/signals';
import { CreateTransaction, Transaction } from './transaction.models';
import { TransactionService } from './transaction.service';

type TransactionState = {
  transactions: Transaction[];
  selectedAccountId: number | null;
  loading: boolean;
  error: string | null;
};

const initialState: TransactionState = {
  transactions: [],
  selectedAccountId: null,
  loading: false,
  error: null,
};

export const TransactionStore = signalStore(
  { providedIn: 'root' },
  withState(initialState),
  withComputed(({ transactions, selectedAccountId }) => ({
    selectedAccountTransactions: computed(() =>
      transactions().filter((t) => t.accountId === selectedAccountId()),
    ),
  })),
  withMethods((store, transactionService = inject(TransactionService)) => ({
    selectAccount(accountId: number) {
      patchState(store, { selectedAccountId: accountId });
    },

    async loadAllTransactions() {
      patchState(store, { loading: true, error: null });
      try {
        const transactions = await transactionService.getAllTransactions();
        patchState(store, { transactions });
      } catch {
        patchState(store, { error: 'Buchungen konnten nicht geladen werden.' });
      } finally {
        patchState(store, { loading: false });
      }
    },

    async createTransaction(dto: CreateTransaction) {
      patchState(store, { loading: true, error: null });
      try {
        const transaction = await transactionService.createTransaction(dto);
        patchState(store, { transactions: [...store.transactions(), transaction] });
      } catch {
        patchState(store, { error: 'Buchung konnte nicht erstellt werden.' });
      } finally {
        patchState(store, { loading: false });
      }
    },

    async deleteTransaction(id: number) {
      patchState(store, { loading: true, error: null });
      try {
        await transactionService.deleteTransaction(id);
        patchState(store, { transactions: store.transactions().filter((t) => t.id !== id) });
      } catch {
        patchState(store, { error: 'Buchung konnte nicht gelöscht werden.' });
      } finally {
        patchState(store, { loading: false });
      }
    },
  })),
);

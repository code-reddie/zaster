import { signalStore, withState, withMethods, withComputed, patchState } from '@ngrx/signals';
import { computed, inject } from '@angular/core';
import { AccountService } from './account.service';
import { Account } from './account.models';

type AccountState = {
  accounts: Account[];
  loading: boolean;
  error: string | null;
};

const initialState: AccountState = {
  accounts: [],
  loading: false,
  error: null,
};

export const AccountStore = signalStore(
  {
    providedIn: 'root',
  },
  withState(initialState),
  withComputed(({ accounts }) => ({
    accountCount: computed(() => accounts().length),
  })),
  withMethods((store, accountService = inject(AccountService)) => ({
    async loadAccounts() {
      patchState(store, { loading: true, error: null });
      try {
        const accounts = await accountService.getAccounts();
        patchState(store, { accounts });
      } catch {
        patchState(store, { error: 'Konten konnten nicht geladen werden.' });
      } finally {
        patchState(store, { loading: false });
      }
    },

    async createAccount(name: string) {
      patchState(store, { loading: true, error: null });
      try {
        const account = await accountService.createAccount(name);
        patchState(store, { accounts: [...store.accounts(), account] });
      } catch {
        patchState(store, { error: 'Konto konnte nicht erstellt werden.' });
      } finally {
        patchState(store, { loading: false });
      }
    },

    async deleteAccount(id: number) {
      patchState(store, { loading: true, error: null });
      try {
        await accountService.deleteAccount(id);
        patchState(store, { accounts: store.accounts().filter((a) => a.id !== id) });
      } catch {
        patchState(store, { error: 'Konto konnte nicht gelöscht werden.' });
      } finally {
        patchState(store, { loading: false });
      }
    },
  })),
);

import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AccountStore } from './account.store';

const LAST_ACCOUNT_KEY = 'zaster_last_account';

/** Startseite: zuletzt genutztes Konto, sonst das erste, ohne Konto „Konto hinzufügen“. */
export const startRedirect: CanActivateFn = async () => {
  const store = inject(AccountStore);
  const router = inject(Router);
  const accounts = await store.ensureLoaded();
  if (accounts.length === 0) {
    return router.parseUrl('/konten/neu');
  }
  const last = Number(readLastAccount());
  const account = accounts.find((a) => a.id === last) ?? accounts[0];
  return router.parseUrl(`/konten/${account.id}/buchungen`);
};

/** Setzt das gewählte Konto; unbekannte Konten führen zur Startseite. */
export const accountGuard: CanActivateFn = async (route) => {
  const store = inject(AccountStore);
  const router = inject(Router);
  const id = Number(route.paramMap.get('accountId'));
  const accounts = await store.ensureLoaded();
  if (!accounts.some((a) => a.id === id)) {
    return router.parseUrl('/');
  }
  store.selectedId.set(id);
  try {
    localStorage.setItem(LAST_ACCOUNT_KEY, String(id));
  } catch {
    // Ohne Speicher startet Zaster eben beim ersten Konto.
  }
  return true;
};

function readLastAccount(): string | null {
  try {
    return localStorage.getItem(LAST_ACCOUNT_KEY);
  } catch {
    return null;
  }
}

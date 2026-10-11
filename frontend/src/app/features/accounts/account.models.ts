export interface Account {
  id: number;
  name: string;
  iban: string;
  finTsUserId: string | null;
  hasFinTsPin: boolean;
  /** Uhrzeiten („HH:mm“) für den automatischen Abruf, sortiert. Leer = aus. */
  syncTimes: string[];
  lastSyncedAt: string | null;
  lastSyncError: string | null;
  members: string[];
  /** Buchungen ohne Kategorie, die noch nicht geprüft sind. */
  openTransactions: number;
}

export interface CreateAccount {
  name: string;
  iban: string;
}

export interface UpdateAccount {
  name: string;
  finTsUserId: string | null;
}

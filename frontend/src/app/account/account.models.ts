export interface Account {
  id: number;
  name: string;
  iban: string;
  finTsUserId: string | null;
  hasFinTsPin: boolean;
  lastSyncedAt: string | null;
  lastSyncError: string | null;
}

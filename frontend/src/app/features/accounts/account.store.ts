import { HttpClient } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { Account, CreateAccount, UpdateAccount } from './account.models';

/**
 * Konten des Nutzers und das gerade gewählte Konto (aus der Adresse /konten/:accountId).
 */
@Injectable({ providedIn: 'root' })
export class AccountStore {
  private readonly http = inject(HttpClient);

  readonly accounts = signal<Account[]>([]);
  readonly loaded = signal(false);
  readonly selectedId = signal<number | null>(null);

  readonly selected = computed(
    () => this.accounts().find((a) => a.id === this.selectedId()) ?? null,
  );

  async load(): Promise<Account[]> {
    const accounts = await firstValueFrom(this.http.get<Account[]>('/api/accounts'));
    this.accounts.set(accounts);
    this.loaded.set(true);
    return accounts;
  }

  async ensureLoaded(): Promise<Account[]> {
    return this.loaded() ? this.accounts() : this.load();
  }

  /** Lädt ein Konto neu, z. B. nachdem sich die Zahl der offenen Buchungen geändert hat. */
  async refresh(id: number) {
    this.replace(await firstValueFrom(this.http.get<Account>(`/api/accounts/${id}`)));
  }

  async create(dto: CreateAccount): Promise<Account> {
    const account = await firstValueFrom(this.http.post<Account>('/api/accounts', dto));
    this.accounts.update((a) => [...a, account].sort((x, y) => x.name.localeCompare(y.name)));
    return account;
  }

  async update(id: number, dto: UpdateAccount) {
    this.replace(await firstValueFrom(this.http.put<Account>(`/api/accounts/${id}`, dto)));
  }

  async delete(id: number) {
    await firstValueFrom(this.http.delete(`/api/accounts/${id}`));
    this.accounts.update((a) => a.filter((account) => account.id !== id));
  }

  /** Teilt das Konto mit einem registrierten Nutzer (Anmeldename). */
  async addMember(id: number, name: string) {
    this.replace(
      await firstValueFrom(this.http.post<Account>(`/api/accounts/${id}/members`, { name })),
    );
  }

  async removeMember(id: number, userId: number) {
    await firstValueFrom(this.http.delete(`/api/accounts/${id}/members/${userId}`));
    await this.refresh(id);
  }

  /** Verlässt ein geteiltes Konto; danach ist es aus der eigenen Liste verschwunden. */
  async leave(id: number, userId: number) {
    await firstValueFrom(this.http.delete(`/api/accounts/${id}/members/${userId}`));
    this.accounts.update((a) => a.filter((account) => account.id !== id));
  }

  private replace(account: Account) {
    this.accounts.update((a) => a.map((x) => (x.id === account.id ? account : x)));
  }
}

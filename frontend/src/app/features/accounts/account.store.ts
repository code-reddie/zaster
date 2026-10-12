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
    // Neu laden, damit das Konto an der Stelle steht, an der das Backend es einsortiert.
    await this.load();
    return account;
  }

  async update(id: number, dto: UpdateAccount) {
    this.replace(await firstValueFrom(this.http.put<Account>(`/api/accounts/${id}`, dto)));
  }

  /** Verschiebt ein Konto in der eigenen Reihenfolge um eine Stelle nach oben (-1) oder unten (1). */
  async move(id: number, direction: -1 | 1) {
    const accounts = [...this.accounts()];
    const index = accounts.findIndex((a) => a.id === id);
    const target = index + direction;
    if (index < 0 || target < 0 || target >= accounts.length) {
      return;
    }
    [accounts[index], accounts[target]] = [accounts[target], accounts[index]];
    this.accounts.set(accounts);
    await firstValueFrom(this.http.put('/api/accounts/order', { ids: accounts.map((a) => a.id) }));
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

import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { PageHeader } from '../../core/layout/page-header';
import { errorMessage } from '../../core/errors';
import { Button } from '../../core/ui/button';
import { AccountStore } from './account.store';

@Component({
  imports: [FormsModule, PageHeader, Button],
  template: `
    <z-page-header
      [title]="first() ? 'Willkommen bei Zaster' : 'Konto hinzufügen'"
      [subtitle]="
        first()
          ? 'Leg dein erstes Bankkonto an. Danach kannst du die Umsätze abrufen oder eine CSV-Datei importieren.'
          : 'Jedes Konto hat eigene Buchungen, Kategorien und Regeln.'
      "
    />
    <form class="z-card flex max-w-lg flex-col gap-4 p-5 sm:p-6" (ngSubmit)="save()">
      <div>
        <label class="z-label" for="name">Name</label>
        <input
          id="name"
          name="name"
          class="z-input"
          placeholder="z. B. Girokonto"
          [(ngModel)]="name"
        />
      </div>
      <div>
        <label class="z-label" for="iban">IBAN</label>
        <input
          id="iban"
          name="iban"
          class="z-input font-mono"
          autocapitalize="characters"
          autocomplete="off"
          placeholder="DE00 0000 0000 0000 0000 00"
          [(ngModel)]="iban"
        />
        <p class="z-hint">Für den Online-Abruf per FinTS. Bisher wird nur die ING unterstützt.</p>
      </div>
      @if (error()) {
        <p class="z-error">{{ error() }}</p>
      }
      <div class="flex justify-end gap-2">
        @if (!first()) {
          <button zButton variant="secondary" type="button" (click)="cancel()">Abbrechen</button>
        }
        <button zButton type="submit" [loading]="saving()">Konto anlegen</button>
      </div>
    </form>
  `,
})
export class CreateAccountPage {
  private readonly store = inject(AccountStore);
  private readonly router = inject(Router);

  protected readonly first = computed(
    () => this.store.loaded() && this.store.accounts().length === 0,
  );
  protected readonly name = signal('');
  protected readonly iban = signal('');
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);

  constructor() {
    this.store.selectedId.set(null);
    this.store.ensureLoaded();
  }

  protected cancel() {
    this.router.navigateByUrl('/');
  }

  protected async save() {
    if (!this.name().trim() || !this.iban().trim()) {
      this.error.set('Bitte gib Name und IBAN ein.');
      return;
    }
    this.saving.set(true);
    this.error.set(null);
    try {
      const account = await this.store.create({ name: this.name().trim(), iban: this.iban() });
      this.router.navigate(['/konten', account.id, 'buchungen']);
    } catch (e) {
      this.error.set(errorMessage(e, 'Das Konto konnte nicht angelegt werden.'));
    } finally {
      this.saving.set(false);
    }
  }
}

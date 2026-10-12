import { DialogRef } from '@angular/cdk/dialog';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { errorMessage } from '../../core/errors';
import { Button } from '../../core/ui/button';
import { DialogFooter, DialogFrame } from '../../core/ui/dialog';
import { ToastService } from '../../core/ui/toast';
import { isoDate } from '../../shared/format';
import { AccountStore } from '../accounts/account.store';
import { TransactionsService } from './transactions.service';
import { MAX_BEMERKUNG } from './transaction.models';

/** Buchung von Hand anlegen, z. B. Bargeld. Ergebnis true, wenn angelegt. */
@Component({
  imports: [FormsModule, DialogFrame, DialogFooter, Button],
  template: `
    <z-dialog title="Buchung anlegen" [subtitle]="accounts.selected()?.name">
      <form id="create-transaction" class="flex flex-col gap-4" (ngSubmit)="save()">
        <div class="grid grid-cols-2 gap-1 rounded-xl bg-surface-2 p-1" role="radiogroup">
          <button
            type="button"
            role="radio"
            [attr.aria-checked]="expense()"
            [class]="segment(expense())"
            (click)="expense.set(true)"
          >
            Ausgabe
          </button>
          <button
            type="button"
            role="radio"
            [attr.aria-checked]="!expense()"
            [class]="segment(!expense())"
            (click)="expense.set(false)"
          >
            Einnahme
          </button>
        </div>
        <div class="grid grid-cols-2 gap-3">
          <div>
            <label class="z-label" for="amount">Betrag in €</label>
            <input
              id="amount"
              name="amount"
              class="z-input amount"
              inputmode="decimal"
              placeholder="0,00"
              [(ngModel)]="amount"
            />
          </div>
          <div>
            <label class="z-label" for="date">Datum</label>
            <input id="date" name="date" type="date" class="z-input" [(ngModel)]="date" />
          </div>
        </div>
        <div>
          <label class="z-label" for="party">{{ expense() ? 'Empfänger' : 'Auftraggeber' }}</label>
          <input id="party" name="party" class="z-input" [(ngModel)]="party" />
        </div>
        <div>
          <label class="z-label" for="purpose">Verwendungszweck</label>
          <input id="purpose" name="purpose" class="z-input" [(ngModel)]="purpose" />
        </div>
        <div>
          <label class="z-label" for="note"
            >Bemerkung <span class="text-muted">(optional)</span></label
          >
          <input id="note" name="note" class="z-input" [maxlength]="maxNote" [(ngModel)]="note" />
        </div>
        @if (error()) {
          <p class="z-error">{{ error() }}</p>
        }
      </form>
      <z-dialog-footer>
        <button zButton variant="secondary" type="button" (click)="ref.close()">Abbrechen</button>
        <button zButton type="submit" form="create-transaction" [loading]="saving()">
          Anlegen
        </button>
      </z-dialog-footer>
    </z-dialog>
  `,
})
export class CreateTransactionSheet {
  protected readonly ref = inject<DialogRef<boolean>>(DialogRef);
  protected readonly accounts = inject(AccountStore);
  private readonly service = inject(TransactionsService);
  private readonly toast = inject(ToastService);

  protected readonly expense = signal(true);
  protected readonly amount = signal('');
  protected readonly date = signal(isoDate(new Date()));
  protected readonly party = signal('');
  protected readonly purpose = signal('');
  protected readonly note = signal('');
  protected readonly maxNote = MAX_BEMERKUNG;
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);

  protected segment(active: boolean) {
    return (
      'h-9 rounded-lg text-sm font-medium transition ' +
      (active ? 'bg-surface shadow-card' : 'text-muted hover:text-fg')
    );
  }

  protected async save() {
    const accountId = this.accounts.selectedId();
    const value = parseAmount(this.amount());
    if (value === null || value === 0) {
      this.error.set('Bitte gib einen Betrag ein, z. B. 12,50.');
      return;
    }
    if (!this.party().trim()) {
      this.error.set(
        this.expense() ? 'Bitte gib den Empfänger an.' : 'Bitte gib den Auftraggeber an.',
      );
      return;
    }
    if (accountId === null || !this.date()) {
      this.error.set('Bitte wähle ein Datum.');
      return;
    }

    this.saving.set(true);
    this.error.set(null);
    try {
      await this.service.create(accountId, {
        buchung: this.date(),
        valuta: null,
        auftraggeber: this.party().trim(),
        buchungstext: this.expense() ? 'Ausgabe' : 'Einnahme',
        verwendungszweck: this.purpose().trim() || null,
        betrag: this.expense() ? -Math.abs(value) : Math.abs(value),
        bemerkung: this.note().trim() || null,
      });
      this.toast.success('Buchung angelegt.');
      this.ref.close(true);
    } catch (e) {
      this.error.set(errorMessage(e, 'Die Buchung konnte nicht angelegt werden.'));
    } finally {
      this.saving.set(false);
    }
  }
}

/** „1.234,56“, „1234.56“ oder „12“ → Zahl; null, wenn es keine ist. */
export function parseAmount(value: string): number | null {
  const cleaned = value.trim().replace(/\s|€/g, '');
  if (!cleaned) {
    return null;
  }
  const normalized = cleaned.includes(',') ? cleaned.replace(/\./g, '').replace(',', '.') : cleaned;
  const number = Number(normalized);
  return Number.isFinite(number) ? Math.round(number * 100) / 100 : null;
}

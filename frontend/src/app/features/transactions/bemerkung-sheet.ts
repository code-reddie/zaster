import { DIALOG_DATA, DialogRef } from '@angular/cdk/dialog';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { errorMessage } from '../../core/errors';
import { Button } from '../../core/ui/button';
import { DialogFooter, DialogFrame } from '../../core/ui/dialog';
import { BemerkungInput } from './bemerkung-input';
import { Transaction } from './transaction.models';
import { TransactionsService } from './transactions.service';

/**
 * Eigene Bemerkung zu einer Buchung eintragen, ändern oder (leer) entfernen.
 * Ergebnis: die gespeicherte Buchung oder undefined bei Abbruch.
 */
@Component({
  imports: [FormsModule, DialogFrame, DialogFooter, Button, BemerkungInput],
  template: `
    <z-dialog title="Bemerkung" [subtitle]="transaction.auftraggeber || transaction.buchungstext">
      <form id="bemerkung" (ngSubmit)="save()">
        @if (transaction.verwendungszweck) {
          <p class="mb-3 text-sm break-words text-muted">{{ transaction.verwendungszweck }}</p>
        }
        <z-bemerkung-input [(value)]="value" />
        @if (error()) {
          <p class="z-error mt-3">{{ error() }}</p>
        }
      </form>
      <z-dialog-footer>
        <button zButton variant="secondary" type="button" (click)="ref.close()">Abbrechen</button>
        <button zButton type="submit" form="bemerkung" [loading]="saving()">Speichern</button>
      </z-dialog-footer>
    </z-dialog>
  `,
})
export class BemerkungSheet {
  protected readonly ref = inject<DialogRef<Transaction>>(DialogRef);
  private readonly service = inject(TransactionsService);

  protected readonly transaction = inject<Transaction>(DIALOG_DATA);
  protected readonly value = signal(this.transaction.bemerkung ?? '');
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);

  protected async save() {
    if (this.saving()) {
      return;
    }
    this.saving.set(true);
    this.error.set(null);
    try {
      const updated = await this.service.setBemerkung(
        this.transaction.accountId,
        this.transaction.id,
        this.value().trim(),
      );
      this.ref.close(updated);
    } catch (e) {
      this.error.set(errorMessage(e, 'Die Bemerkung konnte nicht gespeichert werden.'));
    } finally {
      this.saving.set(false);
    }
  }
}

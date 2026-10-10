import { DialogRef } from '@angular/cdk/dialog';
import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { AccountStore } from '../../account/account.store';
import { LoadingButtonDirective } from '../../buttons/loading-button.directive';
import { TransactionStore } from '../../transaction/transaction.store';
import { FinTsSyncResult } from '../fints.models';
import { FinTsService } from '../fints.service';

@Component({
  selector: 'app-fints-sync-dialog',
  imports: [ReactiveFormsModule, LoadingButtonDirective, DatePipe],
  templateUrl: './fints-sync-dialog.component.html',
})
export class FinTsSyncDialog {
  readonly dialogRef = inject(DialogRef<string>);
  private readonly finTsService = inject(FinTsService);
  private readonly transactionStore = inject(TransactionStore);
  readonly accountStore = inject(AccountStore);

  readonly loading = signal(false);
  readonly result = signal<FinTsSyncResult | null>(null);
  readonly error = signal<string | null>(null);

  readonly formGroup = new FormGroup({
    accountId: new FormControl<number | null>(
      this.transactionStore.selectedAccountId() ?? this.accountStore.accounts()[0]?.id ?? null,
    ),
    userId: new FormControl<string>('', { nonNullable: true }),
    pin: new FormControl<string>('', { nonNullable: true }),
    savePin: new FormControl<boolean>(true, { nonNullable: true }),
  });

  private readonly accountId = toSignal(this.formGroup.controls.accountId.valueChanges, {
    initialValue: this.formGroup.controls.accountId.value,
  });

  readonly account = computed(() => {
    const id = this.accountId() ?? this.accountStore.accounts()[0]?.id;
    return this.accountStore.accounts().find((a) => a.id === id) ?? null;
  });

  constructor() {
    if (this.accountStore.accounts().length === 0) {
      this.accountStore.loadAccounts().then(() => {
        if (this.formGroup.controls.accountId.value === null) {
          this.formGroup.controls.accountId.setValue(this.accountStore.accounts()[0]?.id ?? null);
        }
      });
    }
  }

  async onSubmit() {
    const account = this.account();
    if (!account) {
      return;
    }

    const { userId, pin, savePin } = this.formGroup.getRawValue();
    if (!account.hasFinTsPin && !pin) {
      this.error.set('Bitte gib deine PIN ein.');
      return;
    }

    this.loading.set(true);
    this.error.set(null);
    this.result.set(null);

    try {
      const result = await this.finTsService.sync(account.id, {
        userId: userId || null,
        pin: pin || null,
        savePin,
      });
      this.result.set(result);
      this.error.set(result.error);
      this.accountStore.updateAccount(result.account);
      this.transactionStore.addTransactions(result.transactions);
    } catch (e) {
      const message =
        e instanceof HttpErrorResponse && typeof e.error === 'string' ? e.error : null;
      this.error.set(message ?? 'Der Abruf ist fehlgeschlagen.');
    } finally {
      this.formGroup.controls.pin.reset();
      this.loading.set(false);
    }
  }

  async onDeletePin() {
    const account = this.account();
    if (!account) {
      return;
    }

    try {
      this.accountStore.updateAccount(await this.finTsService.deletePin(account.id));
    } catch {
      this.error.set('Die PIN konnte nicht gelöscht werden.');
    }
  }
}

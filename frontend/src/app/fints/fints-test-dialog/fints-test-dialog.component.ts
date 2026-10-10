import { DialogRef } from '@angular/cdk/dialog';
import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { LoadingButtonDirective } from '../../buttons/loading-button.directive';
import { AccountStore } from '../../account/account.store';
import { TransactionStore } from '../../transaction/transaction.store';
import { ImportTransactionsResult } from '../../transaction/transaction.models';
import { FinTsTestResult } from '../fints.models';
import { FinTsService } from '../fints.service';

@Component({
  selector: 'app-fints-test-dialog',
  imports: [ReactiveFormsModule, LoadingButtonDirective, DatePipe],
  templateUrl: './fints-test-dialog.component.html',
})
export class FinTsTestDialog {
  readonly dialogRef = inject(DialogRef<string>);
  private readonly finTsService = inject(FinTsService);
  private readonly transactionStore = inject(TransactionStore);
  readonly accountStore = inject(AccountStore);

  readonly submitted = signal(false);
  readonly loading = signal(false);
  readonly result = signal<FinTsTestResult | null>(null);
  readonly error = signal<string | null>(null);
  readonly importing = signal(false);
  readonly importResult = signal<ImportTransactionsResult | null>(null);
  readonly importError = signal<string | null>(null);

  readonly accountId = new FormControl<number | null>(
    this.transactionStore.selectedAccountId() ?? this.accountStore.accounts()[0]?.id ?? null,
  );

  constructor() {
    if (this.accountStore.accounts().length === 0) {
      this.accountStore.loadAccounts().then(() => {
        if (this.accountId.value === null) {
          this.accountId.setValue(this.accountStore.accounts()[0]?.id ?? null);
        }
      });
    }
  }

  readonly formGroup = new FormGroup({
    iban: new FormControl<string>('', {
      nonNullable: true,
      validators: [Validators.required],
    }),
    userId: new FormControl<string>('', { nonNullable: true }),
    pin: new FormControl<string>('', {
      nonNullable: true,
      validators: [Validators.required],
    }),
  });

  formatBetrag(betrag: number) {
    return betrag.toLocaleString('de-DE', { style: 'currency', currency: 'EUR' });
  }

  async onSubmit() {
    this.submitted.set(true);

    if (this.formGroup.invalid) {
      return;
    }

    const { iban, userId, pin } = this.formGroup.getRawValue();
    this.loading.set(true);
    this.error.set(null);
    this.result.set(null);
    this.importResult.set(null);
    this.importError.set(null);

    try {
      const result = await this.finTsService.test({
        iban,
        userId: userId || null,
        pin,
        days: null,
      });
      this.result.set(result);
      this.error.set(result.error);
    } catch (e) {
      const message =
        e instanceof HttpErrorResponse && typeof e.error === 'string' ? e.error : null;
      this.error.set(message ?? 'Der Abruf ist fehlgeschlagen.');
    } finally {
      this.formGroup.controls.pin.reset();
      this.loading.set(false);
    }
  }

  async onImport() {
    const result = this.result();
    const accountId = this.accountId.value ?? this.accountStore.accounts()[0]?.id;
    if (!result || accountId === undefined) {
      return;
    }

    this.importing.set(true);
    this.importError.set(null);
    try {
      this.importResult.set(
        await this.transactionStore.importTransactions(accountId, result.transactions),
      );
    } catch {
      this.importError.set('Die Buchungen konnten nicht übernommen werden.');
    } finally {
      this.importing.set(false);
    }
  }
}

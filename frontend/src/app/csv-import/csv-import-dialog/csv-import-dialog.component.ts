import { Component, computed, inject, signal } from '@angular/core';
import { DialogRef } from '@angular/cdk/dialog';
import { DatePipe } from '@angular/common';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { CsvImportComponent } from '../csv-import.component';
import { parseIngCsv } from '../ing-csv-parser';
import { AccountStore } from '../../account/account.store';
import { LoadingButtonDirective } from '../../buttons/loading-button.directive';
import { TransactionStore } from '../../transaction/transaction.store';
import { CreateTransaction, ImportTransactionsResult } from '../../transaction/transaction.models';

@Component({
  selector: 'app-csv-import-dialog',
  imports: [CsvImportComponent, ReactiveFormsModule, LoadingButtonDirective, DatePipe],
  templateUrl: './csv-import-dialog.component.html',
})
export class CsvImportDialog {
  readonly dialogRef = inject(DialogRef<string>);
  private readonly transactionStore = inject(TransactionStore);
  readonly accountStore = inject(AccountStore);

  readonly transactions = signal<CreateTransaction[]>([]);
  readonly importing = signal(false);
  readonly importResult = signal<ImportTransactionsResult | null>(null);
  readonly error = signal<string | null>(null);
  readonly hasTransactions = computed(() => this.transactions().length > 0);

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

  formatBetrag(betrag: number) {
    return betrag.toLocaleString('de-DE', { style: 'currency', currency: 'EUR' });
  }

  async onFileSelected(file: File) {
    const content = await file.text();
    this.importResult.set(null);
    this.error.set(null);
    // Das Konto wird erst beim Übernehmen gesetzt.
    const transactions = parseIngCsv(content, 0);
    this.transactions.set(transactions);
    if (transactions.length === 0) {
      this.error.set('In der Datei wurden keine ING-Buchungen gefunden.');
    }
  }

  async onImport() {
    const accountId = this.accountId.value ?? this.accountStore.accounts()[0]?.id;
    if (accountId === undefined || !this.hasTransactions()) {
      return;
    }

    this.importing.set(true);
    this.error.set(null);
    try {
      const transactions = this.transactions().map(({ accountId: _, ...t }) => t);
      this.importResult.set(
        await this.transactionStore.importTransactions(accountId, transactions),
      );
    } catch {
      this.error.set('Die Buchungen konnten nicht übernommen werden.');
    } finally {
      this.importing.set(false);
    }
  }
}

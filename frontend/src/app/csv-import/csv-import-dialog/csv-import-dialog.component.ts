import { Component, inject, signal } from '@angular/core';
import { DialogRef } from '@angular/cdk/dialog';
import { CsvImportComponent } from '../csv-import.component';
import { parseIngCsv } from '../ing-csv-parser';
import { TransactionStore } from '../../transaction/transaction.store';
import { CreateTransaction } from '../../transaction/transaction.models';

@Component({
  selector: 'app-csv-import-dialog',
  imports: [CsvImportComponent],
  templateUrl: './csv-import-dialog.component.html',
})
export class CsvImportDialog {
  readonly dialogRef = inject(DialogRef<string>);
  private readonly transactionStore = inject(TransactionStore);
  readonly transactions = signal<CreateTransaction[]>([]);

  async onFileSelected(file: File) {
    const content = await file.text();
    const accountId = this.transactionStore.selectedAccountId();
    if (accountId) {
      const transactions = parseIngCsv(content, accountId);
      this.transactions.set(transactions);
    }
  }
}

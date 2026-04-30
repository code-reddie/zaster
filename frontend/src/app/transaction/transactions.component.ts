import { Component, computed, inject, OnInit } from '@angular/core';
import { TransactionStore } from './transaction.store';
import { LoadingSpinnerComponent } from '../../assets/loading-spinner/loading-spinner.component';

@Component({
  selector: 'app-transactions',
  imports: [LoadingSpinnerComponent],
  templateUrl: './transactions.component.html',
})
export class TransactionsComponent implements OnInit {
  readonly transactionStore = inject(TransactionStore);

  readonly balance = computed(() =>
    this.transactionStore.transactions().reduce((sum, t) => sum + t.betrag, 0),
  );

  ngOnInit() {
    this.transactionStore.loadAllTransactions();
  }
}

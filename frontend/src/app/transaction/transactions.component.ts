import { Component, computed, inject, OnInit, ChangeDetectionStrategy } from '@angular/core';
import { TransactionStore } from './transaction.store';
import { LoadingSpinnerComponent } from '../../assets/loading-spinner/loading-spinner.component';
import { CategoryStore } from '../category/category.store';

@Component({
  selector: 'app-transactions',
  imports: [LoadingSpinnerComponent],
  changeDetection: ChangeDetectionStrategy.Eager,
  templateUrl: './transactions.component.html',
})
export class TransactionsComponent implements OnInit {
  readonly transactionStore = inject(TransactionStore);
  readonly categoryStore = inject(CategoryStore);

  readonly balance = computed(() =>
    this.transactionStore.transactions().reduce((sum, t) => sum + t.betrag, 0),
  );

  ngOnInit() {
    this.transactionStore.loadAllTransactions();
    this.categoryStore.loadCategories();
  }

  onCategoryChange(transactionId: number, event: Event) {
    const value = (event.target as HTMLSelectElement).value;
    this.transactionStore.setCategory(transactionId, value === '' ? null : Number(value));
  }
}

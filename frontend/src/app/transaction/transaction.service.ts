import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import {
  CreateTransaction,
  ImportTransaction,
  ImportTransactionsResult,
  Transaction,
} from './transaction.models';

@Injectable({ providedIn: 'root' })
export class TransactionService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/transaction';

  createTransaction(transaction: CreateTransaction) {
    return firstValueFrom(this.http.post<Transaction>(this.baseUrl, transaction));
  }

  importTransactions(accountId: number, transactions: ImportTransaction[]) {
    return firstValueFrom(
      this.http.post<ImportTransactionsResult>(`${this.baseUrl}/import`, {
        accountId,
        transactions,
      }),
    );
  }

  getAllTransactions() {
    return firstValueFrom(this.http.get<Transaction[]>(this.baseUrl));
  }

  setCategory(id: number, categoryId: number | null) {
    return firstValueFrom(
      this.http.put<Transaction>(`${this.baseUrl}/${id}/category`, { categoryId }),
    );
  }

  deleteTransaction(id: number) {
    return firstValueFrom(this.http.delete(`${this.baseUrl}/${id}`));
  }
}

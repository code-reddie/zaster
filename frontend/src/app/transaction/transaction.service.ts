import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { CreateTransaction, Transaction } from './transaction.models';

@Injectable({ providedIn: 'root' })
export class TransactionService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/transaction';

  createTransaction(transaction: CreateTransaction) {
    return firstValueFrom(this.http.post<Transaction>(this.baseUrl, transaction));
  }

  getAllTransactions() {
    return firstValueFrom(this.http.get<Transaction[]>(this.baseUrl));
  }

  deleteTransaction(id: number) {
    return firstValueFrom(this.http.delete(`${this.baseUrl}/${id}`));
  }
}

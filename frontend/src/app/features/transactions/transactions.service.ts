import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import {
  CreateTransaction,
  ImportResult,
  ImportTransaction,
  Transaction,
  TransactionPage,
  TransactionQuery,
  TransactionSplit,
} from './transaction.models';

@Injectable({ providedIn: 'root' })
export class TransactionsService {
  private readonly http = inject(HttpClient);

  list(accountId: number, query: TransactionQuery): Promise<TransactionPage> {
    let params = new HttpParams();
    for (const [key, value] of Object.entries(query)) {
      if (value !== undefined && value !== null && value !== '' && value !== false) {
        params = params.set(key, String(value));
      }
    }
    return firstValueFrom(this.http.get<TransactionPage>(this.url(accountId), { params }));
  }

  create(accountId: number, dto: CreateTransaction): Promise<Transaction> {
    return firstValueFrom(this.http.post<Transaction>(this.url(accountId), dto));
  }

  import(accountId: number, transactions: ImportTransaction[]): Promise<ImportResult> {
    return firstValueFrom(
      this.http.post<ImportResult>(`${this.url(accountId)}/import`, { transactions }),
    );
  }

  setCategory(accountId: number, id: number, categoryId: number | null): Promise<Transaction> {
    return firstValueFrom(
      this.http.put<Transaction>(`${this.url(accountId)}/${id}/category`, { categoryId }),
    );
  }

  /** Teilt die Buchung auf; die Teile müssen zusammen den Buchungsbetrag ergeben. */
  setSplits(accountId: number, id: number, splits: TransactionSplit[]): Promise<Transaction> {
    return firstValueFrom(
      this.http.put<Transaction>(`${this.url(accountId)}/${id}/splits`, { splits }),
    );
  }

  setReviewed(accountId: number, id: number, reviewed: boolean): Promise<Transaction> {
    return firstValueFrom(
      this.http.put<Transaction>(`${this.url(accountId)}/${id}/reviewed`, { reviewed }),
    );
  }

  /** Hakt die angegebenen Buchungen ab, ohne Ids alle offenen des Kontos. */
  async review(accountId: number, ids?: number[]): Promise<number> {
    const result = await firstValueFrom(
      this.http.post<{ updated: number }>(`${this.url(accountId)}/review`, { ids: ids ?? null }),
    );
    return result.updated;
  }

  delete(accountId: number, id: number): Promise<unknown> {
    return firstValueFrom(this.http.delete(`${this.url(accountId)}/${id}`));
  }

  private url(accountId: number) {
    return `/api/accounts/${accountId}/transactions`;
  }
}

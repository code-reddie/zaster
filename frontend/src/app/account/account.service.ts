import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Account } from './account.models';

@Injectable({
  providedIn: 'root',
})
export class AccountService {
  private http = inject(HttpClient);

  createAccount(accountName: string) {
    return this.http.post<Account>('/api/account', { name: accountName });
  }

  getAccounts() {
    return this.http.get<Account[]>('/api/account');
  }
}

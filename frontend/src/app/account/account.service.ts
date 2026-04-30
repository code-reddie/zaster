import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Account } from './account.models';
import { firstValueFrom } from 'rxjs';

@Injectable({
  providedIn: 'root',
})
export class AccountService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/account';

  createAccount(name: string) {
    return firstValueFrom(this.http.post<Account>(this.baseUrl, { name }));
  }

  getAccounts() {
    return firstValueFrom(this.http.get<Account[]>(this.baseUrl));
  }

  deleteAccount(id: number) {
    return firstValueFrom(this.http.delete(`${this.baseUrl}/${id}`));
  }
}

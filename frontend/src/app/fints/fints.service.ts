import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { Account } from '../account/account.models';
import { FinTsSyncRequest, FinTsSyncResult } from './fints.models';

@Injectable({ providedIn: 'root' })
export class FinTsService {
  private readonly http = inject(HttpClient);

  sync(accountId: number, request: FinTsSyncRequest) {
    return firstValueFrom(
      this.http.post<FinTsSyncResult>(`/api/account/${accountId}/fints/sync`, request),
    );
  }

  deletePin(accountId: number) {
    return firstValueFrom(this.http.delete<Account>(`/api/account/${accountId}/fints/pin`));
  }
}

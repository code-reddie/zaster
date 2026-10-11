import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { firstValueFrom } from 'rxjs';

export interface FinTsBankMessage {
  code: string;
  message: string;
}

export interface FinTsSyncRequest {
  userId: string | null;
  pin: string | null;
  savePin: boolean;
}

export interface FinTsSyncResult {
  success: boolean;
  error: string | null;
  messages: FinTsBankMessage[];
  diagnostics: string[];
  imported: number;
  skipped: number;
}

@Injectable({ providedIn: 'root' })
export class FinTsService {
  private readonly http = inject(HttpClient);

  sync(accountId: number, request: FinTsSyncRequest): Promise<FinTsSyncResult> {
    return firstValueFrom(
      this.http.post<FinTsSyncResult>(`/api/accounts/${accountId}/fints/sync`, request),
    );
  }

  updateSyncTimes(accountId: number, times: string[]): Promise<string[]> {
    return firstValueFrom(
      this.http.put<string[]>(`/api/accounts/${accountId}/fints/times`, { times }),
    );
  }

  deletePin(accountId: number): Promise<unknown> {
    return firstValueFrom(this.http.delete(`/api/accounts/${accountId}/fints/pin`));
  }
}

import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { FinTsTestRequest, FinTsTestResult } from './fints.models';

@Injectable({ providedIn: 'root' })
export class FinTsService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api/fints';

  test(request: FinTsTestRequest) {
    return firstValueFrom(this.http.post<FinTsTestResult>(`${this.baseUrl}/test`, request));
  }
}

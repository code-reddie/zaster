import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { catchError, map, Observable } from 'rxjs';

export interface HelloResponse {
  message: string;
}

@Injectable({
  providedIn: 'root',
})
export class ApiService {
  private http = inject(HttpClient);
  private apiUrl = '/api/hello';
  
  getHello(): Observable<string> {
    console.log('API-Request: GET', this.apiUrl);
    return this.http.get<HelloResponse>(this.apiUrl).pipe(
      map(response => response.message),
      catchError(error => {
        console.error('API-Error:', error);
        throw error;
      })
    );
  }
}

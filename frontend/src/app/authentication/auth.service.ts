import { HttpClient } from '@angular/common/http';
import { inject, Injectable, signal } from '@angular/core';
import { tap } from 'rxjs';
import { AuthResponse, LoginCredentials, RegisterCredentials } from './auth.models';

@Injectable({
  providedIn: 'root',
})
export class AuthService {
  private http = inject(HttpClient);
  private readonly TOKEN_KEY = 'zaster_auth_token';

  currentUser = signal<string | null>(localStorage.getItem('user_name'));

  register(credentials: RegisterCredentials) {
    return this.http.post<AuthResponse>('/api/auth/register', credentials).pipe(
      tap((response) => {
        localStorage.setItem(this.TOKEN_KEY, response.token);
        localStorage.setItem('user_name', response.userName);

        this.currentUser.set(response.userName);
      }),
    );
  }

  login(credentials: LoginCredentials) {
    return this.http.post<AuthResponse>('/api/auth/login', credentials).pipe(
      tap((response) => {
        localStorage.setItem(this.TOKEN_KEY, response.token);
        localStorage.setItem('user_name', response.userName);

        this.currentUser.set(response.userName);
      }),
    );
  }

  logout() {
    localStorage.removeItem(this.TOKEN_KEY);
    localStorage.removeItem('user_name');
    this.currentUser.set(null);
  }

  isLoggedIn(): boolean {
    return !!localStorage.getItem(this.TOKEN_KEY);
  }

  getToken(): string | null {
    return localStorage.getItem(this.TOKEN_KEY);
  }
}

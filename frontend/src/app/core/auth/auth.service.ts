import { HttpClient } from '@angular/common/http';
import { inject, Injectable, signal } from '@angular/core';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';

export interface Credentials {
  name: string;
  password: string;
}

interface AuthResponse {
  userId: number;
  userName: string;
  token: string;
}

const TOKEN_KEY = 'zaster_auth_token';
const USER_KEY = 'zaster_user_name';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  readonly userName = signal<string | null>(read(USER_KEY));

  get token(): string | null {
    return read(TOKEN_KEY);
  }

  isLoggedIn(): boolean {
    return this.token !== null;
  }

  async login(credentials: Credentials) {
    this.store(await firstValueFrom(this.http.post<AuthResponse>('/api/auth/login', credentials)));
  }

  async register(credentials: Credentials) {
    this.store(
      await firstValueFrom(this.http.post<AuthResponse>('/api/auth/register', credentials)),
    );
  }

  logout() {
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(USER_KEY);
    this.userName.set(null);
    // Neu laden statt navigieren, damit keine Daten des alten Nutzers im Speicher bleiben.
    window.location.assign('/anmelden');
  }

  private store(response: AuthResponse) {
    localStorage.setItem(TOKEN_KEY, response.token);
    localStorage.setItem(USER_KEY, response.userName);
    this.userName.set(response.userName);
    this.router.navigateByUrl('/');
  }
}

function read(key: string): string | null {
  try {
    return localStorage.getItem(key);
  } catch {
    return null;
  }
}

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

  /** @param returnTo Seite, die nach der nächsten Anmeldung wieder geöffnet wird. */
  logout(returnTo?: string) {
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(USER_KEY);
    this.userName.set(null);
    // Neu laden statt navigieren, damit keine Daten des alten Nutzers im Speicher bleiben.
    const target = returnTo && isInternal(returnTo) && returnTo !== '/' ? returnTo : null;
    window.location.assign(target ? `/anmelden?weiter=${encodeURIComponent(target)}` : '/anmelden');
  }

  private store(response: AuthResponse) {
    localStorage.setItem(TOKEN_KEY, response.token);
    localStorage.setItem(USER_KEY, response.userName);
    this.userName.set(response.userName);
    const target = this.router.parseUrl(this.router.url).queryParams['weiter'];
    this.router.navigateByUrl(typeof target === 'string' && isInternal(target) ? target : '/');
  }
}

/** Nur Pfade innerhalb von Zaster, damit ein Link nicht nach der Anmeldung auf eine fremde Seite führt. */
function isInternal(url: string): boolean {
  return url.startsWith('/') && !url.startsWith('//') && !url.startsWith('/\\');
}

function read(key: string): string | null {
  try {
    return localStorage.getItem(key);
  } catch {
    return null;
  }
}

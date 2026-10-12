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

/** Ab diesem Alter holt sich die App beim nächsten Aufruf einen neuen Token (er gilt 7 Tage). */
const REFRESH_AFTER_MS = 24 * 60 * 60 * 1000;

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  readonly userName = signal<string | null>(read(USER_KEY));

  private refreshing = false;

  get token(): string | null {
    return read(TOKEN_KEY);
  }

  isLoggedIn(): boolean {
    const token = this.token;
    if (token === null) return false;
    const expires = claims(token)?.exp;
    return expires === undefined || expires * 1000 > Date.now();
  }

  /**
   * Holt im Hintergrund einen neuen Token, wenn der aktuelle älter als einen Tag ist.
   * So bleibt angemeldet, wer Zaster mindestens einmal pro Woche öffnet.
   */
  refreshIfDue() {
    const token = this.token;
    const issued = token ? claims(token)?.iat : undefined;
    if (this.refreshing || !this.isLoggedIn() || issued === undefined) return;
    if (Date.now() - issued * 1000 < REFRESH_AFTER_MS) return;

    this.refreshing = true;
    this.http.post<AuthResponse>('/api/auth/refresh', null).subscribe({
      next: (response) => {
        // Inzwischen abgemeldet oder als jemand anderes angemeldet: nichts überschreiben.
        if (this.token === token) {
          localStorage.setItem(TOKEN_KEY, response.token);
          localStorage.setItem(USER_KEY, response.userName);
          this.userName.set(response.userName);
        }
        this.refreshing = false;
      },
      error: () => (this.refreshing = false),
    });
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

/** Liest die Zeitangaben aus dem Token (ohne Prüfung der Signatur, das macht das Backend). */
function claims(token: string): { exp?: number; iat?: number } | null {
  try {
    const payload = token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/');
    return JSON.parse(atob(payload));
  } catch {
    return null;
  }
}

function read(key: string): string | null {
  try {
    return localStorage.getItem(key);
  } catch {
    return null;
  }
}

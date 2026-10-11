import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';

/** Ohne Anmeldung geht es zur Anmeldung und danach zurück, z. B. nach dem Tipp auf eine Benachrichtigung. */
export const authGuard: CanActivateFn = (_route, state) =>
  inject(AuthService).isLoggedIn() ||
  inject(Router).createUrlTree(['/anmelden'], loginTarget(state.url));

function loginTarget(url: string) {
  return url && url !== '/' ? { queryParams: { weiter: url } } : {};
}

export const guestGuard: CanActivateFn = () =>
  !inject(AuthService).isLoggedIn() || inject(Router).parseUrl('/');

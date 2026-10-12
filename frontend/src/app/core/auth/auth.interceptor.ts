import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { AuthService } from './auth.service';

const PUBLIC_URLS = ['/api/auth/login', '/api/auth/register'];

/**
 * Hängt den Login-Token an jede Anfrage und erneuert ihn bei Bedarf im Hintergrund.
 * Ist er abgelaufen (401 außerhalb von Anmeldung und Registrierung),
 * geht es zurück zur Anmeldung und danach wieder auf dieselbe Seite.
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthService);
  const token = auth.token;
  if (token && req.url !== '/api/auth/refresh') auth.refreshIfDue();
  const request = token ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : req;

  return next(request).pipe(
    catchError((error: unknown) => {
      if (
        error instanceof HttpErrorResponse &&
        error.status === 401 &&
        !PUBLIC_URLS.includes(req.url)
      ) {
        auth.logout(location.pathname + location.search);
      }
      return throwError(() => error);
    }),
  );
};

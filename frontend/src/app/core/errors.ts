import { HttpErrorResponse } from '@angular/common/http';

/**
 * Meldung für den Nutzer aus einer fehlgeschlagenen Anfrage. Das Backend schickt
 * ProblemDetails mit deutscher Meldung in `detail`.
 */
export function errorMessage(error: unknown, fallback: string): string {
  if (error instanceof HttpErrorResponse) {
    if (error.status === 0) {
      return 'Zaster ist gerade nicht erreichbar. Bitte prüfe die Verbindung.';
    }
    const body = error.error;
    if (body && typeof body === 'object' && typeof body.detail === 'string') {
      return body.detail;
    }
    if (typeof body === 'string' && body.length > 0 && body.length < 300) {
      return body;
    }
  }
  return fallback;
}

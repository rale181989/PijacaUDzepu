import { HttpErrorResponse } from '@angular/common/http';
import { environment } from '../../environments/environment';

// Tekst za prikaz kad API nije dostupan; pomaže da se uzrok vidi i u mobilnoj aplikaciji bez kabla
export function describeConnectionError(err: unknown): string {
  const server = environment.apiUrl.replace(/\/api$/, '');
  if (err instanceof HttpErrorResponse) {
    const reason = err.status === 0 ? 'zahtev nije stigao do servera (mreža, CORS ili SSL)' : `HTTP ${err.status}`;
    return `${server} – ${reason}`;
  }
  return `${server} – ${String(err)}`;
}

export function isConnectionError(err: unknown): boolean {
  return err instanceof HttpErrorResponse && (err.status === 0 || err.status >= 502);
}

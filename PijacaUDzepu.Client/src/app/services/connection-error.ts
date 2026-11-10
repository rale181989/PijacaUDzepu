import { HttpErrorResponse } from '@angular/common/http';
import { environment } from '../../environments/environment';

type Translate = (key: string) => string;

// Opis greške za korisnika, na njegovom jeziku. Adresa servera se dodaje samo u mobilnoj verziji,
// gde se server bira na ekranu za prijavu (pomaže da se uzrok vidi i bez kabla).
export function describeConnectionError(err: unknown, t: Translate): string {
  let reason: string;
  if (err instanceof HttpErrorResponse && err.status === 0) reason = t('error.unreachable');
  else if (err instanceof HttpErrorResponse && err.status >= 502 && err.status <= 504) reason = t('error.starting');
  else reason = t('error.generic');
  const showServer = (environment as { serverSelectable?: boolean }).serverSelectable;
  return showServer ? `${environment.apiUrl.replace(/\/api$/, '')} – ${reason}` : reason;
}

// Veza nije uspostavljena ili server još nije spreman (Render se budi): "Nema veze sa serverom"
export function isConnectionError(err: unknown): boolean {
  return err instanceof HttpErrorResponse && (err.status === 0 || (err.status >= 502 && err.status <= 504));
}

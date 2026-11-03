import { Injectable } from '@angular/core';
import { HttpInterceptor, HttpRequest, HttpHandler, HttpEvent, HttpErrorResponse } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { Router } from '@angular/router';
import { AuthService } from '../services/auth.service';
import { TranslateService } from '../services/translate.service';

@Injectable()
export class TokenInterceptor implements HttpInterceptor {
  constructor(private authService: AuthService, private router: Router, private ts: TranslateService) {}

  intercept(req: HttpRequest<any>, next: HttpHandler): Observable<HttpEvent<any>> {
    const token = this.authService.token;

    // Server vraća poruke (greške, potvrde) na jeziku aplikacije
    const headers: Record<string, string> = { 'Accept-Language': this.ts.lang };
    if (token) headers['Authorization'] = `Bearer ${token}`;
    req = req.clone({ setHeaders: headers });

    return next.handle(req).pipe(
      catchError((error: HttpErrorResponse) => {
        if (error.status === 401) {
          this.authService.logout();
          this.router.navigateByUrl('/login');
        }
        return throwError(() => this.withMessage(error));
      })
    );
  }

  // Bez veze sa serverom, previše zahteva ili greška servera: stranice prikazuju err.error.message,
  // pa se ovde dopunjuje poruka na jeziku aplikacije kada je server nije poslao
  private withMessage(error: HttpErrorResponse): HttpErrorResponse {
    if (error.error?.message || !(error.status === 0 || error.status === 429 || error.status >= 500)) return error;
    const key = error.status === 0 ? 'error.network' : error.status === 429 ? 'error.tooMany' : 'error.generic';
    const body = error.error && typeof error.error === 'object' ? error.error : {};
    return new HttpErrorResponse({
      error: { ...body, message: this.ts.t(key) },
      headers: error.headers, status: error.status, statusText: error.statusText, url: error.url ?? undefined
    });
  }
}

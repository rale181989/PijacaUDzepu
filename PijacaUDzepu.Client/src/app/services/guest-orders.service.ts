import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';
import { environment } from '../../environments/environment';

const STORAGE_KEY = 'guestOrders';
const MAX_CODES = 20;

// Kodovi za praćenje gostujućih kupovina sa ovog uređaja, da gost vidi "Moje porudžbine" bez naloga
@Injectable({ providedIn: 'root' })
export class GuestOrdersService {
  private codesSubject = new BehaviorSubject<string[]>(this.read());
  codes$ = this.codesSubject.asObservable();

  get codes(): string[] {
    return this.codesSubject.value;
  }

  add(code: string) {
    const codes = [code, ...this.codes.filter(c => c !== code)].slice(0, MAX_CODES);
    this.write(codes);
  }

  remove(code: string) {
    this.write(this.codes.filter(c => c !== code));
  }

  trackingUrl(code: string): string {
    // U mobilnoj aplikaciji origin je https://localhost, pa se deli javna adresa sajta
    const base = (environment as { publicUrl?: string }).publicUrl || window.location.origin;
    return `${base}/prati/${code}`;
  }

  private read(): string[] {
    try {
      const parsed = JSON.parse(localStorage.getItem(STORAGE_KEY) ?? '[]');
      return Array.isArray(parsed) ? parsed.filter(c => typeof c === 'string') : [];
    } catch {
      return [];
    }
  }

  private write(codes: string[]) {
    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(codes));
    } catch {}
    this.codesSubject.next(codes);
  }
}

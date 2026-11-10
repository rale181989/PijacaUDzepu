import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';
import { environment } from '../../environments/environment';

const STORAGE_KEY = 'guestOrders';
const MAX_CODES = 20;
const ADDRESS_KEY = 'guestOrderAddresses';

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

  // Posle prelaska na nalog porudžbine se vide u "Moje porudžbine", pa uređaj više ne pamti kodove
  clear() {
    this.write([]);
    try { localStorage.removeItem(ADDRESS_KEY); } catch {}
  }

  // Server na stranici za praćenje skriva deo adrese (link može da dođe do drugih); ovaj uređaj je poručio,
  // pa pamti punu adresu da kupac proveri da li je dobro upisao broj
  rememberAddress(code: string, address: string) {
    try {
      const map = JSON.parse(localStorage.getItem(ADDRESS_KEY) ?? '{}');
      map[code] = address;
      localStorage.setItem(ADDRESS_KEY, JSON.stringify(map));
    } catch {}
  }

  addressFor(code: string): string | null {
    try {
      return JSON.parse(localStorage.getItem(ADDRESS_KEY) ?? '{}')[code] ?? null;
    } catch {
      return null;
    }
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

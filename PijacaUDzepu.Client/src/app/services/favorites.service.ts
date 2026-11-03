import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject } from 'rxjs';
import { AuthService } from './auth.service';
import { environment } from '../../environments/environment';

interface FavoritesState {
  productIds: number[];
  vendorIds: number[];
  alertProductIds: number[];
}

const STORAGE_KEY = 'favorites';

// Omiljeni proizvodi (srce na kartici). Gost ih čuva na uređaju (localStorage), prijavljeni korisnik na nalogu;
// server podržava i omiljene prodavce, ali ih aplikacija više ne nudi (dva srca na kartici su zbunjivala).
// pri prijavi se omiljeni sa uređaja prenose na nalog. "Obavesti me kad bude dostupno" radi samo uz nalog.
@Injectable({ providedIn: 'root' })
export class FavoritesService {
  private baseUrl = environment.apiUrl + '/favorites';
  private state = new BehaviorSubject<FavoritesState>(this.readLocal());
  changes$ = this.state.asObservable();

  constructor(private http: HttpClient, private authService: AuthService) {
    this.authService.currentUser$.subscribe(user => {
      if (user) this.loadFromServer();
      else this.state.next(this.readLocal());
    });
  }

  get productIds(): number[] { return this.state.value.productIds; }
  get vendorIds(): number[] { return this.state.value.vendorIds; }
  get isEmpty(): boolean { return this.productIds.length === 0; }

  isProduct(id: number): boolean { return this.state.value.productIds.includes(id); }
  hasAlert(id: number): boolean { return this.state.value.alertProductIds.includes(id); }

  toggleProduct(id: number) {
    const on = !this.isProduct(id);
    this.update({ productIds: toggle(this.productIds, id, on) });
    if (this.authService.isLoggedIn) this.call(on, `${this.baseUrl}/products/${id}`);
  }

  // Vraća false kada korisnik nije prijavljen (obaveštenje traži nalog)
  toggleAlert(id: number): boolean {
    if (!this.authService.isLoggedIn) return false;
    const on = !this.hasAlert(id);
    this.update({ alertProductIds: toggle(this.state.value.alertProductIds, id, on) });
    this.call(on, `${this.baseUrl}/alerts/${id}`);
    return true;
  }

  // Kada stigne obaveštenje da je proizvod ponovo dostupan, server je već obrisao zahtev
  clearAlert(id: number) {
    this.update({ alertProductIds: toggle(this.state.value.alertProductIds, id, false) });
  }

  private loadFromServer() {
    const local = this.readLocal();
    const request = local.productIds.length || local.vendorIds.length
      ? this.http.post<FavoritesState>(`${this.baseUrl}/sync`, { productIds: local.productIds, vendorIds: local.vendorIds })
      : this.http.get<FavoritesState>(this.baseUrl);
    request.subscribe({
      next: s => {
        this.writeLocal({ productIds: [], vendorIds: [], alertProductIds: [] });
        this.state.next(s);
      },
      error: () => {}
    });
  }

  private call(on: boolean, url: string) {
    (on ? this.http.put(url, {}) : this.http.delete(url)).subscribe({ error: () => {} });
  }

  private update(patch: Partial<FavoritesState>) {
    const next = { ...this.state.value, ...patch };
    this.state.next(next);
    if (!this.authService.isLoggedIn) this.writeLocal(next);
  }

  private readLocal(): FavoritesState {
    try {
      const s = JSON.parse(localStorage.getItem(STORAGE_KEY) || '{}');
      return { productIds: s.productIds ?? [], vendorIds: s.vendorIds ?? [], alertProductIds: [] };
    } catch {
      return { productIds: [], vendorIds: [], alertProductIds: [] };
    }
  }

  private writeLocal(s: FavoritesState) {
    try { localStorage.setItem(STORAGE_KEY, JSON.stringify({ productIds: s.productIds, vendorIds: s.vendorIds })); } catch {}
  }
}

function toggle(list: number[], id: number, on: boolean): number[] {
  return on ? (list.includes(id) ? list : [...list, id]) : list.filter(x => x !== id);
}

import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../environments/environment';
import { Market, MarketInput } from '../models/market.model';

@Injectable({ providedIn: 'root' })
export class MarketService {
  private baseUrl = environment.apiUrl + '/markets';

  constructor(private http: HttpClient) {}

  getAll() {
    return this.http.get<Market[]>(this.baseUrl);
  }

  getAllIncludingInactive() {
    return this.http.get<Market[]>(`${this.baseUrl}/all`);
  }

  create(dto: MarketInput) {
    return this.http.post<Market>(this.baseUrl, dto);
  }

  update(id: number, dto: MarketInput) {
    return this.http.put<Market>(`${this.baseUrl}/${id}`, dto);
  }

  toggleActive(id: number) {
    return this.http.put(`${this.baseUrl}/${id}/toggle-active`, {});
  }
}

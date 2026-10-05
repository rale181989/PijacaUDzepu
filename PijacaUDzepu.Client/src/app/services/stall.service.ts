import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../environments/environment';
import { Stall, StallInput } from '../models/stall.model';

@Injectable({ providedIn: 'root' })
export class StallService {
  private baseUrl = environment.apiUrl + '/stalls';

  constructor(private http: HttpClient) {}

  getByMarket(marketId: number) {
    return this.http.get<Stall[]>(`${this.baseUrl}/by-market/${marketId}`);
  }

  getAll() {
    return this.http.get<Stall[]>(this.baseUrl);
  }

  create(dto: StallInput) {
    return this.http.post<Stall>(this.baseUrl, dto);
  }

  update(id: number, dto: StallInput) {
    return this.http.put<Stall>(`${this.baseUrl}/${id}`, dto);
  }

  delete(id: number) {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }
}

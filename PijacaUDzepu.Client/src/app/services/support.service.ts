import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { environment } from '../../environments/environment';

export type SupportCategory = 'Problem' | 'Question' | 'Account' | 'Other';

export interface SupportRequestInput {
  name: string;
  email: string;
  category: SupportCategory;
  message: string;
  platform?: string;
  userAgent?: string;
  website?: string;
}

export interface SupportRequest {
  id: number;
  userId?: number;
  userName?: string;
  name: string;
  email: string;
  category: SupportCategory;
  message: string;
  platform?: string;
  userAgent?: string;
  isResolved: boolean;
  resolvedAt?: string;
  createdAt: string;
}

@Injectable({ providedIn: 'root' })
export class SupportService {
  private baseUrl = environment.apiUrl + '/support';

  constructor(private http: HttpClient) {}

  send(dto: SupportRequestInput) {
    return this.http.post<void>(this.baseUrl, dto);
  }

  getAll(resolved?: boolean) {
    let params = new HttpParams();
    if (resolved !== undefined) params = params.set('resolved', String(resolved));
    return this.http.get<SupportRequest[]>(this.baseUrl, { params });
  }

  getOpenCount() {
    return this.http.get<{ count: number }>(`${this.baseUrl}/open-count`);
  }

  setResolved(id: number, value: boolean) {
    return this.http.put<SupportRequest>(`${this.baseUrl}/${id}/resolved`, {}, { params: { value: String(value) } });
  }
}

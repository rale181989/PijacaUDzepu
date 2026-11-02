import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { environment } from '../../environments/environment';
import { User, CreateCourierDto } from '../models/user.model';
import { PagedResult } from '../models/paged-result.model';

@Injectable({ providedIn: 'root' })
export class UserService {
  private baseUrl = environment.apiUrl + '/users';

  constructor(private http: HttpClient) {}

  // Strana korisnika jedne uloge, uz pretragu po imenu, korisničkom imenu, e-mailu i telefonu
  getPage(role: string, search: string, skip = 0, take = 20) {
    let params = new HttpParams().set('role', role).set('skip', String(skip)).set('take', String(take));
    if (search.trim()) params = params.set('search', search.trim());
    return this.http.get<PagedResult<User>>(this.baseUrl, { params });
  }

  createCourier(dto: CreateCourierDto) {
    return this.http.post<User>(environment.apiUrl + '/auth/create-courier', dto);
  }

  // Admin postavlja privremenu lozinku (kurir/prodavac bez e-maila); vraća je jednom
  resetPassword(id: number) {
    return this.http.post<{ password: string }>(`${this.baseUrl}/${id}/reset-password`, {});
  }

  toggleActive(id: number) {
    return this.http.put<void>(`${this.baseUrl}/${id}/toggle-active`, {});
  }
}

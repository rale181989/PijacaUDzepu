import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../environments/environment';
import { User, CreateCourierDto } from '../models/user.model';

@Injectable({ providedIn: 'root' })
export class UserService {
  private baseUrl = environment.apiUrl + '/users';

  constructor(private http: HttpClient) {}

  getAll() {
    return this.http.get<User[]>(this.baseUrl);
  }

  createCourier(dto: CreateCourierDto) {
    return this.http.post<User>(environment.apiUrl + '/auth/create-courier', dto);
  }

  toggleActive(id: number) {
    return this.http.put<void>(`${this.baseUrl}/${id}/toggle-active`, {});
  }
}

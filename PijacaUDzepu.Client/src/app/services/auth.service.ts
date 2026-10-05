import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, map, tap } from 'rxjs';
import { environment } from '../../environments/environment';
import { AcceptInvitationDto, AuthResponse, CreateVendorAdminDto, InviteVendorDto, LoginDto, RegisterDto, User } from '../models/user.model';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private baseUrl = environment.apiUrl + '/auth';
  private currentUserSource = new BehaviorSubject<User | null>(null);
  currentUser$ = this.currentUserSource.asObservable();

  constructor(private http: HttpClient) {
    this.loadStoredUser();
  }

  get currentUser(): User | null {
    return this.currentUserSource.value;
  }

  get isLoggedIn(): boolean {
    return !!this.currentUser;
  }

  get token(): string | null {
    return localStorage.getItem('token');
  }

  hasRole(role: string): boolean {
    return this.currentUser?.roles?.includes(role) ?? false;
  }

  login(dto: LoginDto) {
    return this.http.post<AuthResponse>(`${this.baseUrl}/login`, dto).pipe(
      tap(res => this.setAuth(res))
    );
  }

  register(dto: RegisterDto) {
    return this.http.post<AuthResponse>(`${this.baseUrl}/register`, dto).pipe(
      tap(res => this.setAuth(res))
    );
  }

  createVendorAdmin(dto: CreateVendorAdminDto) {
    return this.http.post<User>(`${this.baseUrl}/create-vendor-admin`, dto);
  }

  inviteVendor(dto: InviteVendorDto) {
    return this.http.post<{ message: string }>(`${this.baseUrl}/invite-vendor`, dto);
  }

  acceptInvitation(dto: AcceptInvitationDto) {
    return this.http.post<AuthResponse>(`${this.baseUrl}/accept-invitation`, dto).pipe(
      tap(res => this.setAuth(res))
    );
  }

  changePassword(currentPassword: string, newPassword: string) {
    return this.http.put<{ message: string }>(`${this.baseUrl}/change-password`, { currentPassword, newPassword });
  }

  loadCurrentUser() {
    return this.http.get<User>(`${this.baseUrl}/me`).pipe(
      tap(user => this.currentUserSource.next(user))
    );
  }

  logout() {
    localStorage.removeItem('token');
    localStorage.removeItem('user');
    this.currentUserSource.next(null);
  }

  private setAuth(res: AuthResponse) {
    localStorage.setItem('token', res.token);
    localStorage.setItem('user', JSON.stringify(res.user));
    this.currentUserSource.next(res.user);
  }

  private loadStoredUser() {
    try {
      const userJson = localStorage.getItem('user');
      if (userJson) {
        this.currentUserSource.next(JSON.parse(userJson));
      }
    } catch {
      this.logout();
    }
  }
}

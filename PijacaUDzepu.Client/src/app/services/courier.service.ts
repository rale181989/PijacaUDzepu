import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { environment } from '../../environments/environment';
import { Order } from '../models/order.model';
import { PagedResult } from '../models/paged-result.model';
import { CourierProfile, CourierProfileInput } from '../models/courier.model';

@Injectable({ providedIn: 'root' })
export class CourierService {
  private baseUrl = environment.apiUrl + '/couriers';

  constructor(private http: HttpClient) {}

  getProfile() {
    return this.http.get<CourierProfile>(`${this.baseUrl}/me`);
  }

  updateProfile(dto: CourierProfileInput) {
    return this.http.put<CourierProfile>(`${this.baseUrl}/me`, dto);
  }

  setShift(isOnShift: boolean) {
    return this.http.put<CourierProfile>(`${this.baseUrl}/me/shift`, { isOnShift });
  }

  markPickedUp(id: number) {
    return this.http.post<Order>(`${this.baseUrl}/orders/${id}/picked-up`, {});
  }

  markDelivered(id: number) {
    return this.http.post<Order>(`${this.baseUrl}/orders/${id}/delivered`, {});
  }

  getAvailableOrders() {
    return this.http.get<Order[]>(`${this.baseUrl}/available-orders`);
  }

  // Aktivne dostave (sve) ili završene/otkazane (po stranama)
  getMyOrders(finished = false, skip = 0, take = 20) {
    const params = new HttpParams().set('finished', String(finished)).set('skip', String(skip)).set('take', String(take));
    return this.http.get<PagedResult<Order>>(`${this.baseUrl}/my-orders`, { params });
  }

  acceptOrder(id: number) {
    return this.http.post<Order>(`${this.baseUrl}/orders/${id}/accept`, {});
  }

  // Porudžbina nestaje samo ovom kuriru; ostali kuriri je i dalje vide
  declineOrder(id: number) {
    return this.http.post<void>(`${this.baseUrl}/orders/${id}/decline`, {});
  }
}

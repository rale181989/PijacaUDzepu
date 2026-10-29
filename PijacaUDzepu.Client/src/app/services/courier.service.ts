import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../environments/environment';
import { Order } from '../models/order.model';
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

  getMyOrders() {
    return this.http.get<Order[]>(`${this.baseUrl}/my-orders`);
  }

  acceptOrder(id: number) {
    return this.http.post<Order>(`${this.baseUrl}/orders/${id}/accept`, {});
  }
}

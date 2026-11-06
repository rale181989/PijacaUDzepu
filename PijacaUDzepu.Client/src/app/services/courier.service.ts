import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { environment } from '../../environments/environment';
import { Order } from '../models/order.model';
import { PagedResult } from '../models/paged-result.model';
import { CourierProfile, CourierProfileInput } from '../models/courier.model';

export interface CourierSettlement {
  date: string;
  delivered: number;
  inProgress: number;
  collected: number;
  owedToVendors: number;
  earned: number;
  vendors: { vendorId: number; vendorName: string; marketName?: string; orders: number; amount: number }[];
  deliveries: { orderId: number; deliveredAt: string; vendorName: string; deliveryAddress?: string; goods: number; fee: number }[];
}

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

  markNotDelivered(id: number, reason: string) {
    return this.http.post<Order>(`${this.baseUrl}/orders/${id}/not-delivered`, { reason });
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

  // "Prihvati sve": sve slobodne porudžbine iste korpe
  acceptCheckout(checkoutId: string) {
    return this.http.post<Order[]>(`${this.baseUrl}/checkouts/${checkoutId}/accept`, {});
  }

  // Dnevni obračun (yyyy-MM-dd; bez datuma = danas)
  getSettlement(date?: string) {
    let params = new HttpParams();
    if (date) params = params.set('date', date);
    return this.http.get<CourierSettlement>(`${this.baseUrl}/settlement`, { params });
  }

  // Porudžbina nestaje samo ovom kuriru; ostali kuriri je i dalje vide
  declineOrder(id: number) {
    return this.http.post<void>(`${this.baseUrl}/orders/${id}/decline`, {});
  }
}

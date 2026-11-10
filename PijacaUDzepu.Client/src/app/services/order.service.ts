import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { environment } from '../../environments/environment';
import { Order, OrderInput, GuestOrderInput, OrderStatus } from '../models/order.model';
import { PagedResult } from '../models/paged-result.model';

export interface VendorToday {
  pending: number;
  confirmed: number;
  ready: number;
  inDelivery: number;
  toPrepare: { productName: string; unit: string; quantity: number; orders: number }[];
  awaitingAnswer: { productName: string; unit: string; quantity: number; orders: number }[];
  todayOrders: number;
  todayCompleted: number;
  todayGoodsTotal: number;
  lowStock: { productId: number; name: string; unit: string; stockQuantity: number }[];
}

@Injectable({ providedIn: 'root' })
export class OrderService {
  private baseUrl = environment.apiUrl + '/orders';

  constructor(private http: HttpClient) {}

  createOrder(dto: OrderInput) {
    return this.http.post<Order[]>(this.baseUrl, dto);
  }

  createGuestOrder(dto: GuestOrderInput) {
    return this.http.post<Order[]>(`${this.baseUrl}/guest`, dto);
  }

  track(code: string) {
    return this.http.get<Order[]>(`${this.baseUrl}/track/${encodeURIComponent(code)}`);
  }

  cancelTracked(code: string, id: number) {
    return this.http.put<Order>(`${this.baseUrl}/track/${encodeURIComponent(code)}/${id}/cancel`, {});
  }

  getDeliveryOptions() {
    return this.http.get<{ courierFee: number }>(`${this.baseUrl}/delivery-options`);
  }

  getMyOrders(skip = 0, take = 20) {
    const params = new HttpParams().set('skip', skip.toString()).set('take', take.toString());
    return this.http.get<PagedResult<Order>>(`${this.baseUrl}/my-orders`, { params });
  }

  getVendorOrders(finished = false, skip = 0, take = 20) {
    const params = new HttpParams().set('finished', String(finished)).set('skip', skip.toString()).set('take', take.toString());
    return this.http.get<PagedResult<Order>>(`${this.baseUrl}/vendor-orders`, { params });
  }

  // SuperAdmin: porudžbine koje još nisu zatvorene, najstarije prve
  getOpenOrders(skip = 0, take = 20) {
    const params = new HttpParams().set('skip', skip.toString()).set('take', take.toString());
    return this.http.get<PagedResult<Order>>(`${this.baseUrl}/open`, { params });
  }

  adminComplete(id: number) {
    return this.http.post<Order>(`${this.baseUrl}/${id}/admin-complete`, {});
  }

  getVendorToday() {
    return this.http.get<VendorToday>(`${this.baseUrl}/vendor-today`);
  }

  getVendorPendingCount() {
    return this.http.get<{ count: number }>(`${this.baseUrl}/vendor-pending-count`);
  }

  getById(id: number) {
    return this.http.get<Order>(`${this.baseUrl}/${id}`);
  }

  updateStatus(id: number, status: OrderStatus, message?: string) {
    return this.http.put<Order>(`${this.baseUrl}/${id}/status`, { status, message });
  }

  rate(id: number, rating: { vendorScore: number; courierScore?: number; comment?: string }) {
    return this.http.post<Order>(`${this.baseUrl}/${id}/rating`, rating);
  }

  rateTracked(code: string, id: number, rating: { vendorScore: number; courierScore?: number; comment?: string }) {
    return this.http.post<Order>(`${this.baseUrl}/track/${encodeURIComponent(code)}/${id}/rating`, rating);
  }

  cancelOrder(id: number) {
    return this.http.put<Order>(`${this.baseUrl}/${id}/cancel`, {});
  }
}

import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { environment } from '../../environments/environment';
import { Order, OrderInput, GuestOrderInput, OrderStatus } from '../models/order.model';
import { PagedResult } from '../models/paged-result.model';

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

  getMyOrders(skip = 0, take = 20) {
    const params = new HttpParams().set('skip', skip.toString()).set('take', take.toString());
    return this.http.get<PagedResult<Order>>(`${this.baseUrl}/my-orders`, { params });
  }

  getVendorOrders(skip = 0, take = 20) {
    const params = new HttpParams().set('skip', skip.toString()).set('take', take.toString());
    return this.http.get<PagedResult<Order>>(`${this.baseUrl}/vendor-orders`, { params });
  }

  getVendorPendingCount() {
    return this.http.get<{ count: number }>(`${this.baseUrl}/vendor-pending-count`);
  }

  getById(id: number) {
    return this.http.get<Order>(`${this.baseUrl}/${id}`);
  }

  updateStatus(id: number, status: OrderStatus) {
    return this.http.put<Order>(`${this.baseUrl}/${id}/status`, { status });
  }

  cancelOrder(id: number) {
    return this.http.put<Order>(`${this.baseUrl}/${id}/cancel`, {});
  }
}

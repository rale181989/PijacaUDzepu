import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { environment } from '../../environments/environment';
import { FulfillmentType, OrderStatus } from '../models/order.model';
import { CourierVehicle } from '../models/courier.model';

// Izveštaji za SuperAdmin-a. Datumi perioda su lokalni (yyyy-MM-dd), uključujući oba dana;
// "promet" računa samo završene porudžbine.
export interface OrdersReport {
  from: string;
  to: string;
  totalOrders: number;
  completedOrders: number;
  rejectedOrders: number;
  cancelledOrders: number;
  activeOrders: number;
  goodsTotal: number;
  deliveryFeesTotal: number;
  averageOrderValue: number;
  byStatus: { status: OrderStatus; count: number }[];
  byFulfillment: { fulfillment: FulfillmentType; orders: number; completed: number; goodsTotal: number; deliveryFeesTotal: number }[];
  byDay: { date: string; orders: number; completed: number; goodsTotal: number }[];
}

export interface CustomerReport {
  key: string;
  name: string;
  userName?: string;
  phone?: string;
  isGuest: boolean;
  orders: number;
  completed: number;
  rejected: number;
  goodsTotal: number;
  lastOrderAt: string;
}

export interface CourierDelivery {
  orderId: number;
  status: OrderStatus;
  createdAt: string;
  pickedUpAt?: string;
  deliveredAt?: string;
  vendorName: string;
  marketName?: string;
  stallLabel?: string;
  deliveryAddress?: string;
  deliveryFee: number;
  amountToPay: number;
  minutes?: number;
}

export interface CourierReport {
  courierId: number;
  name: string;
  phone?: string;
  vehicle?: CourierVehicle;
  licensePlate?: string;
  isOnShift: boolean;
  delivered: number;
  inProgress: number;
  feesTotal: number;
  cashCollected: number;
  averageDeliveryMinutes?: number;
  markets: { name: string; count: number }[];
  deliveries: CourierDelivery[];
}

@Injectable({ providedIn: 'root' })
export class ReportService {
  private baseUrl = environment.apiUrl + '/reports';

  constructor(private http: HttpClient) {}

  getOrders(from: string, to: string) {
    return this.http.get<OrdersReport>(`${this.baseUrl}/orders`, { params: this.range(from, to) });
  }

  getCustomers(from: string, to: string) {
    return this.http.get<CustomerReport[]>(`${this.baseUrl}/customers`, { params: this.range(from, to) });
  }

  getCouriers(from: string, to: string) {
    return this.http.get<CourierReport[]>(`${this.baseUrl}/couriers`, { params: this.range(from, to) });
  }

  // Prazan datum se ne šalje: bez "od" izveštaj ide od prve porudžbine, bez "do" do danas
  private range(from: string, to: string) {
    let params = new HttpParams();
    if (from) params = params.set('from', from);
    if (to) params = params.set('to', to);
    return params;
  }
}

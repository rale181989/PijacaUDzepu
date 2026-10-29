import { Injectable, NgZone } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, Subject } from 'rxjs';
import { ToastController } from '@ionic/angular/lazy';
import * as signalR from '@microsoft/signalr';
import { AuthService } from './auth.service';
import { SoundService } from './sound.service';
import { Order } from '../models/order.model';
import { environment } from '../../environments/environment';

export type CourierEvent =
  | { type: 'new'; order: Order }
  | { type: 'unavailable'; orderId: number }
  | { type: 'status'; orderId: number; status: string };

@Injectable({ providedIn: 'root' })
export class NotificationService {
  private hubConnection: signalR.HubConnection | null = null;
  private hubUrl = environment.apiUrl.replace('/api', '/hubs/orders');
  private apiUrl = environment.apiUrl + '/orders';
  private connecting = false;

  private pendingCountSubject = new BehaviorSubject<number>(0);
  pendingCount$ = this.pendingCountSubject.asObservable();

  // Broj porudžbina koje čekaju kurira (badge u meniju kurira)
  private courierAvailableSubject = new BehaviorSubject<number>(0);
  courierAvailable$ = this.courierAvailableSubject.asObservable();

  private courierEventsSubject = new Subject<CourierEvent>();
  courierEvents$ = this.courierEventsSubject.asObservable();

  // Otvorena pitanja za podršku (badge u meniju SuperAdmin-a)
  private supportOpenSubject = new BehaviorSubject<number>(0);
  private supportRequestSubject = new Subject<number>();
  supportRequest$ = this.supportRequestSubject.asObservable();

  get supportOpenCount(): number {
    return this.supportOpenSubject.value;
  }

  // Bilo kakva promena na porudžbini (status, dodeljen kurir) - stranice se osvežavaju
  private orderUpdatedSubject = new Subject<number>();
  orderUpdated$ = this.orderUpdatedSubject.asObservable();

  get courierAvailableCount(): number {
    return this.courierAvailableSubject.value;
  }

  get pendingCount(): number {
    return this.pendingCountSubject.value;
  }

  constructor(
    private authService: AuthService,
    private http: HttpClient,
    private ngZone: NgZone,
    private toastController: ToastController,
    private sound: SoundService
  ) {
    this.authService.currentUser$.subscribe(user => {
      if (user) {
        this.startConnection();
        if (this.authService.hasRole('VendorAdmin')) {
          this.loadPendingCount();
        }
        if (this.authService.hasRole('Courier')) {
          this.loadCourierAvailableCount();
        }
        if (this.authService.hasRole('SuperAdmin')) {
          this.loadSupportOpenCount();
        }
      } else {
        this.stopConnection();
      }
    });
  }

  loadPendingCount() {
    this.http.get<{ count: number }>(`${this.apiUrl}/vendor-pending-count`).subscribe({
      next: res => this.pendingCountSubject.next(res.count),
      error: () => {}
    });
  }

  loadSupportOpenCount() {
    this.http.get<{ count: number }>(`${environment.apiUrl}/support/open-count`).subscribe({
      next: res => this.supportOpenSubject.next(res.count),
      error: () => {}
    });
  }

  loadCourierAvailableCount() {
    this.http.get<Order[]>(`${environment.apiUrl}/couriers/available-orders`).subscribe({
      next: orders => this.courierAvailableSubject.next(orders.length),
      error: () => {}
    });
  }

  private async startConnection() {
    if (this.connecting || this.hubConnection?.state === signalR.HubConnectionState.Connected) return;

    const token = this.authService.token;
    if (!token) return;

    this.connecting = true;

    if (this.hubConnection) {
      await this.hubConnection.stop().catch(() => {});
    }

    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl(this.hubUrl, { accessTokenFactory: () => token })
      .withAutomaticReconnect()
      .build();

    this.hubConnection.on('NewOrder', (data: any) => {
      this.ngZone.run(async () => {
        this.pendingCountSubject.next(this.pendingCount + 1);
        this.sound.play('newOrder');
        this.orderUpdatedSubject.next(data.orderId);
        const kind = data.fulfillment === 'Pickup' ? 'Nova rezervacija' : 'Nova porudžbina (Eko kurir)';
        await this.showToast(`${kind} #${data.orderId} – ${data.customerName}`, 'tertiary');
      });
    });

    this.hubConnection.on('OrderStatusChanged', (data: any) => {
      this.ngZone.run(async () => {
        this.sound.play('update');
        // Kupac odmah vidi odgovor prodavca
        if (data.message && this.authService.hasRole('Customer')) {
          await this.showToast(`${data.vendorName}: ${data.message}`, 'primary');
        }
        this.courierEventsSubject.next({ type: 'status', orderId: data.orderId, status: data.status });
        this.orderUpdatedSubject.next(data.orderId);
        if (this.authService.hasRole('VendorAdmin')) this.loadPendingCount();
      });
    });

    this.hubConnection.on('CourierAssigned', (data: any) => {
      this.ngZone.run(async () => {
        this.sound.play('update');
        this.orderUpdatedSubject.next(data.orderId);
        await this.showToast(`Kurir ${data.courierName} preuzima porudžbinu #${data.orderId}`, 'success');
      });
    });

    this.hubConnection.on('CourierNewOrder', (order: Order) => {
      this.ngZone.run(async () => {
        this.sound.play('newOrder');
        this.courierAvailableSubject.next(this.courierAvailableCount + 1);
        this.courierEventsSubject.next({ type: 'new', order });
        await this.showToast(`Nova porudžbina za dostavu: ${order.vendorName} → ${order.deliveryAddress ?? ''}`, 'tertiary');
      });
    });

    this.hubConnection.on('NewSupportRequest', (data: { id: number; name: string }) => {
      this.ngZone.run(async () => {
        this.sound.play('newOrder');
        this.supportOpenSubject.next(this.supportOpenCount + 1);
        this.supportRequestSubject.next(data.id);
        await this.showToast(`Novo pitanje za podršku #${data.id} – ${data.name}`, 'warning');
      });
    });

    this.hubConnection.on('CourierOrderUnavailable', (data: { orderId: number }) => {
      this.ngZone.run(() => {
        this.courierEventsSubject.next({ type: 'unavailable', orderId: data.orderId });
        this.loadCourierAvailableCount();
      });
    });

    try {
      await this.hubConnection.start();
    } catch {}
    finally {
      this.connecting = false;
    }
  }

  private stopConnection() {
    this.hubConnection?.stop().catch(() => {});
    this.hubConnection = null;
    this.pendingCountSubject.next(0);
    this.courierAvailableSubject.next(0);
    this.supportOpenSubject.next(0);
  }

  private async showToast(message: string, color: string) {
    const toast = await this.toastController.create({ message, duration: 4000, position: 'top', color });
    await toast.present();
  }
}

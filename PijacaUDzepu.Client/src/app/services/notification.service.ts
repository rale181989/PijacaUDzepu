import { Injectable, NgZone } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { BehaviorSubject, Subject } from 'rxjs';
import { ToastController } from '@ionic/angular/lazy';
import * as signalR from '@microsoft/signalr';
import { Capacitor } from '@capacitor/core';
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

  // Veza je ponovo uspostavljena posle prekida (zaključan telefon, mreža) - događaji iz
  // tog perioda su propušteni, pa stranice sa porudžbinama treba da se ponovo učitaju
  private resyncSubject = new Subject<void>();
  resync$ = this.resyncSubject.asObservable();

  private retryTimer: ReturnType<typeof setTimeout> | null = null;

  // Zvono: ponavlja se dok prodavac ima porudžbina na čekanju, odnosno dok kurir na smeni ima
  // neprihvaćenih (a neodbijenih) porudžbina. Dok aplikacija nije na ekranu, zvoni Android obaveštenje.
  private static readonly AlarmIntervalMs = 4000;
  private alarmTimer: ReturnType<typeof setInterval> | null = null;
  private courierOnShift = false;

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
        this.loadCounts();
      } else {
        this.stopConnection();
      }
    });

    this.pendingCount$.subscribe(() => this.updateAlarm());
    this.courierAvailable$.subscribe(() => this.updateAlarm());

    // Povratak u aplikaciju (ili na tab): Android često prekine vezu dok je ekran zaključan
    document.addEventListener('visibilitychange', () => {
      this.updateAlarm();
      if (document.visibilityState !== 'visible' || !this.authService.token) return;
      if (this.hubConnection?.state === signalR.HubConnectionState.Disconnected || !this.hubConnection) {
        this.startConnection();
      }
      this.ngZone.run(() => this.resync());
    });
  }

  private loadCounts() {
    if (this.authService.hasRole('VendorAdmin')) {
      this.loadPendingCount();
    }
    if (this.authService.hasRole('Courier')) {
      this.loadCourierAvailableCount();
      this.loadCourierShift();
    }
    if (this.authService.hasRole('SuperAdmin')) {
      this.loadSupportOpenCount();
    }
  }

  private loadCourierShift() {
    this.http.get<{ isOnShift: boolean }>(`${environment.apiUrl}/couriers/me`).subscribe({
      next: p => this.setCourierOnShift(p.isOnShift),
      error: () => {}
    });
  }

  // Poziva i stranica kurira kada kurir uključi/isključi smenu
  setCourierOnShift(onShift: boolean) {
    this.courierOnShift = onShift;
    this.updateAlarm();
  }

  private updateAlarm() {
    const vendorRinging = this.authService.hasRole('VendorAdmin') && this.pendingCount > 0;
    const courierRinging = this.authService.hasRole('Courier') && this.courierOnShift && this.courierAvailableCount > 0;
    // Kad aplikacija nije na ekranu, zvoni Android obaveštenje (push); bez push-a zvoni i dalje aplikacija,
    // koliko god joj Android dozvoli da radi u pozadini
    const nativeAlarms = Capacitor.isNativePlatform() && environment.pushEnabled;
    const ring = (vendorRinging || courierRinging) && (document.visibilityState === 'visible' || !nativeAlarms);

    if (ring && !this.alarmTimer) {
      this.sound.play('alarm');
      this.alarmTimer = setInterval(() => this.sound.play('alarm'), NotificationService.AlarmIntervalMs);
    } else if (!ring && this.alarmTimer) {
      clearInterval(this.alarmTimer);
      this.alarmTimer = null;
    }
  }

  // Nova porudžbina dok zvono već radi: zazvoni odmah, a ne tek na sledećem ponavljanju
  private ringAgain() {
    if (!this.alarmTimer) return;
    clearInterval(this.alarmTimer);
    this.alarmTimer = null;
  }

  // Poziva se i kad push alarm stigne dok je aplikacija otvorena (SignalR je možda propustio događaj)
  resync() {
    this.loadCounts();
    this.resyncSubject.next();
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
    this.clearRetry();

    if (this.hubConnection) {
      const old = this.hubConnection;
      this.hubConnection = null;
      await old.stop().catch(() => {});
    }

    // Podrazumevani withAutomaticReconnect() odustaje posle 4 pokušaja (~42 s), pa bi prodavac
    // i kurir posle dužeg prekida ostali bez obaveštenja; ovde se pokušava dok god je korisnik prijavljen
    const connection = new signalR.HubConnectionBuilder()
      .withUrl(this.hubUrl, { accessTokenFactory: () => token })
      .withAutomaticReconnect({
        nextRetryDelayInMilliseconds: ctx => [0, 2000, 5000, 10000][ctx.previousRetryCount] ?? 30000
      })
      .build();
    this.hubConnection = connection;

    connection.onreconnected(() => this.ngZone.run(() => this.resync()));

    // Zatvorena veza (npr. server je bio nedostupan duže vreme) - nova veza za 5 s
    connection.onclose(() => {
      if (this.hubConnection === connection) this.scheduleRetry();
    });

    this.hubConnection.on('NewOrder', (data: any) => {
      this.ngZone.run(async () => {
        // Brojač pokreće zvono (updateAlarm), koje traje dok porudžbina ne bude prihvaćena ili odbijena
        this.ringAgain();
        this.pendingCountSubject.next(this.pendingCount + 1);
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
        this.ringAgain();
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

    // Porudžbina kod ove tezge više nije na čekanju (prihvaćena/odbijena, možda sa drugog uređaja) - zvono staje
    this.hubConnection.on('PendingChanged', (data: { orderId: number }) => {
      this.ngZone.run(() => {
        this.loadPendingCount();
        this.orderUpdatedSubject.next(data.orderId);
      });
    });

    this.hubConnection.on('CourierOrderUnavailable', (data: { orderId: number }) => {
      this.ngZone.run(() => {
        this.courierEventsSubject.next({ type: 'unavailable', orderId: data.orderId });
        this.loadCourierAvailableCount();
      });
    });

    try {
      await connection.start();
    } catch {
      // Server nedostupan pri pokretanju (npr. Render se tek budi) - pokušaj ponovo
      if (this.hubConnection === connection) this.scheduleRetry();
    }
    finally {
      this.connecting = false;
    }
  }

  private scheduleRetry() {
    this.clearRetry();
    this.retryTimer = setTimeout(() => {
      this.retryTimer = null;
      if (this.authService.token) this.startConnection();
    }, 5000);
  }

  private clearRetry() {
    if (this.retryTimer) clearTimeout(this.retryTimer);
    this.retryTimer = null;
  }

  private stopConnection() {
    this.clearRetry();
    const old = this.hubConnection;
    this.hubConnection = null;
    old?.stop().catch(() => {});
    this.courierOnShift = false;
    this.pendingCountSubject.next(0);
    this.courierAvailableSubject.next(0);
    this.supportOpenSubject.next(0);
  }

  private async showToast(message: string, color: string) {
    const toast = await this.toastController.create({ message, duration: 4000, position: 'top', color });
    await toast.present();
  }
}

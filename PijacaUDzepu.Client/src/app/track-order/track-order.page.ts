import { Component, OnDestroy, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { AlertController, ToastController } from '@ionic/angular/lazy';
import { forkJoin, of } from 'rxjs';
import { catchError, map } from 'rxjs/operators';
import * as signalR from '@microsoft/signalr';
import { OrderService } from '../services/order.service';
import { GuestOrdersService } from '../services/guest-orders.service';
import { SoundService } from '../services/sound.service';
import { TranslateService } from '../services/translate.service';
import { Order } from '../models/order.model';
import { environment } from '../../environments/environment';

interface TrackedGroup {
  code: string;
  orders: Order[];
}

// Praćenje porudžbina bez naloga: /prati/{kod} prikazuje jednu kupovinu,
// a /prati sve kupovine zapamćene na ovom uređaju
@Component({
  selector: 'app-track-order',
  templateUrl: './track-order.page.html',
  styleUrls: ['./track-order.page.scss'],
  standalone: false,
})
export class TrackOrderPage implements OnInit, OnDestroy {
  code: string | null = null;
  groups: TrackedGroup[] = [];
  loading = false;
  notFound = false;
  private hub: signalR.HubConnection | null = null;

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private orderService: OrderService,
    private guestOrders: GuestOrdersService,
    private sound: SoundService,
    private alertController: AlertController,
    private toastController: ToastController,
    private ts: TranslateService
  ) {}

  ngOnInit() {
    this.code = this.route.snapshot.paramMap.get('code');
    // Otvoren link se pamti, pa se kupovina vidi i kasnije u "Moje porudžbine"
    if (this.code) this.guestOrders.add(this.code);
    this.load();
  }

  ngOnDestroy() {
    this.hub?.stop().catch(() => {});
  }

  get codes(): string[] {
    return this.code ? [this.code] : this.guestOrders.codes;
  }

  load() {
    const codes = this.codes;
    if (codes.length === 0) {
      this.groups = [];
      return;
    }
    this.loading = true;
    forkJoin(codes.map(code => this.orderService.track(code).pipe(
      map(orders => ({ code, orders } as TrackedGroup)),
      catchError(() => {
        // Istekao ili neispravan link više ne prikazujemo
        this.guestOrders.remove(code);
        return of(null);
      })
    ))).subscribe(results => {
      this.groups = results.filter((g): g is TrackedGroup => g !== null);
      this.notFound = !!this.code && this.groups.length === 0;
      this.loading = false;
      this.connect();
    });
  }

  private async connect() {
    if (this.hub || this.groups.length === 0) return;
    this.hub = new signalR.HubConnectionBuilder()
      .withUrl(environment.apiUrl.replace('/api', '/hubs/tracking'))
      .withAutomaticReconnect()
      .build();

    const onChange = async (data: { message?: string; vendorName?: string; courierName?: string }) => {
      this.sound.play('update');
      this.load();
      const text = data.message ? `${data.vendorName}: ${data.message}`
        : data.courierName ? `${this.ts.t('order.courier')}: ${data.courierName}`
        : this.ts.t('track.updated');
      const toast = await this.toastController.create({ message: text, duration: 4000, position: 'top', color: 'primary' });
      await toast.present();
    };
    this.hub.on('OrderStatusChanged', onChange);
    this.hub.on('CourierAssigned', onChange);
    // Posle ponovnog povezivanja pretplata se obnavlja
    this.hub.onreconnected(() => this.subscribeAll());

    try {
      await this.hub.start();
      await this.subscribeAll();
    } catch {}
  }

  private async subscribeAll() {
    for (const g of this.groups) {
      await this.hub?.invoke('Subscribe', g.code).catch(() => {});
    }
  }

  async share(code: string) {
    const url = this.guestOrders.trackingUrl(code);
    try {
      if (navigator.share) {
        await navigator.share({ title: this.ts.t('track.title'), url });
        return;
      }
      await navigator.clipboard.writeText(url);
      const toast = await this.toastController.create({ message: this.ts.t('track.copied'), duration: 2000, color: 'success' });
      await toast.present();
    } catch {}
  }

  // Za zajedničke računare: briše kupovinu samo sa ovog uređaja, link i dalje radi
  async forget(code: string) {
    const alert = await this.alertController.create({
      header: this.ts.t('track.forget'),
      message: this.ts.t('track.forgetConfirm'),
      buttons: [
        { text: this.ts.t('cart.cancelBtn'), role: 'cancel' },
        {
          text: this.ts.t('track.forget'),
          role: 'destructive',
          handler: () => {
            this.guestOrders.remove(code);
            this.groups = this.groups.filter(g => g.code !== code);
            if (this.code === code) this.router.navigateByUrl('/home');
          }
        }
      ]
    });
    await alert.present();
  }

  trackingUrl(code: string): string {
    return this.guestOrders.trackingUrl(code);
  }

  async cancel(code: string, order: Order) {
    const alert = await this.alertController.create({
      header: this.ts.t('orders.cancel'),
      message: `${this.ts.t('track.cancelConfirm')} #${order.id}?`,
      buttons: [
        { text: this.ts.t('cart.cancelBtn'), role: 'cancel' },
        {
          text: this.ts.t('orders.cancel'),
          role: 'destructive',
          handler: () => this.orderService.cancelTracked(code, order.id).subscribe({
            next: () => this.load(),
            error: async err => {
              const toast = await this.toastController.create({ message: err.error?.message || this.ts.t('order.error'), duration: 3000, color: 'danger' });
              await toast.present();
            }
          })
        }
      ]
    });
    await alert.present();
  }

  getStatusLabel(status: string): string {
    return this.ts.t('status.' + status) || status;
  }

  getStatusColor(status: string): string {
    const map: Record<string, string> = {
      'Pending': 'warning', 'Confirmed': 'primary', 'Rejected': 'danger',
      'ReadyForPickup': 'success', 'InDelivery': 'tertiary', 'Completed': 'medium', 'Cancelled': 'medium'
    };
    return map[status] || 'medium';
  }

  getUnitLabel(unit: string): string {
    return this.ts.t('unit.' + unit) || unit;
  }
}

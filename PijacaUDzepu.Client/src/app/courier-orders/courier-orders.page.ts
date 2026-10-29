import { Component, OnDestroy, OnInit } from '@angular/core';
import { AlertController, ToastController } from '@ionic/angular/lazy';
import { Observable, Subscription } from 'rxjs';
import { CourierService } from '../services/courier.service';
import { NotificationService } from '../services/notification.service';
import { TranslateService } from '../services/translate.service';
import { Order, OrderStatus } from '../models/order.model';

@Component({
  selector: 'app-courier-orders',
  templateUrl: './courier-orders.page.html',
  styleUrls: ['./courier-orders.page.scss'],
  standalone: false,
})
export class CourierOrdersPage implements OnInit, OnDestroy {
  tab: 'available' | 'mine' = 'available';
  available: Order[] = [];
  mine: Order[] = [];
  loading = false;
  acceptingId: number | null = null;
  busyId: number | null = null;
  isOnShift = false;
  shiftLoaded = false;
  private sub?: Subscription;

  constructor(
    private courierService: CourierService,
    private notificationService: NotificationService,
    private alertController: AlertController,
    private toastController: ToastController,
    private ts: TranslateService
  ) {}

  ngOnInit() {
    this.sub = this.notificationService.courierEvents$.subscribe(event => {
      if (event.type === 'new') {
        if (!this.available.some(o => o.id === event.order.id)) {
          this.available = [event.order, ...this.available];
        }
      } else if (event.type === 'unavailable') {
        this.available = this.available.filter(o => o.id !== event.orderId);
      } else if (event.type === 'status') {
        const order = this.mine.find(o => o.id === event.orderId);
        if (order) order.status = event.status as OrderStatus;
      }
    });
  }

  ionViewWillEnter() {
    this.load();
  }

  ngOnDestroy() {
    this.sub?.unsubscribe();
  }

  load() {
    this.courierService.getProfile().subscribe(p => {
      this.isOnShift = p.isOnShift;
      this.shiftLoaded = true;
    });
    this.loading = true;
    let pending = 2;
    const done = () => { if (--pending === 0) this.loading = false; };
    this.courierService.getAvailableOrders().subscribe({
      next: orders => { this.available = orders; done(); },
      error: () => done()
    });
    this.courierService.getMyOrders().subscribe({
      next: orders => { this.mine = orders; done(); },
      error: () => done()
    });
    this.notificationService.loadCourierAvailableCount();
  }

  doRefresh(event: any) {
    this.load();
    setTimeout(() => event.target.complete(), 500);
  }

  toggleShift(value: boolean) {
    this.courierService.setShift(value).subscribe({
      next: async p => {
        this.isOnShift = p.isOnShift;
        await this.toast(p.isOnShift ? 'Na smeni ste – stižu vam nove porudžbine' : 'Van smene – nove porudžbine vam neće stizati', p.isOnShift ? 'success' : 'medium');
      },
      error: async () => {
        this.isOnShift = !value;
        await this.toast('Promena smene nije uspela', 'danger');
      }
    });
  }

  markPickedUp(order: Order) {
    this.runStep(order, this.courierService.markPickedUp(order.id), 'Roba preuzeta – srećan put!');
  }

  async markDelivered(order: Order) {
    const alert = await this.alertController.create({
      header: 'Potvrda isporuke',
      message: `Da li ste predali porudžbinu #${order.id} i naplatili ${order.amountToPay.toFixed(0)} din?`,
      buttons: [
        { text: 'Ne', role: 'cancel' },
        { text: 'Isporučeno', handler: () => this.runStep(order, this.courierService.markDelivered(order.id), 'Porudžbina isporučena') }
      ]
    });
    await alert.present();
  }

  private runStep(order: Order, step: Observable<Order>, success: string) {
    this.busyId = order.id;
    step.subscribe({
      next: async updated => {
        this.busyId = null;
        this.mine = this.mine.map(o => o.id === updated.id ? updated : o);
        await this.toast(success, 'success');
      },
      error: async err => {
        this.busyId = null;
        await this.toast(err.error?.message || 'Greška', 'danger');
      }
    });
  }

  accept(order: Order) {
    this.acceptingId = order.id;
    this.courierService.acceptOrder(order.id).subscribe({
      next: async accepted => {
        this.acceptingId = null;
        this.available = this.available.filter(o => o.id !== order.id);
        this.mine = [accepted, ...this.mine];
        this.tab = 'mine';
        this.notificationService.loadCourierAvailableCount();
        await this.toast(`Porudžbina #${order.id} je vaša`, 'success');
      },
      error: async err => {
        this.acceptingId = null;
        if (err.status === 409 || err.status === 404) {
          this.available = this.available.filter(o => o.id !== order.id);
        }
        await this.toast(err.error?.message || 'Greška pri prihvatanju', 'warning');
      }
    });
  }

  get activeMine(): Order[] {
    return this.mine.filter(o => this.isActive(o.status));
  }

  get finishedMine(): Order[] {
    return this.mine.filter(o => !this.isActive(o.status));
  }

  private isActive(status: OrderStatus): boolean {
    return status === 'Pending' || status === 'Confirmed' || status === 'ReadyForPickup' || status === 'InDelivery';
  }

  getStatusLabel(status: OrderStatus): string {
    const labels: Record<string, string> = {
      'Pending': 'Čeka potvrdu prodavca', 'Confirmed': 'Prodavac priprema', 'Rejected': 'Odbijena',
      'ReadyForPickup': 'Spremno za preuzimanje', 'InDelivery': 'U dostavi', 'Completed': 'Isporučena', 'Cancelled': 'Otkazana'
    };
    return labels[status] || status;
  }

  getStatusColor(status: OrderStatus): string {
    const colors: Record<string, string> = {
      'Pending': 'warning', 'Confirmed': 'primary', 'Rejected': 'danger',
      'ReadyForPickup': 'success', 'InDelivery': 'tertiary', 'Completed': 'medium', 'Cancelled': 'medium'
    };
    return colors[status] || 'medium';
  }

  getUnitLabel(unit: string): string {
    return this.ts.t('unit.' + unit) || unit;
  }

  mapsUrl(address?: string): string {
    return 'https://www.google.com/maps/search/?api=1&query=' + encodeURIComponent(address ?? '');
  }

  private async toast(message: string, color: string) {
    const toast = await this.toastController.create({ message, duration: 2500, color });
    await toast.present();
  }
}

import { Component, OnDestroy, OnInit } from '@angular/core';
import { OrderService } from '../services/order.service';
import { NotificationService } from '../services/notification.service';
import { Order, OrderStatus } from '../models/order.model';
import { AlertController, ToastController } from '@ionic/angular/lazy';
import { Subscription } from 'rxjs';

@Component({
  selector: 'app-vendor-orders',
  templateUrl: './vendor-orders.page.html',
  styleUrls: ['./vendor-orders.page.scss'],
  standalone: false,
})
export class VendorOrdersPage implements OnInit, OnDestroy {
  orders: Order[] = [];
  private sub?: Subscription;
  private loadSub?: Subscription;
  loading = false;
  hasMore = true;
  private pageSize = 20;

  constructor(
    private orderService: OrderService,
    private notificationService: NotificationService,
    private alertController: AlertController,
    private toastController: ToastController
  ) {}

  ngOnInit() {
    this.loadOrders();
    // Nova porudžbina, dodeljen kurir ili promena statusa od strane kurira
    this.sub = this.notificationService.orderUpdated$.subscribe(() => this.loadOrders());
  }

  ngOnDestroy() {
    this.sub?.unsubscribe();
    this.loadSub?.unsubscribe();
  }
  ionViewWillEnter() {
    this.loadOrders();
    this.notificationService.loadPendingCount();
  }

  loadOrders() {
    // Novo učitavanje prekida prethodno i zamenjuje listu; inače se dva istovremena
    // učitavanja (ulazak na stranicu + obaveštenje) spoje i porudžbine se prikažu duplo
    this.loadSub?.unsubscribe();
    this.hasMore = true;
    this.loading = true;
    this.loadSub = this.orderService.getVendorOrders(0, this.pageSize).subscribe({
      next: result => {
        this.orders = result.items;
        this.hasMore = result.hasMore;
        this.loading = false;
      },
      error: () => this.loading = false
    });
  }

  loadMore(event: any) {
    this.orderService.getVendorOrders(this.orders.length, this.pageSize).subscribe({
      next: result => {
        this.orders = [...this.orders, ...result.items];
        this.hasMore = result.hasMore;
        event.target.complete();
        if (!result.hasMore) event.target.disabled = true;
      },
      error: () => event.target.complete()
    });
  }

  async updateStatus(order: Order, newStatus: OrderStatus, message?: string) {
    this.orderService.updateStatus(order.id, newStatus, message).subscribe({
      next: async updated => {
        Object.assign(order, updated);
        this.notificationService.loadPendingCount();
        const toast = await this.toastController.create({
          message: `Status promenjen: ${this.getStatusLabel(newStatus)}`, duration: 2000, color: 'success'
        });
        await toast.present();
      },
      error: async err => {
        const toast = await this.toastController.create({
          message: err.error?.message || 'Greška', duration: 2000, color: 'danger'
        });
        await toast.present();
      }
    });
  }

  // Prodavac uz potvrdu, odbijanje ili "Spremno" može da napiše poruku kupcu
  async respond(order: Order, status: OrderStatus) {
    const pickup = order.fulfillment === 'Pickup';
    const titles: Record<string, string> = {
      'Confirmed': pickup ? 'Potvrdi rezervaciju' : 'Potvrdi porudžbinu',
      'Rejected': pickup ? 'Odbij rezervaciju' : 'Odbij porudžbinu',
      'ReadyForPickup': 'Roba je spremna',
    };
    const placeholders: Record<string, string> = {
      'Confirmed': pickup ? 'npr. Možete doći posle 10h, tezga 14' : 'npr. Pripremam, biće spremno za 30 min',
      'Rejected': 'npr. Nažalost, nestalo je robe',
      'ReadyForPickup': pickup ? 'npr. Spremno, čeka vas kod tezge' : 'npr. Spremno za kurira',
    };
    const alert = await this.alertController.create({
      header: `${titles[status]} #${order.id}`,
      inputs: [{ name: 'message', type: 'textarea', placeholder: placeholders[status] + ' (opciono)', attributes: { maxlength: 300 } }],
      buttons: [
        { text: 'Otkaži', role: 'cancel' },
        {
          text: status === 'Rejected' ? 'Odbij' : 'Pošalji',
          handler: data => this.updateStatus(order, status, data?.message?.trim() || undefined)
        }
      ]
    });
    await alert.present();
  }

  getStatusLabel(status: string): string {
    const map: Record<string, string> = {
      'Pending': 'Na čekanju', 'Confirmed': 'Potvrđena', 'Rejected': 'Odbijena',
      'ReadyForPickup': 'Spremna', 'InDelivery': 'U dostavi', 'Completed': 'Završena', 'Cancelled': 'Otkazana'
    };
    return map[status] || status;
  }

  getStatusColor(status: string): string {
    const map: Record<string, string> = {
      'Pending': 'warning', 'Confirmed': 'primary', 'Rejected': 'danger',
      'ReadyForPickup': 'success', 'InDelivery': 'tertiary', 'Completed': 'medium', 'Cancelled': 'medium'
    };
    return map[status] || 'medium';
  }

  getUnitLabel(unit: string): string {
    switch (unit) { case 'Kg': return 'kg'; case 'Komad': return 'kom'; case 'Veza': return 'veza'; default: return unit; }
  }

  doRefresh(event: any) {
    this.loadOrders();
    setTimeout(() => event.target.complete(), 500);
  }
}

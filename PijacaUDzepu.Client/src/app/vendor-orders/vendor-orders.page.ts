import { Component, OnInit } from '@angular/core';
import { OrderService } from '../services/order.service';
import { NotificationService } from '../services/notification.service';
import { Order, OrderStatus } from '../models/order.model';
import { AlertController, ToastController } from '@ionic/angular/lazy';

@Component({
  selector: 'app-vendor-orders',
  templateUrl: './vendor-orders.page.html',
  styleUrls: ['./vendor-orders.page.scss'],
  standalone: false,
})
export class VendorOrdersPage implements OnInit {
  orders: Order[] = [];
  loading = false;
  hasMore = true;
  private pageSize = 20;

  constructor(
    private orderService: OrderService,
    private notificationService: NotificationService,
    private alertController: AlertController,
    private toastController: ToastController
  ) {}

  ngOnInit() { this.loadOrders(); }
  ionViewWillEnter() {
    this.loadOrders();
    this.notificationService.loadPendingCount();
  }

  loadOrders(reset = true) {
    if (reset) {
      this.orders = [];
      this.hasMore = true;
    }
    this.loading = true;
    this.orderService.getVendorOrders(this.orders.length, this.pageSize).subscribe({
      next: result => {
        this.orders = [...this.orders, ...result.items];
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

  async updateStatus(order: Order, newStatus: OrderStatus) {
    this.orderService.updateStatus(order.id, newStatus).subscribe({
      next: async updated => {
        order.status = updated.status;
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

  async confirmReject(order: Order) {
    const alert = await this.alertController.create({
      header: 'Odbij porudžbinu',
      message: `Da li želite da odbijete porudžbinu #${order.id}?`,
      buttons: [
        { text: 'Otkaži', role: 'cancel' },
        { text: 'Odbij', handler: () => this.updateStatus(order, 'Rejected') }
      ]
    });
    await alert.present();
  }

  getStatusLabel(status: string): string {
    const map: Record<string, string> = {
      'Pending': 'Na čekanju', 'Confirmed': 'Potvrđena', 'Rejected': 'Odbijena',
      'ReadyForPickup': 'Spremna', 'Completed': 'Završena', 'Cancelled': 'Otkazana'
    };
    return map[status] || status;
  }

  getStatusColor(status: string): string {
    const map: Record<string, string> = {
      'Pending': 'warning', 'Confirmed': 'primary', 'Rejected': 'danger',
      'ReadyForPickup': 'success', 'Completed': 'medium', 'Cancelled': 'medium'
    };
    return map[status] || 'medium';
  }

  getUnitLabel(unit: string): string {
    switch (unit) { case 'Kg': return 'kg'; case 'Komad': return 'kom'; case 'Veza': return 'veza'; default: return unit; }
  }

  doRefresh(event: any) {
    this.loadOrders(true);
    setTimeout(() => event.target.complete(), 500);
  }
}

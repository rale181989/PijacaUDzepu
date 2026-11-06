import { Component, OnDestroy } from '@angular/core';
import { AlertController, ToastController } from '@ionic/angular/lazy';
import { Subscription } from 'rxjs';
import { OrderService } from '../services/order.service';
import { Order, OrderStatus } from '../models/order.model';
import { PagerComponent } from '../shared/pager/pager.component';

// Porudžbine koje još nisu zatvorene, najstarije prve. SuperAdmin ovde završava one koje su
// slučajno ostale otvorene; porudžbina pamti ko ju je i kada zatvorio.
@Component({
  selector: 'app-admin-orders',
  templateUrl: './admin-orders.page.html',
  styleUrls: ['./admin-orders.page.scss'],
  standalone: false,
})
export class AdminOrdersPage implements OnDestroy {
  orders: Order[] = [];
  loading = false;
  busyId: number | null = null;
  page = 0;
  totalCount = 0;
  readonly pageSizeKey = 'adminOrdersPageSize';
  pageSize = PagerComponent.savedSize(this.pageSizeKey);
  private loadSub?: Subscription;

  constructor(
    private orderService: OrderService,
    private alertController: AlertController,
    private toastController: ToastController
  ) {}

  ionViewWillEnter() {
    this.load();
  }

  ngOnDestroy() {
    this.loadSub?.unsubscribe();
  }

  load() {
    this.loadSub?.unsubscribe();
    this.loading = true;
    this.loadSub = this.orderService.getOpenOrders(this.page * this.pageSize, this.pageSize).subscribe({
      next: r => {
        this.orders = r.items;
        this.totalCount = r.totalCount;
        this.loading = false;
        // Poslednja porudžbina sa strane je zatvorena - prikaži prethodnu stranu
        if (this.orders.length === 0 && this.page > 0 && this.totalCount > 0) {
          this.page = Math.ceil(this.totalCount / this.pageSize) - 1;
          this.load();
        }
      },
      error: () => this.loading = false
    });
  }

  goToPage(page: number) {
    this.page = page;
    this.load();
  }

  setPageSize(size: number) {
    this.page = PagerComponent.pageAfterResize(this.page, this.pageSize, size);
    this.pageSize = size;
    this.load();
  }

  doRefresh(event: any) {
    this.load();
    setTimeout(() => event.target.complete(), 500);
  }

  async complete(order: Order) {
    const alert = await this.alertController.create({
      header: `Završi porudžbinu #${order.id}?`,
      message: `${order.vendorName} · ${order.customerName}. Porudžbina će biti označena kao završena, a prodavac, kurir i kupac videće da ju je zatvorio administrator.`,
      buttons: [
        { text: 'Otkaži', role: 'cancel' },
        { text: 'Završi', handler: () => this.doComplete(order) }
      ]
    });
    await alert.present();
  }

  private doComplete(order: Order) {
    this.busyId = order.id;
    this.orderService.adminComplete(order.id).subscribe({
      next: async () => {
        this.busyId = null;
        await this.toast(`Porudžbina #${order.id} je završena`, 'success');
        this.load();
      },
      error: async err => {
        this.busyId = null;
        await this.toast(err.error?.message || 'Greška', 'danger');
        this.load();
      }
    });
  }

  // Koliko dugo je porudžbina otvorena: "45 min", "5 h", "3 dana"
  age(order: Order): string {
    const minutes = Math.max(0, Math.floor((Date.now() - new Date(order.createdAt).getTime()) / 60000));
    if (minutes < 60) return `${minutes} min`;
    const hours = Math.floor(minutes / 60);
    if (hours < 48) return `${hours} h`;
    return `${Math.floor(hours / 24)} dana`;
  }

  isOld(order: Order): boolean {
    return Date.now() - new Date(order.createdAt).getTime() > 3 * 3600_000;
  }

  getStatusLabel(status: OrderStatus): string {
    const map: Record<string, string> = {
      'Pending': 'Na čekanju', 'Confirmed': 'Potvrđena', 'ReadyForPickup': 'Spremna', 'InDelivery': 'U dostavi'
    };
    return map[status] || status;
  }

  getStatusColor(status: OrderStatus): string {
    const map: Record<string, string> = {
      'Pending': 'warning', 'Confirmed': 'primary', 'ReadyForPickup': 'success', 'InDelivery': 'tertiary'
    };
    return map[status] || 'medium';
  }

  private async toast(message: string, color: string) {
    const toast = await this.toastController.create({ message, duration: 2500, color });
    await toast.present();
  }
}

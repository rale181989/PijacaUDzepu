import { Component, OnInit } from '@angular/core';
import { OrderService } from '../services/order.service';
import { Order } from '../models/order.model';
import { ToastController } from '@ionic/angular/lazy';

@Component({
  selector: 'app-my-orders',
  templateUrl: './my-orders.page.html',
  styleUrls: ['./my-orders.page.scss'],
  standalone: false,
})
export class MyOrdersPage implements OnInit {
  orders: Order[] = [];
  loading = false;
  hasMore = true;
  private pageSize = 20;

  constructor(
    private orderService: OrderService,
    private toastController: ToastController
  ) {}

  ngOnInit() {
    this.loadOrders();
  }

  ionViewWillEnter() {
    this.loadOrders();
  }

  loadOrders(reset = true) {
    if (reset) {
      this.orders = [];
      this.hasMore = true;
    }
    this.loading = true;
    this.orderService.getMyOrders(this.orders.length, this.pageSize).subscribe({
      next: result => {
        this.orders = [...this.orders, ...result.items];
        this.hasMore = result.hasMore;
        this.loading = false;
      },
      error: () => this.loading = false
    });
  }

  loadMore(event: any) {
    this.orderService.getMyOrders(this.orders.length, this.pageSize).subscribe({
      next: result => {
        this.orders = [...this.orders, ...result.items];
        this.hasMore = result.hasMore;
        event.target.complete();
        if (!result.hasMore) event.target.disabled = true;
      },
      error: () => event.target.complete()
    });
  }

  async cancelOrder(order: Order) {
    this.orderService.cancelOrder(order.id).subscribe({
      next: async (updated) => {
        order.status = updated.status;
        const toast = await this.toastController.create({
          message: 'Porudžbina je otkazana', duration: 2000, color: 'warning'
        });
        await toast.present();
      },
      error: async (err) => {
        const toast = await this.toastController.create({
          message: err.error?.message || 'Greška', duration: 2000, color: 'danger'
        });
        await toast.present();
      }
    });
  }

  getStatusLabel(status: string): string {
    const map: Record<string, string> = {
      'Pending': 'Na čekanju',
      'Confirmed': 'Potvrđena',
      'Rejected': 'Odbijena',
      'ReadyForPickup': 'Spremna za preuzimanje',
      'Completed': 'Završena',
      'Cancelled': 'Otkazana'
    };
    return map[status] || status;
  }

  getStatusColor(status: string): string {
    const map: Record<string, string> = {
      'Pending': 'warning',
      'Confirmed': 'primary',
      'Rejected': 'danger',
      'ReadyForPickup': 'success',
      'Completed': 'medium',
      'Cancelled': 'medium'
    };
    return map[status] || 'medium';
  }

  doRefresh(event: any) {
    this.loadOrders(true);
    setTimeout(() => event.target.complete(), 500);
  }
}

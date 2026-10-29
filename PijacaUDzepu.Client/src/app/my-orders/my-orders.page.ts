import { Component, OnDestroy, OnInit } from '@angular/core';
import { OrderService } from '../services/order.service';
import { Order } from '../models/order.model';
import { ToastController } from '@ionic/angular/lazy';
import { TranslateService } from '../services/translate.service';
import { NotificationService } from '../services/notification.service';
import { Subscription } from 'rxjs';

@Component({
  selector: 'app-my-orders',
  templateUrl: './my-orders.page.html',
  styleUrls: ['./my-orders.page.scss'],
  standalone: false,
})
export class MyOrdersPage implements OnInit, OnDestroy {
  orders: Order[] = [];
  private sub?: Subscription;
  private loadSub?: Subscription;
  loading = false;
  hasMore = true;
  private pageSize = 20;

  constructor(
    private orderService: OrderService,
    private toastController: ToastController,
    private ts: TranslateService,
    private notificationService: NotificationService
  ) {}

  ngOnInit() {
    this.loadOrders();
    this.sub = this.notificationService.orderUpdated$.subscribe(orderId => {
      if (this.orders.some(o => o.id === orderId)) this.loadOrders();
    });
  }

  ngOnDestroy() {
    this.sub?.unsubscribe();
    this.loadSub?.unsubscribe();
  }

  ionViewWillEnter() {
    this.loadOrders();
  }

  loadOrders() {
    // Novo učitavanje prekida prethodno i zamenjuje listu; inače se dva istovremena
    // učitavanja (ulazak na stranicu + obaveštenje) spoje i porudžbine se prikažu duplo
    this.loadSub?.unsubscribe();
    this.hasMore = true;
    this.loading = true;
    this.loadSub = this.orderService.getMyOrders(0, this.pageSize).subscribe({
      next: result => {
        this.orders = result.items;
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
          message: this.ts.t('order.cancelled'), duration: 2000, color: 'warning'
        });
        await toast.present();
      },
      error: async (err) => {
        const toast = await this.toastController.create({
          message: err.error?.message || this.ts.t('order.error'), duration: 2000, color: 'danger'
        });
        await toast.present();
      }
    });
  }

  getStatusLabel(status: string): string {
    return this.ts.t('status.' + status) || status;
  }

  getStatusColor(status: string): string {
    const map: Record<string, string> = {
      'Pending': 'warning',
      'Confirmed': 'primary',
      'Rejected': 'danger',
      'ReadyForPickup': 'success',
      'InDelivery': 'tertiary',
      'Completed': 'medium',
      'Cancelled': 'medium'
    };
    return map[status] || 'medium';
  }

  doRefresh(event: any) {
    this.loadOrders();
    setTimeout(() => event.target.complete(), 500);
  }
}

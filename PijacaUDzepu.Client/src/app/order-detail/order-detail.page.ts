import { Component, OnDestroy, OnInit } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { OrderService } from '../services/order.service';
import { Order } from '../models/order.model';
import { TranslateService } from '../services/translate.service';
import { NotificationService } from '../services/notification.service';
import { Subscription } from 'rxjs';

@Component({
  selector: 'app-order-detail',
  templateUrl: './order-detail.page.html',
  styleUrls: ['./order-detail.page.scss'],
  standalone: false,
})
export class OrderDetailPage implements OnInit, OnDestroy {
  order: Order | null = null;
  loading = false;
  private sub?: Subscription;

  constructor(
    private route: ActivatedRoute,
    private orderService: OrderService,
    private ts: TranslateService,
    private notificationService: NotificationService
  ) {}

  ngOnInit() {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    // Status i kurir se osvežavaju čim stigne obaveštenje
    this.sub = this.notificationService.orderUpdated$.subscribe(orderId => {
      if (orderId === id) this.orderService.getById(id).subscribe(o => this.order = o);
    });
    if (id) {
      this.loading = true;
      this.orderService.getById(id).subscribe({
        next: order => { this.order = order; this.loading = false; },
        error: () => this.loading = false
      });
    }
  }

  ngOnDestroy() {
    this.sub?.unsubscribe();
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

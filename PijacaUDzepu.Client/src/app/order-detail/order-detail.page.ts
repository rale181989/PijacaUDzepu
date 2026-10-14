import { Component, OnInit } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { OrderService } from '../services/order.service';
import { Order } from '../models/order.model';
import { TranslateService } from '../services/translate.service';

@Component({
  selector: 'app-order-detail',
  templateUrl: './order-detail.page.html',
  styleUrls: ['./order-detail.page.scss'],
  standalone: false,
})
export class OrderDetailPage implements OnInit {
  order: Order | null = null;
  loading = false;

  constructor(
    private route: ActivatedRoute,
    private orderService: OrderService,
    private ts: TranslateService
  ) {}

  ngOnInit() {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    if (id) {
      this.loading = true;
      this.orderService.getById(id).subscribe({
        next: order => { this.order = order; this.loading = false; },
        error: () => this.loading = false
      });
    }
  }

  getStatusLabel(status: string): string {
    return this.ts.t('status.' + status) || status;
  }

  getStatusColor(status: string): string {
    const map: Record<string, string> = {
      'Pending': 'warning', 'Confirmed': 'primary', 'Rejected': 'danger',
      'ReadyForPickup': 'success', 'Completed': 'medium', 'Cancelled': 'medium'
    };
    return map[status] || 'medium';
  }

  getUnitLabel(unit: string): string {
    return this.ts.t('unit.' + unit) || unit;
  }
}

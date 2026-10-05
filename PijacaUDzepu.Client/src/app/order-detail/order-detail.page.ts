import { Component, OnInit } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { OrderService } from '../services/order.service';
import { Order } from '../models/order.model';

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
    private orderService: OrderService
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
    const map: Record<string, string> = {
      'Pending': 'Na čekanju', 'Confirmed': 'Potvrđena', 'Rejected': 'Odbijena',
      'ReadyForPickup': 'Spremna za preuzimanje', 'Completed': 'Završena', 'Cancelled': 'Otkazana'
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
}

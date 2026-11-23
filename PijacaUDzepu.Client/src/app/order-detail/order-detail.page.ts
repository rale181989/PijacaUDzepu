import { directionsTo, marketPoint, showPickupWay, stallPoint } from '../shared/maps';
import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { OrderService } from '../services/order.service';
import { Order } from '../models/order.model';
import { TranslateService } from '../services/translate.service';
import { NotificationService } from '../services/notification.service';
import { OrderActionsService } from '../services/order-actions.service';
import { Subscription } from 'rxjs';

@Component({
  selector: 'app-order-detail',
  templateUrl: './order-detail.page.html',
  styleUrls: ['./order-detail.page.scss'],
  standalone: false,
})
export class OrderDetailPage implements OnInit, OnDestroy {
  private route = inject(ActivatedRoute);
  private orderService = inject(OrderService);
  private ts = inject(TranslateService);
  private notificationService = inject(NotificationService);
  orderActions = inject(OrderActionsService);

  order: Order | null = null;
  loading = false;
  private sub?: Subscription;

  async rate() {
    if (!this.order) return;
    const rating = await this.orderActions.rate(this.order);
    if (rating) this.order.rating = rating;
  }

  ngOnInit() {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    // Status i kurir se osvežavaju čim stigne obaveštenje
    this.sub = this.notificationService.orderUpdated$.subscribe(orderId => {
      if (orderId === id) this.orderService.getById(id).subscribe(o => this.order = o);
    });
    // Prodavac je pomerio tačku tezge: mapa i "Do tezge" odmah pokazuju novo mesto
    this.sub.add(this.notificationService.stallMoved$.subscribe(vendorId => {
      if (this.order?.vendorId === vendorId) this.orderService.getById(id).subscribe(o => this.order = o);
    }));
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

  readonly encodeURIComponent = encodeURIComponent;
  // Rezervacija: tačka tezge (ako ju je prodavac postavio) na mapi i navigacija peške do nje
  readonly stallPoint = stallPoint;
  readonly marketPoint = marketPoint;
  readonly directionsTo = directionsTo;
  readonly showPickupWay = showPickupWay;

  // Kurirska porudžbina spremna kod prodavca čeka kurira (kupac ne treba da dođe po nju)
  getStatusLabel(status: string, fulfillment?: string): string {
    if (status === 'ReadyForPickup' && fulfillment === 'Courier') return this.ts.t('status.ReadyForCourier');
    return this.ts.t('status.' + status) || status;
  }

  getStatusColor(status: string): string {
    const map: Record<string, string> = {
      'Pending': 'warning', 'Confirmed': 'primary', 'Rejected': 'danger',
      'ReadyForPickup': 'success', 'InDelivery': 'tertiary', 'Completed': 'medium', 'Cancelled': 'medium', 'NotDelivered': 'danger'
    };
    return map[status] || 'medium';
  }

  // Uz količinu: "2 veze", "5 veza"
  getUnitLabel(unit: string, qty?: number): string {
    return this.ts.unit(unit, qty);
  }
}

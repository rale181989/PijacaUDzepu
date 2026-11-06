import { Component, Input } from '@angular/core';
import { Order } from '../../models/order.model';

interface ProgressStep {
  key: string;      // ključ prevoda: progress.*
  done: boolean;
  time?: string | null;
  detail?: string | null;
}

// Koraci porudžbine za kupca. Dostava: poručeno → prodavac potvrdio → dodeljen kurir → kurir preuzeo robu → dostavljeno.
// Rezervacija: rezervisano → prodavac potvrdio → roba spremna → preuzeto. Odbijena i otkazana imaju svoj prikaz.
@Component({
  selector: 'app-order-progress',
  templateUrl: './order-progress.component.html',
  styleUrls: ['./order-progress.component.scss'],
  standalone: false,
})
export class OrderProgressComponent {
  @Input() order!: Order;

  // Zapamćen rezultat dok se porudžbina ne promeni: novi niz pri svakoj proveri promena bi stalno
  // ponovo pravio elemente u *ngFor
  private cache?: { key: string; steps: ProgressStep[] };

  get steps(): ProgressStep[] {
    const o = this.order;
    const key = [o.id, o.status, o.respondedAt, o.courierName, o.courierAssignedAt, o.pickedUpAt, o.deliveredAt, o.closedByAdminAt].join('|');
    if (this.cache?.key === key) return this.cache.steps;

    // Korak je završen samo ako se zaista desio (porudžbinu koju je zatvorio administrator kurir možda nije ni preuzeo)
    const reached = (...statuses: string[]) => statuses.includes(o.status);
    const byAdmin = !!o.closedByAdminAt;
    const last: ProgressStep = byAdmin
      ? { key: 'progress.closedByAdmin', done: true, time: o.closedByAdminAt }
      : o.fulfillment === 'Courier'
        ? { key: 'progress.delivered', done: reached('Completed'), time: o.deliveredAt }
        : { key: 'progress.collected', done: reached('Completed') };
    const confirmed = !!o.respondedAt || reached('Confirmed', 'ReadyForPickup', 'InDelivery') || (reached('Completed') && !byAdmin);
    const steps: ProgressStep[] = o.fulfillment === 'Courier'
      ? [
          { key: 'progress.ordered', done: true, time: o.createdAt },
          { key: 'progress.confirmed', done: confirmed, time: o.respondedAt },
          { key: 'progress.courierAssigned', done: !!o.courierName || !!o.courierAssignedAt, time: o.courierAssignedAt, detail: o.courierName },
          { key: 'progress.pickedUp', done: !!o.pickedUpAt || reached('InDelivery'), time: o.pickedUpAt },
          last,
        ]
      : [
          { key: 'progress.reserved', done: true, time: o.createdAt },
          { key: 'progress.confirmed', done: confirmed, time: o.respondedAt },
          { key: 'progress.ready', done: reached('ReadyForPickup') || (reached('Completed') && !byAdmin) },
          last,
        ];
    this.cache = { key, steps };
    return steps;
  }

  // Prvi korak koji još nije završen je "trenutni"
  get currentIndex(): number {
    return this.steps.findIndex(s => !s.done);
  }
}

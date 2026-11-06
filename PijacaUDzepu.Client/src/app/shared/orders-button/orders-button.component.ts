import { Component, OnDestroy } from '@angular/core';
import { Subscription } from 'rxjs';
import { AuthService } from '../../services/auth.service';
import { NotificationService } from '../../services/notification.service';
import { GuestOrdersService } from '../../services/guest-orders.service';

// Porudžbine u gornjem desnom uglu, van menija, sa crvenim brojem koji kratko poskoči kada nešto stigne.
// Prodavac: porudžbine na čekanju (one na koje treba da odgovori) → /vendor-orders.
// Kupac: porudžbine u toku → /my-orders. Gost sa sačuvanim kodovima: bez broja → /prati.
// Na računaru su porudžbine u gornjem meniju.
@Component({
  selector: 'app-orders-button',
  templateUrl: './orders-button.component.html',
  styleUrls: ['./orders-button.component.scss'],
  standalone: false,
})
export class OrdersButtonComponent implements OnDestroy {
  count = 0;
  bump = false;
  private sub = new Subscription();
  private bumpTimer?: ReturnType<typeof setTimeout>;

  constructor(
    public authService: AuthService,
    public guestOrders: GuestOrdersService,
    notificationService: NotificationService
  ) {
    const vendor = authService.hasRole('VendorAdmin');
    const count$ = vendor ? notificationService.pendingCount$ : notificationService.customerActive$;
    this.sub.add(count$.subscribe(count => {
      // Nova porudžbina takođe "poskoči"
      if (count > this.count) this.animate();
      this.count = count;
    }));
    if (!vendor) this.sub.add(notificationService.customerBump$.subscribe(() => this.animate()));
  }

  get visible(): boolean {
    return this.authService.hasRole('VendorAdmin') || this.authService.hasRole('Customer')
      || (!this.authService.isLoggedIn && this.guestOrders.codes.length > 0);
  }

  get link(): string {
    if (this.authService.hasRole('VendorAdmin')) return '/vendor-orders';
    return this.authService.isLoggedIn ? '/my-orders' : '/prati';
  }

  private animate() {
    this.bump = false;
    clearTimeout(this.bumpTimer);
    // Sledeći frame, da bi animacija krenula ispočetka
    setTimeout(() => this.bump = true);
    this.bumpTimer = setTimeout(() => this.bump = false, 1200);
  }

  ngOnDestroy() {
    this.sub.unsubscribe();
    clearTimeout(this.bumpTimer);
  }
}

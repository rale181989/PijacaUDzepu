import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { AlertController, ToastController } from '@ionic/angular/lazy';
import { CartService } from '../services/cart.service';
import { OrderService } from '../services/order.service';
import { AuthService } from '../services/auth.service';
import { VendorService } from '../services/vendor.service';
import { MarketService } from '../services/market.service';
import { GeoService } from '../services/geo.service';
import { CartItem } from '../models/cart.model';
import { Vendor } from '../models/vendor.model';
import { Market } from '../models/market.model';
import { OrderInput, GuestOrderInput } from '../models/order.model';
import { environment } from '../../environments/environment';

interface VendorCheck {
  vendorName: string;
  serviceLabel: { icon: string; text: string; color: string } | null;
  minOrderAmount?: number;
  subtotal: number;
  deficit: number;
  deliveryRadiusKm?: number;
  distanceKm?: number;
  tooFar: boolean;
  blocked: boolean;
}

@Component({
  selector: 'app-cart',
  templateUrl: './cart.page.html',
  styleUrls: ['./cart.page.scss'],
  standalone: false,
})
export class CartPage implements OnInit {
  items: CartItem[] = [];
  vendors: Vendor[] = [];
  markets: Market[] = [];
  note = '';
  submitting = false;
  apiUrl = environment.apiUrl.replace('/api', '');

  guestName = '';
  guestPhone = '';
  guestAddress = '';
  deliveryAddress = '';

  vendorChecks: Record<number, VendorCheck> = {};
  customerPos: { lat: number; lng: number } | null = null;
  geoStatus: 'pending' | 'ok' | 'denied' = 'pending';

  get hasDeliveryVendors(): boolean {
    return this.items.some(i => {
      const v = this.vendors.find(vn => vn.id === i.product.vendorId);
      return v?.offersDelivery;
    });
  }

  get isBlocked(): boolean {
    return Object.values(this.vendorChecks).some(c => c.blocked);
  }

  constructor(
    public cartService: CartService,
    public authService: AuthService,
    private orderService: OrderService,
    private vendorService: VendorService,
    private marketService: MarketService,
    private geoService: GeoService,
    private router: Router,
    private alertController: AlertController,
    private toastController: ToastController
  ) {}

  ngOnInit() {
    this.vendorService.getAll().subscribe(v => {
      this.vendors = v;
      this.rebuildChecks();
    });
    this.marketService.getAll().subscribe(m => {
      this.markets = m;
      this.rebuildChecks();
    });
    this.cartService.cart$.subscribe(items => {
      this.items = items;
      this.rebuildChecks();
    });
    this.requestGeolocation();
  }

  async requestGeolocation() {
    this.geoStatus = 'pending';
    const pos = await this.geoService.getCurrentPosition();
    if (pos) {
      this.customerPos = pos;
      this.geoStatus = 'ok';
    } else {
      this.geoStatus = 'denied';
    }
    this.rebuildChecks();
  }

  async onAddressChange() {
    const addr = this.authService.isLoggedIn ? this.deliveryAddress : this.guestAddress;
    if (!addr || addr.trim().length < 5) {
      this.customerPos = null;
      this.rebuildChecks();
      return;
    }
    const pos = await this.geoService.geocode(addr.trim());
    if (pos) {
      this.customerPos = pos;
      this.geoStatus = 'ok';
    }
    this.rebuildChecks();
  }

  rebuildChecks() {
    const checks: Record<number, VendorCheck> = {};
    const vendorSubtotals: Record<number, number> = {};

    for (const item of this.items) {
      const vid = item.product.vendorId;
      vendorSubtotals[vid] = (vendorSubtotals[vid] ?? 0) + item.product.price * item.quantity;
    }

    const shortNames: Record<number, string> = { 1: 'pon', 2: 'uto', 3: 'sre', 4: 'čet', 5: 'pet', 6: 'sub', 7: 'ned' };

    for (const vid of Object.keys(vendorSubtotals).map(Number)) {
      const v = this.vendors.find(vn => vn.id === vid);
      if (!v) continue;

      let serviceLabel: VendorCheck['serviceLabel'] = null;
      if (v.acceptsReservations && v.offersDelivery) {
        serviceLabel = { icon: 'bag-check-outline', text: 'Lično preuzimanje ili dostava', color: 'primary' };
      } else if (v.acceptsReservations) {
        serviceLabel = { icon: 'bag-check-outline', text: 'Lično preuzimanje', color: 'primary' };
      } else if (v.offersDelivery) {
        let text = 'Dostava';
        if (v.deliverySchedule && v.deliverySchedule.length > 0) {
          const days = v.deliverySchedule.map(d => (shortNames[d.day] ?? '') + ' ' + d.from + '-' + d.to).join(', ');
          text += ': ' + days;
        }
        serviceLabel = { icon: 'car-outline', text, color: 'success' };
      }

      const subtotal = vendorSubtotals[vid];
      const minOrder = v.offersDelivery ? (v.minOrderAmount ?? 0) : 0;
      const deficit = Math.max(0, minOrder - subtotal);

      let distanceKm: number | undefined;
      let tooFar = false;

      if (v.offersDelivery && v.deliveryRadiusKm && this.customerPos) {
        const market = this.markets.find(m => m.id === v.marketId);
        if (market?.address) {
          const cached = this.geoService['geocodeCache']?.get(market.address.trim().toLowerCase());
          if (cached) {
            distanceKm = Math.round(this.geoService.distanceKm(
              this.customerPos.lat, this.customerPos.lng, cached.lat, cached.lng
            ) * 10) / 10;
            tooFar = distanceKm > v.deliveryRadiusKm;
          } else {
            this.geoService.geocode(market.address).then(() => this.rebuildChecks());
          }
        }
      }

      const deliveryOnly = v.offersDelivery && !v.acceptsReservations;
      const blocked = deliveryOnly && (deficit > 0 || tooFar);

      checks[vid] = {
        vendorName: v.name,
        serviceLabel,
        minOrderAmount: minOrder > 0 ? minOrder : undefined,
        subtotal,
        deficit,
        deliveryRadiusKm: v.deliveryRadiusKm ?? undefined,
        distanceKm,
        tooFar,
        blocked
      };
    }

    this.vendorChecks = checks;
  }

  updateQuantity(productId: number, quantity: number) {
    this.cartService.updateQuantity(productId, quantity);
  }

  removeItem(productId: number) {
    this.cartService.removeFromCart(productId);
  }

  async clearCart() {
    const alert = await this.alertController.create({
      header: 'Isprazni korpu',
      message: 'Da li ste sigurni da želite da ispraznite korpu?',
      buttons: [
        { text: 'Otkaži', role: 'cancel' },
        { text: 'Isprazni', role: 'destructive', handler: () => this.cartService.clearCart() }
      ]
    });
    await alert.present();
  }

  getUnitLabel(unit: string): string {
    switch (unit) {
      case 'Kg': return 'kg';
      case 'Gram': return 'g';
      case 'Komad': return 'kom';
      case 'Veza': return 'veza';
      case 'Litar': return 'l';
      case 'Pakovanje': return 'pak';
      case 'Kutija': return 'kut';
      case 'Flasa': return 'flaša';
      default: return unit;
    }
  }

  getImageUrl(imageUrl?: string): string {
    if (!imageUrl) return 'assets/placeholder.svg';
    if (imageUrl.startsWith('http')) return imageUrl;
    return this.apiUrl + imageUrl;
  }

  get canGuestOrder(): boolean {
    return this.guestName.trim().length > 0 && this.guestPhone.trim().length > 0;
  }

  async submitOrder() {
    if (this.items.length === 0 || this.isBlocked) return;

    if (!this.authService.isLoggedIn && !this.canGuestOrder) return;

    const alert = await this.alertController.create({
      header: 'Potvrda porudžbine',
      message: `Ukupno: ${this.cartService.total.toFixed(0)} din. Da li želite da naručite?`,
      buttons: [
        { text: 'Otkaži', role: 'cancel' },
        {
          text: 'Naruči',
          handler: () => this.placeOrder()
        }
      ]
    });
    await alert.present();
  }

  private placeOrder() {
    this.submitting = true;

    const cartItems = this.items.map(i => ({
      productId: i.product.id,
      quantity: i.quantity
    }));

    const obs = this.authService.isLoggedIn
      ? this.orderService.createOrder({
          items: cartItems,
          note: this.note || undefined
        })
      : this.orderService.createGuestOrder({
          items: cartItems,
          note: this.note || undefined,
          guestName: this.guestName.trim(),
          guestPhone: this.guestPhone.trim(),
          guestAddress: this.guestAddress.trim() || undefined
        });

    obs.subscribe({
      next: async () => {
        this.submitting = false;
        this.cartService.clearCart();
        this.note = '';
        this.guestName = '';
        this.guestPhone = '';
        this.guestAddress = '';
        this.deliveryAddress = '';
        const toast = await this.toastController.create({
          message: 'Porudžbina je uspešno kreirana!',
          duration: 3000,
          color: 'success'
        });
        await toast.present();
        if (this.authService.isLoggedIn) {
          this.router.navigateByUrl('/my-orders');
        } else {
          this.router.navigateByUrl('/home');
        }
      },
      error: async (err) => {
        this.submitting = false;
        const toast = await this.toastController.create({
          message: err.error?.message || 'Greška pri kreiranju porudžbine',
          duration: 3000,
          color: 'danger'
        });
        await toast.present();
      }
    });
  }
}

import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { AlertController, ToastController } from '@ionic/angular/lazy';
import { CartService } from '../services/cart.service';
import { OrderService } from '../services/order.service';
import { AuthService } from '../services/auth.service';
import { CartItem } from '../models/cart.model';
import { ProductUnit, quantityStep, roundQty } from '../models/product.model';
import { FulfillmentType } from '../models/order.model';
import { environment } from '../../environments/environment';
import { TranslateService } from '../services/translate.service';
import { GuestOrdersService } from '../services/guest-orders.service';
import { deliveryAddressOf } from '../models/user.model';
import { NotificationService } from '../services/notification.service';
import { ProductService } from '../services/product.service';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';

@Component({
  selector: 'app-cart',
  templateUrl: './cart.page.html',
  styleUrls: ['./cart.page.scss'],
  standalone: false,
})
export class CartPage implements OnInit {
  items: CartItem[] = [];
  note = '';
  submitting = false;
  apiUrl = environment.apiUrl.replace('/api', '');

  guestName = '';
  guestEmail = '';
  phone = '';
  address = '';
  courierFee = 0;

  constructor(
    public cartService: CartService,
    public authService: AuthService,
    private orderService: OrderService,
    private router: Router,
    private alertController: AlertController,
    private toastController: ToastController,
    private ts: TranslateService,
    private guestOrders: GuestOrdersService,
    private notificationService: NotificationService,
    private productService: ProductService
  ) {}

  ngOnInit() {
    this.cartService.cart$.subscribe(items => this.items = items);
    this.phone = this.authService.currentUser?.phoneNumber ?? '';
    this.address = deliveryAddressOf(this.authService.currentUser);
    // Sačuvani korisnik je možda od pre nego što je adresa postojala; ne prepisuje ono što je već upisano
    if (this.authService.isLoggedIn) {
      this.authService.loadCurrentUser().subscribe({
        next: user => {
          if (!this.phone.trim()) this.phone = user.phoneNumber ?? '';
          if (!this.address.trim()) this.address = deliveryAddressOf(user);
        },
        error: () => {}
      });
    }
    this.orderService.getDeliveryOptions().subscribe({ next: o => this.courierFee = o.courierFee, error: () => {} });
  }

  ionViewWillEnter() {
    this.refreshProducts();
  }

  // Korpa pamti proizvod iz trenutka dodavanja: pre poručivanja se osvežavaju cena, stanje i dostupnost,
  // a kupac dobija poruku ako se cena promenila (server ionako odbija porudžbinu sa starom cenom)
  refreshProducts() {
    const items = this.cartService.getItems();
    if (items.length === 0) return;
    forkJoin(items.map(i => this.productService.getById(i.product.id).pipe(catchError(() => of(null))))).subscribe(products => {
      const result = this.cartService.updateProducts(products.filter((p): p is NonNullable<typeof p> => !!p));
      const parts: string[] = [];
      if (result.priceChanged.length) parts.push(`${this.ts.t('cart.priceChanged')}: ${result.priceChanged.join(', ')}`);
      if (result.unavailable.length) parts.push(`${this.ts.t('cart.nowUnavailable')}: ${result.unavailable.join(', ')}`);
      if (parts.length) {
        this.toastController.create({ message: parts.join('. '), duration: 5000, color: 'warning' }).then(t => t.present());
      }
    });
  }

  async updateQuantity(productId: number, quantity: number) {
    const value = roundQty(quantity);
    // Na nuli stavka nestaje: kupac prvo potvrdi (lako se slučajno dodirne − jednom previše)
    if (value <= 0) {
      const item = this.items.find(i => i.product.id === productId);
      const alert = await this.alertController.create({
        header: this.ts.t('cart.removeTitle'),
        message: this.ts.t('cart.removeText').replace('{0}', item?.product.name ?? ''),
        buttons: [
          { text: this.ts.t('cart.removeNo'), role: 'cancel' },
          { text: this.ts.t('cart.removeYes'), role: 'confirm' }
        ]
      });
      await alert.present();
      if ((await alert.onDidDismiss()).role !== 'confirm') return;
    }
    this.cartService.updateQuantity(productId, value);
  }

  // Stara korpa (pre pravila "jedna pijaca") sa robom sa više pijaca: ostaje pijaca prve stavke
  keepFirstMarket() {
    const marketId = this.cartService.marketId;
    for (const item of this.items.filter(i => i.product.marketId !== marketId))
      this.cartService.removeFromCart(item.product.id);
  }

  // Korak dugmića − i +: 0,5 za kg i litar, 1 za ostalo
  step(unit: ProductUnit): number {
    return quantityStep(unit);
  }

  removeItem(productId: number) {
    this.cartService.removeFromCart(productId);
  }

  async clearCart() {
    const alert = await this.alertController.create({
      header: this.ts.t('cart.clear'),
      message: this.ts.t('cart.clearConfirm'),
      buttons: [
        { text: this.ts.t('cart.cancelBtn'), role: 'cancel' },
        { text: this.ts.t('cart.clear'), role: 'destructive', handler: () => this.cartService.clearCart() }
      ]
    });
    await alert.present();
  }

  // Uz količinu: "2 veze", "5 veza"
  getUnitLabel(unit: string, qty?: number): string {
    return this.ts.unit(unit, qty);
  }

  getImageUrl(imageUrl?: string): string {
    if (!imageUrl) return 'assets/placeholder.svg';
    if (imageUrl.startsWith('http')) return imageUrl;
    return this.apiUrl + imageUrl;
  }

  // Korpa se deli na porudžbinu po prodavcu, a kurir za svakog ide posebno
  get vendorCount(): number {
    return new Set(this.items.map(i => i.product.vendorId)).size;
  }

  get totalCourierFee(): number {
    return this.courierFee * this.vendorCount;
  }

  // Različite pijace u korpi: kod preuzimanja kupac ide na više mesta
  get marketNames(): string[] {
    return [...new Set(this.items.map(i => i.product.marketName).filter(Boolean))];
  }

  // Šta još fali za poručivanje (dugmad su inače siva bez objašnjenja)
  get missingFields(): string[] {
    const missing: string[] = [];
    if (!this.authService.isLoggedIn && !this.guestName.trim()) missing.push(this.ts.t('cart.missingName'));
    if (!this.phone.trim()) missing.push(this.ts.t('cart.missingPhone'));
    if (!this.emailValid) missing.push(this.ts.t('cart.missingEmail'));
    if (!this.address.trim()) missing.push(this.ts.t('cart.missingAddress'));
    return missing;
  }

  // E-mail nije obavezan, ali ako je unet mora biti ispravan
  get emailValid(): boolean {
    const email = this.guestEmail.trim();
    return !email || /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email);
  }

  get canReserve(): boolean {
    const hasName = this.authService.isLoggedIn || this.guestName.trim().length > 0;
    // Porudžbina je sa jedne pijace (server bi je inače odbio)
    return this.items.length > 0 && this.marketNames.length <= 1 && hasName && this.phone.trim().length > 0 && this.emailValid;
  }

  get canCourier(): boolean {
    // Adresa mora samo da bude upisana; oblik se ne proverava
    return this.canReserve && this.address.trim().length > 0;
  }

  async submitOrder(fulfillment: FulfillmentType) {
    if (this.submitting) return;
    if (fulfillment === 'Pickup' ? !this.canReserve : !this.canCourier) return;

    const goods = this.cartService.total;
    const message = fulfillment === 'Pickup'
      ? `${this.ts.t('cart.reserveConfirm')} ${this.ts.t('cart.total')} ${this.ts.num(goods, 0)} ${this.ts.t('din')}.`
      : `${this.ts.t('cart.goods')}: ${this.ts.num(goods, 0)} ${this.ts.t('din')} + ${this.ts.t('cart.ecoCourier')}: ${this.ts.num(this.totalCourierFee, 0)} ${this.ts.t('din')} = ${this.ts.num(goods + this.totalCourierFee, 0)} ${this.ts.t('din')}. ${this.ts.t('cart.orderConfirm')}`;

    const alert = await this.alertController.create({
      header: fulfillment === 'Pickup' ? this.ts.t('cart.reserve') : this.ts.t('cart.confirmTitle'),
      message,
      buttons: [
        { text: this.ts.t('cart.cancelBtn'), role: 'cancel' },
        {
          text: fulfillment === 'Pickup' ? this.ts.t('cart.reserve') : this.ts.t('cart.order'),
          handler: () => this.placeOrder(fulfillment)
        }
      ]
    });
    await alert.present();
  }

  private placeOrder(fulfillment: FulfillmentType) {
    // Dupli dodir na "Naruči" u potvrdi: druga porudžbina ne sme da ode
    if (this.submitting) return;
    this.submitting = true;

    const cartItems = this.items.map(i => ({
      productId: i.product.id,
      quantity: i.quantity,
      expectedUnitPrice: i.product.price
    }));

    const obs = this.authService.isLoggedIn
      ? this.orderService.createOrder({
          items: cartItems,
          note: this.note || undefined,
          fulfillment,
          contactPhone: this.phone.trim(),
          deliveryAddress: this.address.trim() || undefined
        })
      : this.orderService.createGuestOrder({
          items: cartItems,
          note: this.note || undefined,
          guestName: this.guestName.trim(),
          guestPhone: this.phone.trim(),
          guestEmail: this.guestEmail.trim() || undefined,
          guestAddress: this.address.trim() || undefined,
          fulfillment
        });

    obs.subscribe({
      next: async orders => {
        this.submitting = false;
        const usedAddress = this.address.trim(); // pre nego što se polje vrati na adresu iz naloga
        this.cartService.clearCart();
        // Crveni broj na ikonici "Moje porudžbine" odmah raste
        this.notificationService.loadCustomerActiveCount();
        this.note = '';
        this.guestName = '';
        this.address = deliveryAddressOf(this.authService.currentUser);
        const toast = await this.toastController.create({
          message: fulfillment === 'Pickup' ? this.ts.t('cart.reserveSuccess') : this.ts.t('cart.orderSuccess'),
          duration: 3000,
          color: 'success'
        });
        await toast.present();
        const trackingCode = orders[0]?.trackingCode;
        if (this.authService.isLoggedIn) {
          this.router.navigateByUrl('/my-orders');
        } else if (trackingCode) {
          this.guestOrders.add(trackingCode);
          if (fulfillment === 'Courier' && usedAddress) this.guestOrders.rememberAddress(trackingCode, usedAddress);
          this.router.navigateByUrl('/prati/' + trackingCode + '?nova=1');
        } else {
          this.router.navigateByUrl('/home');
        }
      },
      error: async (err) => {
        this.submitting = false;
        // Najčešće se promenila cena ili stanje: korpa odmah pokazuje nove podatke
        this.refreshProducts();
        const toast = await this.toastController.create({
          message: err.error?.message || this.ts.t('cart.orderError'),
          duration: 5000,
          color: 'danger'
        });
        await toast.present();
      }
    });
  }
}

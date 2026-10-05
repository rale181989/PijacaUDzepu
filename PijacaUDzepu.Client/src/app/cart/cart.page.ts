import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { AlertController, ToastController } from '@ionic/angular/lazy';
import { CartService } from '../services/cart.service';
import { OrderService } from '../services/order.service';
import { AuthService } from '../services/auth.service';
import { CartItem } from '../models/cart.model';
import { OrderInput, GuestOrderInput } from '../models/order.model';
import { environment } from '../../environments/environment';

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
  guestPhone = '';
  guestAddress = '';

  constructor(
    public cartService: CartService,
    public authService: AuthService,
    private orderService: OrderService,
    private router: Router,
    private alertController: AlertController,
    private toastController: ToastController
  ) {}

  ngOnInit() {
    this.cartService.cart$.subscribe(items => this.items = items);
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
      case 'Komad': return 'kom';
      case 'Veza': return 'veza';
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
    if (this.items.length === 0) return;

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

import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';
import { CartItem } from '../models/cart.model';
import { Product } from '../models/product.model';

@Injectable({ providedIn: 'root' })
export class CartService {
  private items: CartItem[] = [];
  private cartSource = new BehaviorSubject<CartItem[]>([]);
  cart$ = this.cartSource.asObservable();

  constructor() {
    this.loadFromStorage();
  }

  get itemCount(): number {
    return this.items.reduce((sum, item) => sum + item.quantity, 0);
  }

  get total(): number {
    return this.items.reduce((sum, item) => sum + item.product.price * item.quantity, 0);
  }

  addToCart(product: Product, quantity: number = 1) {
    const existing = this.items.find(i => i.product.id === product.id);
    if (existing) {
      existing.quantity = this.limit(product, existing.quantity + quantity);
    } else {
      this.items.push({ product, quantity: this.limit(product, quantity) });
    }
    this.save();
  }

  updateQuantity(productId: number, quantity: number) {
    const item = this.items.find(i => i.product.id === productId);
    if (item) {
      item.quantity = this.limit(item.product, quantity);
      if (item.quantity <= 0) {
        this.removeFromCart(productId);
        return;
      }
    }
    this.save();
  }

  removeFromCart(productId: number) {
    this.items = this.items.filter(i => i.product.id !== productId);
    this.save();
  }

  clearCart() {
    this.items = [];
    this.save();
  }

  getQuantity(productId: number): number {
    return this.items.find(i => i.product.id === productId)?.quantity ?? 0;
  }

  getItems(): CartItem[] {
    return [...this.items];
  }

  // Sveži podaci sa servera (cena, stanje, dostupnost): korpa ne sme da pokazuje cenu koju više nema.
  // Vraća nazive proizvoda kojima se cena promenila i one koji više nisu dostupni.
  updateProducts(fresh: Product[]): { priceChanged: string[]; unavailable: string[] } {
    const priceChanged: string[] = [];
    const unavailable: string[] = [];
    for (const item of this.items) {
      const p = fresh.find(f => f.id === item.product.id);
      if (!p) continue;
      if (p.price !== item.product.price) priceChanged.push(p.name);
      if (!p.isAvailable) unavailable.push(p.name);
      item.product = p;
      item.quantity = this.limit(p, item.quantity);
    }
    this.items = this.items.filter(i => i.quantity > 0);
    this.save();
    return { priceChanged, unavailable };
  }

  // Ne više od stanja (kad prodavac prati stanje)
  private limit(product: Product, quantity: number): number {
    const stock = product.stockQuantity;
    return stock != null ? Math.min(quantity, stock) : quantity;
  }

  private save() {
    try {
      localStorage.setItem('cart', JSON.stringify(this.items));
    } catch {}
    this.cartSource.next([...this.items]);
  }

  private loadFromStorage() {
    try {
      const stored = localStorage.getItem('cart');
      if (stored) {
        this.items = JSON.parse(stored);
        this.cartSource.next([...this.items]);
      }
    } catch {
      this.items = [];
    }
  }
}

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
      existing.quantity += quantity;
    } else {
      this.items.push({ product, quantity });
    }
    this.save();
  }

  updateQuantity(productId: number, quantity: number) {
    const item = this.items.find(i => i.product.id === productId);
    if (item) {
      item.quantity = quantity;
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

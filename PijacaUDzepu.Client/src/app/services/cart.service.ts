import { Injectable } from '@angular/core';
import { AlertController } from '@ionic/angular/lazy';
import { BehaviorSubject } from 'rxjs';
import { CartItem } from '../models/cart.model';
import { Product } from '../models/product.model';
import { TranslateService } from './translate.service';

@Injectable({ providedIn: 'root' })
export class CartService {
  private items: CartItem[] = [];
  private cartSource = new BehaviorSubject<CartItem[]>([]);
  cart$ = this.cartSource.asObservable();

  constructor(private alertController: AlertController, private ts: TranslateService) {
    this.loadFromStorage();
  }

  // Jedna porudžbina = jedna pijaca (kurir ne obilazi više pijaca, kupac ne preuzima na više mesta).
  // Pijaca robe u korpi; null kada je korpa prazna.
  get marketId(): number | null {
    return this.items[0]?.product.marketId ?? null;
  }

  get marketName(): string {
    return this.items[0]?.product.marketName ?? '';
  }

  // Korpa je sa jedne pijace, a proizvod sa druge
  isOtherMarket(product: Product): boolean {
    return this.marketId != null && product.marketId !== this.marketId;
  }

  // Dodaje u korpu; proizvod sa druge pijace tek kada kupac pristane da isprazni korpu. Vraća da li je dodat.
  async addOrAsk(product: Product, quantity: number = 1): Promise<boolean> {
    if (this.isOtherMarket(product)) {
      const alert = await this.alertController.create({
        header: this.ts.t('cart.oneMarketTitle'),
        message: this.ts.t('cart.oneMarketText').replace('{0}', this.marketName).replace('{1}', product.marketName),
        buttons: [
          { text: this.ts.t('cart.oneMarketKeep'), role: 'cancel' },
          { text: this.ts.t('cart.oneMarketSwitch'), role: 'confirm' }
        ]
      });
      await alert.present();
      const { role } = await alert.onDidDismiss();
      if (role !== 'confirm') return false;
      this.clearCart();
    }
    this.addToCart(product, quantity);
    return true;
  }

  get itemCount(): number {
    return this.items.reduce((sum, item) => sum + item.quantity, 0);
  }

  get total(): number {
    return this.items.reduce((sum, item) => sum + item.product.price * item.quantity, 0);
  }

  // Bez provere pijace; sa ekrana se dodaje preko addOrAsk
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

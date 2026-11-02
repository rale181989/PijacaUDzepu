import { Injectable } from '@angular/core';
import { Router } from '@angular/router';
import { ModalController, ToastController } from '@ionic/angular/lazy';
import { firstValueFrom } from 'rxjs';
import { Order, OrderRating } from '../models/order.model';
import { OrderService } from './order.service';
import { ProductService } from './product.service';
import { CartService } from './cart.service';
import { TranslateService } from './translate.service';
import { RatingFormComponent, RatingResult } from '../shared/rating-form/rating-form.component';

// Zajedničke radnje nad porudžbinom kupca (Moje porudžbine, detalji, praćenje gosta)
@Injectable({ providedIn: 'root' })
export class OrderActionsService {
  constructor(
    private orderService: OrderService,
    private productService: ProductService,
    private cartService: CartService,
    private modalController: ModalController,
    private toastController: ToastController,
    private router: Router,
    private ts: TranslateService
  ) {}

  canRate(order: Order): boolean {
    return order.status === 'Completed' && !order.rating;
  }

  // Otvara ocenu; vraća sačuvanu ocenu ili null ako je kupac odustao. Gost šalje kod za praćenje.
  async rate(order: Order, trackingCode?: string): Promise<OrderRating | null> {
    const modal = await this.modalController.create({
      component: RatingFormComponent,
      componentProps: { vendorName: order.vendorName, withCourier: !!order.courierName },
      breakpoints: [0, 0.75, 1],
      initialBreakpoint: 0.75
    });
    await modal.present();
    const { data, role } = await modal.onWillDismiss<RatingResult>();
    if (role !== 'confirm' || !data) return null;

    try {
      const updated = await firstValueFrom(trackingCode
        ? this.orderService.rateTracked(trackingCode, order.id, data)
        : this.orderService.rate(order.id, data));
      await this.toast(this.ts.t('rating.thanks'), 'success');
      return updated.rating ?? null;
    } catch (err: any) {
      await this.toast(err.error?.message || this.ts.t('order.error'), 'danger');
      return null;
    }
  }

  // "Poruči ponovo": iste stavke u korpu, samo ono što je i dalje dostupno i dok tezga radi
  async reorder(order: Order) {
    const added: string[] = [];
    const missing: string[] = [];
    for (const item of order.items) {
      if (!item.productId) { missing.push(item.productName); continue; }
      try {
        const product = await firstValueFrom(this.productService.getById(item.productId));
        if (product.isAvailable && product.vendorOpen !== false) {
          this.cartService.addToCart(product, item.quantity);
          added.push(product.name);
        } else {
          missing.push(item.productName);
        }
      } catch {
        missing.push(item.productName);
      }
    }

    if (added.length === 0) {
      await this.toast(this.ts.t('reorder.none'), 'warning');
      return;
    }
    const text = missing.length
      ? `${this.ts.t('reorder.added')} ${this.ts.t('reorder.missing')}: ${missing.join(', ')}`
      : this.ts.t('reorder.added');
    await this.toast(text, missing.length ? 'warning' : 'success');
    this.router.navigateByUrl('/cart');
  }

  private async toast(message: string, color: string) {
    const t = await this.toastController.create({ message, duration: 3500, color, position: 'top' });
    await t.present();
  }
}

import { Component } from '@angular/core';
import { OrderService, VendorToday } from '../services/order.service';
import { TranslateService } from '../services/translate.service';
import { ordersWord } from '../shared/plural';

// Pregled dana za prodavca: šta treba spremiti (zbir stavki porudžbina u toku), stanje i današnji promet
@Component({
  selector: 'app-vendor-today',
  templateUrl: './vendor-today.page.html',
  styleUrls: ['./vendor-today.page.scss'],
  standalone: false,
})
export class VendorTodayPage {
  data: VendorToday | null = null;
  loading = false;
  readonly ordersWord = ordersWord;

  constructor(private orderService: OrderService, private ts: TranslateService) {}

  ionViewWillEnter() {
    this.load();
  }

  load() {
    this.loading = true;
    this.orderService.getVendorToday().subscribe({
      next: d => { this.data = d; this.loading = false; },
      error: () => this.loading = false
    });
  }

  doRefresh(event: any) {
    this.load();
    setTimeout(() => event.target.complete(), 500);
  }

  unit(u: string): string {
    return this.ts.t('unit.' + u) || u;
  }
}

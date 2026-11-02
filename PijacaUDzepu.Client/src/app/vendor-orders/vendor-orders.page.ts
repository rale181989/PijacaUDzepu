import { Component, OnDestroy, OnInit, ViewChild } from '@angular/core';
import { OrderService } from '../services/order.service';
import { NotificationService } from '../services/notification.service';
import { Order, OrderStatus } from '../models/order.model';
import { AlertController, IonContent, ToastController } from '@ionic/angular/lazy';
import { Subscription } from 'rxjs';
import { PagerComponent } from '../shared/pager/pager.component';

@Component({
  selector: 'app-vendor-orders',
  templateUrl: './vendor-orders.page.html',
  styleUrls: ['./vendor-orders.page.scss'],
  standalone: false,
})
export class VendorOrdersPage implements OnInit, OnDestroy {
  @ViewChild(IonContent) content?: IonContent;

  orders: Order[] = [];
  private sub?: Subscription;
  private loadSub?: Subscription;
  loading = false;

  // Paginacija: strana (od 0), veličina strane (10 ili 20, pamti se na uređaju) i ukupan broj
  readonly pageSizeKey = 'vendorOrdersPageSize';
  pageSize = PagerComponent.savedSize(this.pageSizeKey);
  page = 0;
  totalCount = 0;

  get pageCount(): number {
    return Math.max(1, Math.ceil(this.totalCount / this.pageSize));
  }

  constructor(
    private orderService: OrderService,
    private notificationService: NotificationService,
    private alertController: AlertController,
    private toastController: ToastController
  ) {}

  ngOnInit() {
    this.loadOrders();
    // Nova porudžbina, dodeljen kurir ili promena statusa od strane kurira
    this.sub = this.notificationService.orderUpdated$.subscribe(() => this.loadOrders());
    // Posle prekida veze (zaključan telefon) učitavaju se i porudžbine koje su stigle u međuvremenu
    this.sub.add(this.notificationService.resync$.subscribe(() => this.loadOrders()));
  }

  ngOnDestroy() {
    this.sub?.unsubscribe();
    this.loadSub?.unsubscribe();
  }
  ionViewWillEnter() {
    this.loadOrders();
    this.notificationService.loadPendingCount();
  }

  loadOrders() {
    // Novo učitavanje prekida prethodno i zamenjuje listu; inače se dva istovremena
    // učitavanja (ulazak na stranicu + obaveštenje) spoje i porudžbine se prikažu duplo
    this.loadSub?.unsubscribe();
    this.loading = true;
    this.loadSub = this.orderService.getVendorOrders(this.page * this.pageSize, this.pageSize).subscribe({
      next: result => {
        this.orders = result.items;
        this.totalCount = result.totalCount;
        this.loading = false;
        // Strana je ostala prazna (npr. posle promene veličine strane) - prikaži poslednju
        if (this.orders.length === 0 && this.page > 0 && this.totalCount > 0) {
          this.page = this.pageCount - 1;
          this.loadOrders();
        }
      },
      error: () => this.loading = false
    });
  }

  goToPage(page: number) {
    if (page < 0 || page >= this.pageCount || page === this.page) return;
    this.page = page;
    this.loadOrders();
    this.content?.scrollToTop(300);
  }

  setPageSize(size: number) {
    // Ostaje na strani koja sadrži prvu porudžbinu koja je bila prikazana
    this.page = PagerComponent.pageAfterResize(this.page, this.pageSize, size);
    this.pageSize = size;
    this.loadOrders();
  }

  async updateStatus(order: Order, newStatus: OrderStatus, message?: string) {
    this.orderService.updateStatus(order.id, newStatus, message).subscribe({
      next: async updated => {
        Object.assign(order, updated);
        this.notificationService.loadPendingCount();
        const toast = await this.toastController.create({
          message: `Status promenjen: ${this.getStatusLabel(newStatus)}`, duration: 2000, color: 'success'
        });
        await toast.present();
      },
      error: async err => {
        const toast = await this.toastController.create({
          message: err.error?.message || 'Greška', duration: 2000, color: 'danger'
        });
        await toast.present();
      }
    });
  }

  // Prodavac uz potvrdu, odbijanje ili "Spremno" može da napiše poruku kupcu
  async respond(order: Order, status: OrderStatus) {
    const pickup = order.fulfillment === 'Pickup';
    const titles: Record<string, string> = {
      'Confirmed': pickup ? 'Potvrdi rezervaciju' : 'Potvrdi porudžbinu',
      'Rejected': pickup ? 'Odbij rezervaciju' : 'Odbij porudžbinu',
      'ReadyForPickup': 'Roba je spremna',
    };
    const placeholders: Record<string, string> = {
      'Confirmed': pickup ? 'npr. Možete doći posle 10h, tezga 14' : 'npr. Pripremam, biće spremno za 30 min',
      'Rejected': 'npr. Nažalost, nestalo je robe',
      'ReadyForPickup': pickup ? 'npr. Spremno, čeka vas kod tezge' : 'npr. Spremno za kurira',
    };
    // Kod odbijanja je razlog obavezan: kupac ga dobija u aplikaciji, na stranici za praćenje i e-mailom
    const reject = status === 'Rejected';
    const hint = reject ? 'Upišite razlog odbijanja. Kupac će dobiti ovu poruku.' : undefined;
    const alert = await this.alertController.create({
      header: reject ? `Razlog odbijanja #${order.id}` : `${titles[status]} #${order.id}`,
      message: hint,
      inputs: [{
        name: 'message', type: 'textarea',
        placeholder: placeholders[status] + (reject ? '' : ' (opciono)'),
        attributes: { maxlength: 300 }
      }],
      buttons: [
        { text: 'Otkaži', role: 'cancel' },
        {
          text: reject ? 'Odbij' : 'Pošalji',
          role: reject ? 'destructive' : undefined,
          handler: data => {
            const message = data?.message?.trim();
            if (reject && !message) {
              alert.message = '⚠ Razlog je obavezan – kupac mora da zna zašto je porudžbina odbijena.';
              return false;
            }
            this.updateStatus(order, status, message || undefined);
            return true;
          }
        }
      ]
    });
    await alert.present();
  }

  getStatusLabel(status: string): string {
    const map: Record<string, string> = {
      'Pending': 'Na čekanju', 'Confirmed': 'Potvrđena', 'Rejected': 'Odbijena',
      'ReadyForPickup': 'Spremna', 'InDelivery': 'U dostavi', 'Completed': 'Završena', 'Cancelled': 'Otkazana'
    };
    return map[status] || status;
  }

  getStatusColor(status: string): string {
    const map: Record<string, string> = {
      'Pending': 'warning', 'Confirmed': 'primary', 'Rejected': 'danger',
      'ReadyForPickup': 'success', 'InDelivery': 'tertiary', 'Completed': 'medium', 'Cancelled': 'medium'
    };
    return map[status] || 'medium';
  }

  getUnitLabel(unit: string): string {
    switch (unit) { case 'Kg': return 'kg'; case 'Komad': return 'kom'; case 'Veza': return 'veza'; default: return unit; }
  }

  doRefresh(event: any) {
    this.loadOrders();
    setTimeout(() => event.target.complete(), 500);
  }
}

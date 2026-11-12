import { Component, OnDestroy, OnInit, ViewChild } from '@angular/core';
import { OrderService } from '../services/order.service';
import { NotificationService } from '../services/notification.service';
import { Order, OrderStatus } from '../models/order.model';
import { ActionSheetController, AlertController, IonContent, ToastController } from '@ionic/angular/lazy';
import { Subscription } from 'rxjs';
import { PagerComponent } from '../shared/pager/pager.component';
import { ordersWord, srPlural } from '../shared/plural';

@Component({
  selector: 'app-vendor-orders',
  templateUrl: './vendor-orders.page.html',
  styleUrls: ['./vendor-orders.page.scss'],
  standalone: false,
})
export class VendorOrdersPage implements OnInit, OnDestroy {
  @ViewChild(IonContent) content?: IonContent;

  orders: Order[] = [];
  // Aktivne (na čekanju, u pripremi, spremne, u dostavi) i završene (završene, odbijene, otkazane)
  tab: 'active' | 'finished' = 'active';
  activeCount: number | null = null;
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
    public notificationService: NotificationService,
    private alertController: AlertController,
    private actionSheetController: ActionSheetController,
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
    const finished = this.tab === 'finished';
    this.loadSub = this.orderService.getVendorOrders(finished, this.page * this.pageSize, this.pageSize).subscribe({
      next: result => {
        this.orders = result.items;
        this.totalCount = result.totalCount;
        if (!finished) this.activeCount = result.totalCount;
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

  selectTab(tab: 'active' | 'finished') {
    if (tab === this.tab) return;
    this.tab = tab;
    this.page = 0;
    this.orders = [];
    this.loadOrders();
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
        // Završena ili odbijena prelazi u tab "Završene"
        if (['Completed', 'Rejected', 'Cancelled'].includes(updated.status)) this.loadOrders();
        // Gore, da ne pokrije dugmad na kartici
        const toast = await this.toastController.create({
          message: `Status promenjen: ${this.getStatusLabel(newStatus)}`, duration: 2000, color: 'success', position: 'top'
        });
        await toast.present();
      },
      error: async err => {
        // Najčešće je status u međuvremenu promenio kurir: prikaži stvarno stanje
        this.loadOrders();
        const toast = await this.toastController.create({
          message: err.error?.message || 'Greška', duration: 3000, color: 'danger'
        });
        await toast.present();
      }
    });
  }

  // "Kupac preuzeo" završava porudžbinu i ne može da se vrati: prvo potvrda
  async confirmPickedUp(order: Order) {
    const alert = await this.alertController.create({
      header: `Kupac preuzeo #${order.id}?`,
      message: `${order.customerName} je preuzeo robu i platio ${order.amountToPay.toFixed(0)} din. Porudžbina prelazi u „Završene”.`,
      buttons: [
        { text: 'Nazad', role: 'cancel' },
        { text: 'Da, preuzeo', handler: () => this.updateStatus(order, 'Completed') }
      ]
    });
    await alert.present();
  }

  // Odbijanje: gotov razlog jednim dodirom ili svoj tekst
  async reject(order: Order) {
    const reasons = ['Nestalo je robe', 'Danas ne radim', 'Ne mogu da spremim na vreme'];
    const sheet = await this.actionSheetController.create({
      header: `Razlog odbijanja #${order.id}`,
      subHeader: 'Kupac će dobiti ovu poruku.',
      buttons: [
        ...reasons.map(reason => ({ text: reason, handler: () => { this.updateStatus(order, 'Rejected', reason); } })),
        { text: 'Drugi razlog…', handler: () => { this.respond(order, 'Rejected'); } },
        { text: 'Nazad', role: 'cancel' }
      ]
    });
    await sheet.present();
  }

  readonly ordersWord = ordersWord;

  // Prodavac uz potvrdu ili odbijanje može da napiše poruku kupcu ("Spremno" ide jednim dodirom)
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
        { text: 'Nazad', role: 'cancel' },
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
      'ReadyForPickup': 'Spremna', 'InDelivery': 'U dostavi', 'Completed': 'Završena', 'Cancelled': 'Otkazana', 'NotDelivered': 'Nije dostavljena'
    };
    return map[status] || status;
  }

  // Dostava bez kurira: kuriri dobijaju porudžbinu tek kada odgovore svi prodavci iz iste korpe
  courierHint(order: Order): string {
    if (order.status === 'Pending') return 'Kurir je dobija kada potvrdite (kod korpe sa više tezgi – kada odgovore sve)';
    return order.courierOfferedAt ? 'Čeka se kurir' : 'Kurir kreće kada odgovore i ostale tezge iz korpe';
  }

  getStatusColor(status: string): string {
    const map: Record<string, string> = {
      'Pending': 'warning', 'Confirmed': 'primary', 'Rejected': 'danger',
      'ReadyForPickup': 'success', 'InDelivery': 'tertiary', 'Completed': 'medium', 'Cancelled': 'medium', 'NotDelivered': 'danger'
    };
    return map[status] || 'medium';
  }

  // Uz količinu: "2 veze", "5 veza"
  getUnitLabel(unit: string, qty = 0): string {
    switch (unit) {
      case 'Kg': return 'kg';
      case 'Gram': return 'g';
      case 'Komad': return 'kom';
      case 'Veza': return srPlural(qty, 'veza', 'veze', 'veza');
      case 'Litar': return 'l';
      case 'Pakovanje': return 'pak';
      case 'Kutija': return 'kut';
      case 'Flasa': return srPlural(qty, 'flaša', 'flaše', 'flaša');
      default: return unit;
    }
  }

  doRefresh(event: any) {
    this.loadOrders();
    setTimeout(() => event.target.complete(), 500);
  }
}

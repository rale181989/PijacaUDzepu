import { Component, OnDestroy, OnInit } from '@angular/core';
import { AlertController, ToastController } from '@ionic/angular/lazy';
import { Observable, Subscription } from 'rxjs';
import { CourierService, CourierSettlement } from '../services/courier.service';
import { NotificationService } from '../services/notification.service';
import { TranslateService } from '../services/translate.service';
import { Order, OrderStatus } from '../models/order.model';
import { PagerComponent } from '../shared/pager/pager.component';
import { itemsWord } from '../shared/plural';

@Component({
  selector: 'app-courier-orders',
  templateUrl: './courier-orders.page.html',
  styleUrls: ['./courier-orders.page.scss'],
  standalone: false,
})
export class CourierOrdersPage implements OnInit, OnDestroy {
  tab: 'available' | 'mine' | 'finished' | 'settlement' = 'available';
  readonly itemsWord = itemsWord;
  settlement: CourierSettlement | null = null;
  settlementDate = '';
  acceptingGroup: string | null = null;
  available: Order[] = [];
  mine: Order[] = [];
  loading = false;
  acceptingId: number | null = null;
  // Završene dostave se učitavaju po stranama (vremenom ih ima mnogo)
  finished: Order[] = [];
  finishedPage = 0;
  finishedTotal = 0;
  readonly pageSizeKey = 'courierFinishedPageSize';
  finishedPageSize = PagerComponent.savedSize(this.pageSizeKey);
  decliningId: number | null = null;
  busyId: number | null = null;
  isOnShift = false;
  shiftLoaded = false;
  private sub?: Subscription;

  constructor(
    private courierService: CourierService,
    private notificationService: NotificationService,
    private alertController: AlertController,
    private toastController: ToastController,
    private ts: TranslateService
  ) {}

  ngOnInit() {
    this.sub = this.notificationService.courierEvents$.subscribe(event => {
      if (event.type === 'new') {
        if (!this.available.some(o => o.id === event.order.id)) {
          this.available = [event.order, ...this.available];
        }
      } else if (event.type === 'unavailable') {
        this.available = this.available.filter(o => o.id !== event.orderId);
      } else if (event.type === 'status') {
        // Moja dostava ili slobodna porudžbina za koju je prodavac javio "Spremno"
        const order = this.mine.find(o => o.id === event.orderId) ?? this.available.find(o => o.id === event.orderId);
        if (order) order.status = event.status as OrderStatus;
      }
    });
    // Posle prekida veze (zaključan telefon) učitavaju se i porudžbine koje su stigle u međuvremenu
    this.sub.add(this.notificationService.resync$.subscribe(() => this.load()));
  }

  ionViewWillEnter() {
    this.load();
  }

  ngOnDestroy() {
    this.sub?.unsubscribe();
  }

  load() {
    this.courierService.getProfile().subscribe(p => {
      this.isOnShift = p.isOnShift;
      this.shiftLoaded = true;
      this.notificationService.setCourierOnShift(p.isOnShift);
    });
    this.loading = true;
    let pending = 2;
    const done = () => { if (--pending === 0) this.loading = false; };
    this.courierService.getAvailableOrders().subscribe({
      next: orders => { this.available = orders; done(); },
      error: () => done()
    });
    this.loadFinished();
    this.courierService.getMyOrders().subscribe({
      next: r => { this.mine = r.items; done(); },
      error: () => done()
    });
    this.notificationService.loadCourierAvailableCount();
  }

  doRefresh(event: any) {
    this.load();
    setTimeout(() => event.target.complete(), 500);
  }

  toggleShift(value: boolean) {
    this.courierService.setShift(value).subscribe({
      next: async p => {
        this.isOnShift = p.isOnShift;
        this.notificationService.setCourierOnShift(p.isOnShift);
        await this.toast(p.isOnShift ? 'Na smeni ste – stižu vam nove porudžbine' : 'Van smene – nove porudžbine vam neće stizati', p.isOnShift ? 'success' : 'medium');
      },
      error: async () => {
        this.isOnShift = !value;
        await this.toast('Promena smene nije uspela', 'danger');
      }
    });
  }

  markPickedUp(order: Order) {
    this.runStep(order, this.courierService.markPickedUp(order.id), 'Roba preuzeta – srećan put!');
  }

  async markDelivered(order: Order) {
    const alert = await this.alertController.create({
      header: 'Potvrda isporuke',
      message: `Da li ste predali porudžbinu #${order.id} i naplatili ${order.amountToPay.toFixed(0)} din?`,
      buttons: [
        { text: 'Ne', role: 'cancel' },
        { text: 'Isporučeno', handler: () => this.runStep(order, this.courierService.markDelivered(order.id), 'Porudžbina isporučena') }
      ]
    });
    await alert.present();
  }

  // Kupac nije tu, pogrešna adresa...: razlog je obavezan, dobijaju ga kupac, prodavac i administrator
  async markNotDelivered(order: Order) {
    const alert = await this.alertController.create({
      header: `Nije dostavljeno #${order.id}`,
      message: 'Upišite razlog. Kupac i prodavac će ga dobiti, a robu vratite prodavcu.',
      inputs: [{ name: 'reason', type: 'textarea', placeholder: 'npr. Kupac se ne javlja na telefon', attributes: { maxlength: 300 } }],
      buttons: [
        { text: 'Otkaži', role: 'cancel' },
        {
          text: 'Potvrdi', role: 'destructive',
          handler: data => {
            const reason = data?.reason?.trim();
            if (!reason) {
              alert.message = '⚠ Razlog je obavezan – kupac i prodavac moraju da znaju zašto roba nije predata.';
              return false;
            }
            this.runStep(order, this.courierService.markNotDelivered(order.id, reason), 'Prijavljeno: porudžbina nije dostavljena');
            return true;
          }
        }
      ]
    });
    await alert.present();
  }

  private runStep(order: Order, step: Observable<Order>, success: string) {
    this.busyId = order.id;
    step.subscribe({
      next: async updated => {
        this.busyId = null;
        this.mine = this.mine.map(o => o.id === updated.id ? updated : o);
        await this.toast(success, 'success');
      },
      error: async err => {
        this.busyId = null;
        await this.toast(err.error?.message || 'Greška', 'danger');
      }
    });
  }

  accept(order: Order) {
    this.acceptingId = order.id;
    this.courierService.acceptOrder(order.id).subscribe({
      next: async accepted => {
        this.acceptingId = null;
        this.available = this.available.filter(o => o.id !== order.id);
        this.mine = [accepted, ...this.mine];
        this.tab = 'mine';
        this.notificationService.loadCourierAvailableCount();
        await this.toast(`Porudžbina #${order.id} je vaša`, 'success');
      },
      error: async err => {
        this.acceptingId = null;
        if (err.status === 409 || err.status === 404) {
          this.available = this.available.filter(o => o.id !== order.id);
        }
        await this.toast(err.error?.message || 'Greška pri prihvatanju', 'warning');
      }
    });
  }

  // Slobodne porudžbine grupisane po korpi: ista adresa, više tezgi -> "Prihvati sve"
  // Getteri vraćaju zapamćen rezultat dok se podaci ne promene: novi niz pri svakoj proveri promena bi naterao
  // *ngFor da stalno ponovo pravi Ionic kartice, što pokreće novu proveru - stranica bi se zaglavila
  private groupsCache?: { source: Order[]; value: { key: string; orders: Order[] }[] };
  private routesCache?: { key: string; value: { key: string; orders: Order[]; address?: string; mapsUrl: string }[] };

  get availableGroups(): { key: string; orders: Order[] }[] {
    if (this.groupsCache?.source === this.available) return this.groupsCache.value;
    const groups: { key: string; orders: Order[] }[] = [];
    for (const order of this.available) {
      const key = order.checkoutId ?? `order-${order.id}`;
      const group = groups.find(g => g.key === key);
      if (group) group.orders.push(order); else groups.push({ key, orders: [order] });
    }
    this.groupsCache = { source: this.available, value: groups };
    return groups;
  }

  acceptGroup(group: { key: string; orders: Order[] }) {
    this.acceptingGroup = group.key;
    this.courierService.acceptCheckout(group.key).subscribe({
      next: async accepted => {
        this.acceptingGroup = null;
        const ids = accepted.map(o => o.id);
        this.available = this.available.filter(o => !ids.includes(o.id));
        this.mine = [...accepted, ...this.mine.filter(o => !ids.includes(o.id))];
        this.tab = 'mine';
        this.notificationService.loadCourierAvailableCount();
        await this.toast(`Prihvaćeno: ${ids.map(id => '#' + id).join(', ')}`, 'success');
      },
      error: async err => {
        this.acceptingGroup = null;
        this.load();
        await this.toast(err.error?.message || 'Greška pri prihvatanju', 'warning');
      }
    });
  }

  // Aktivne dostave iste korpe (više tezgi) - ruta: tezge redom, pa kupac
  get routes(): { key: string; orders: Order[]; address?: string; mapsUrl: string }[] {
    const cacheKey = this.mine.map(o => `${o.id}:${o.status}`).join(',');
    if (this.routesCache?.key === cacheKey) return this.routesCache.value;
    const groups: { key: string; orders: Order[] }[] = [];
    for (const order of this.activeMine) {
      if (!order.checkoutId) continue;
      const group = groups.find(g => g.key === order.checkoutId);
      if (group) group.orders.push(order); else groups.push({ key: order.checkoutId, orders: [order] });
    }
    const routes = groups.filter(g => g.orders.length > 1).map(g => {
      const address = g.orders[0].deliveryAddress ?? undefined;
      const stops = [...new Set(g.orders.map(o => o.marketAddress || `${o.marketName}, Novi Sad`))];
      const url = 'https://www.google.com/maps/dir/?api=1&travelmode=driving'
        + `&destination=${encodeURIComponent(address ?? '')}`
        + `&waypoints=${encodeURIComponent(stops.join('|'))}`;
      return { ...g, address, mapsUrl: url };
    });
    this.routesCache = { key: cacheKey, value: routes };
    return routes;
  }

  trackByKey(_: number, item: { key: string }) {
    return item.key;
  }

  selectTab(tab: 'available' | 'mine' | 'finished' | 'settlement') {
    this.tab = tab;
    if (tab === 'settlement') this.loadSettlement();
    if (tab === 'finished') this.loadFinished();
  }

  loadSettlement() {
    this.courierService.getSettlement(this.settlementDate || undefined).subscribe({
      next: s => { this.settlement = s; this.settlementDate = s.date; },
      error: async () => this.toast('Obračun nije učitan', 'danger')
    });
  }

  // Porudžbina nestaje samo ovom kuriru i prestaje da mu zvoni; ostali kuriri je i dalje vide
  decline(order: Order) {
    this.decliningId = order.id;
    this.courierService.declineOrder(order.id).subscribe({
      next: async () => {
        this.decliningId = null;
        this.available = this.available.filter(o => o.id !== order.id);
        this.notificationService.loadCourierAvailableCount();
        await this.toast(`Porudžbina #${order.id} je odbijena`, 'medium');
      },
      error: async err => {
        this.decliningId = null;
        if (err.status === 404) {
          this.available = this.available.filter(o => o.id !== order.id);
          this.notificationService.loadCourierAvailableCount();
        }
        await this.toast(err.error?.message || 'Greška pri odbijanju', 'warning');
      }
    });
  }

  loadFinished() {
    this.courierService.getMyOrders(true, this.finishedPage * this.finishedPageSize, this.finishedPageSize).subscribe({
      next: r => {
        this.finished = r.items;
        this.finishedTotal = r.totalCount;
      },
      error: () => {}
    });
  }

  goToFinishedPage(page: number) {
    this.finishedPage = page;
    this.loadFinished();
  }

  setFinishedPageSize(size: number) {
    this.finishedPage = PagerComponent.pageAfterResize(this.finishedPage, this.finishedPageSize, size);
    this.finishedPageSize = size;
    this.loadFinished();
  }

  get activeMine(): Order[] {
    return this.mine.filter(o => this.isActive(o.status));
  }

  // Upravo isporučena dostava ostaje vidljiva dok se lista ne osveži
  get finishedMine(): Order[] {
    const justFinished = this.mine.filter(o => !this.isActive(o.status) && !this.finished.some(f => f.id === o.id));
    return [...justFinished, ...this.finished];
  }

  private isActive(status: OrderStatus): boolean {
    return status === 'Pending' || status === 'Confirmed' || status === 'ReadyForPickup' || status === 'InDelivery';
  }

  getStatusLabel(status: OrderStatus): string {
    const labels: Record<string, string> = {
      'Pending': 'Čeka potvrdu prodavca', 'Confirmed': 'Prodavac priprema', 'Rejected': 'Odbijena',
      'ReadyForPickup': 'Spremno za preuzimanje', 'InDelivery': 'U dostavi', 'Completed': 'Isporučena', 'Cancelled': 'Otkazana', 'NotDelivered': 'Nije dostavljena'
    };
    return labels[status] || status;
  }

  getStatusColor(status: OrderStatus): string {
    const colors: Record<string, string> = {
      'Pending': 'warning', 'Confirmed': 'primary', 'Rejected': 'danger',
      'ReadyForPickup': 'success', 'InDelivery': 'tertiary', 'Completed': 'medium', 'Cancelled': 'medium', 'NotDelivered': 'danger'
    };
    return colors[status] || 'medium';
  }

  // Uz količinu: "2 veze", "5 veza"
  getUnitLabel(unit: string, qty?: number): string {
    return this.ts.unit(unit, qty);
  }

  mapsUrl(address?: string): string {
    return 'https://www.google.com/maps/search/?api=1&query=' + encodeURIComponent(address ?? '');
  }

  private async toast(message: string, color: string) {
    const toast = await this.toastController.create({ message, duration: 2500, color });
    await toast.present();
  }
}

import { Component, OnDestroy, OnInit } from '@angular/core';
import { AlertController, ToastController } from '@ionic/angular/lazy';
import { Observable, Subscription } from 'rxjs';
import { CourierService, CourierSettlement } from '../services/courier.service';
import { NotificationService } from '../services/notification.service';
import { TranslateService } from '../services/translate.service';
import { Order, OrderStatus } from '../models/order.model';
import { GeoPoint, directionsTo, marketPoint, pointText, stallPoint } from '../shared/maps';
import { PagerComponent } from '../shared/pager/pager.component';
import { itemsWord, srPlural } from '../shared/plural';
import { forkJoin } from 'rxjs';

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

  // Potvrda pre preuzimanja: kupac odmah dobija poruku da je roba kod kurira, pa slučajan dodir ne sme da prođe
  async markPickedUp(order: Order) {
    const alert = await this.alertController.create({
      header: 'Preuzeli ste robu?',
      message: `#${order.id} · ${order.vendorName}${order.stallLabel ? ', tezga ' + order.stallLabel : ''}. Kupac će dobiti poruku da je roba kod vas.`,
      buttons: [
        { text: 'Ne', role: 'cancel' },
        { text: 'Preuzeo sam', handler: () => this.runStep(order, this.courierService.markPickedUp(order.id), 'Roba preuzeta – srećan put!') }
      ]
    });
    await alert.present();
  }

  // R4: kod kupca se cela tura predaje odjednom – jedna potvrda sa ukupnim iznosom
  async markRouteDelivered(route: { orders: Order[] }) {
    const orders = route.orders.filter(o => o.status === 'InDelivery');
    const total = orders.reduce((sum, o) => sum + o.amountToPay, 0);
    const alert = await this.alertController.create({
      header: 'Potvrda isporuke',
      message: `Predali ste svih ${orders.length} porudžbine kupcu i naplatili ukupno ${total.toLocaleString('sr-Latn')} din?`,
      buttons: [
        { text: 'Ne', role: 'cancel' },
        {
          text: 'Isporučeno', handler: () => {
            forkJoin(orders.map(o => this.courierService.markDelivered(o.id))).subscribe({
              next: async updated => {
                this.mine = this.mine.map(o => updated.find(u => u.id === o.id) ?? o);
                await this.toast('Tura isporučena', 'success');
              },
              error: async err => {
                this.load();
                await this.toast(err.error?.message || 'Greška', 'danger');
              }
            });
          }
        }
      ]
    });
    await alert.present();
  }

  // Usluga kurira je jednom po turi (korpi sa jedne pijace) i stoji na jednoj porudžbini ture
  feeOf(orders: Order[]): number {
    return orders.reduce((sum, o) => sum + o.deliveryFee, 0);
  }

  routeTotal(route: { orders: Order[] }): number {
    return route.orders.reduce((sum, o) => sum + o.amountToPay, 0);
  }

  routeAllInDelivery(route: { orders: Order[] }): boolean {
    return route.orders.length > 1 && route.orders.every(o => o.status === 'InDelivery');
  }

  async markDelivered(order: Order) {
    const alert = await this.alertController.create({
      header: 'Potvrda isporuke',
      message: `Da li ste predali porudžbinu #${order.id} i naplatili ${order.amountToPay.toLocaleString('sr-Latn')} din?`,
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
      return { ...g, address, mapsUrl: this.routeUrl(g.orders) };
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
      next: s => {
        this.settlement = s;
        this.settlementDate = s.date;
        try { this.handed = JSON.parse(localStorage.getItem(this.handedKey()) || '{}'); } catch { this.handed = {}; }
      },
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
        await this.undoToast([order]);
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

  // R3: cela tura (više tezgi iste korpe) se sklanja zajedno, kao što se i prihvata zajedno
  declineGroup(group: { key: string; orders: Order[] }) {
    this.acceptingGroup = group.key;
    forkJoin(group.orders.map(o => this.courierService.declineOrder(o.id))).subscribe({
      next: async () => {
        this.acceptingGroup = null;
        const ids = group.orders.map(o => o.id);
        this.available = this.available.filter(o => !ids.includes(o.id));
        this.notificationService.loadCourierAvailableCount();
        await this.undoToast(group.orders);
      },
      error: async err => {
        this.acceptingGroup = null;
        this.load();
        await this.toast(err.error?.message || 'Greška pri odbijanju', 'warning');
      }
    });
  }

  // "Odbij" se lako promaši palcem u pokretu: poruka nudi "Vrati" nekoliko sekundi
  private async undoToast(orders: Order[]) {
    const toast = await this.toastController.create({
      message: `Sklonjeno sa vašeg spiska: ${orders.map(o => '#' + o.id).join(', ')}. Ostali kuriri je i dalje vide.`,
      duration: 6000, color: 'medium', position: 'top',
      buttons: [{
        text: 'Vrati', handler: () => {
          forkJoin(orders.map(o => this.courierService.undoDecline(o.id))).subscribe({
            next: () => { this.load(); this.notificationService.loadCourierAvailableCount(); },
            error: () => this.load()
          });
        }
      }]
    });
    await toast.present();
  }

  // Obračun: "Još 1 dostava u toku nije uračunata" / "Još 3 dostave … nisu uračunate"
  inProgressText(n: number): string {
    return n === 1 || (n % 10 === 1 && n % 100 !== 11)
      ? `Još ${n} dostava u toku nije uračunata.`
      : `Još ${n} ${srPlural(n, 'dostava', 'dostave', 'dostava')} u toku nisu uračunate.`;
  }

  // "Predato" uz prodavca: podsetnik samo na ovom telefonu (za taj dan)
  handed: Record<string, boolean> = {};
  private handedKey() { return 'settlementHanded:' + (this.settlement?.date ?? ''); }
  isHanded(vendorId: number): boolean { return !!this.handed[vendorId]; }
  toggleHanded(vendorId: number) {
    this.handed[vendorId] = !this.handed[vendorId];
    try { localStorage.setItem(this.handedKey(), JSON.stringify(this.handed)); } catch {}
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
      'ReadyForPickup': 'Spremno', 'InDelivery': 'U dostavi', 'Completed': 'Isporučena', 'Cancelled': 'Otkazana', 'NotDelivered': 'Nije dostavljena'
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
    return 'https://www.google.com/maps/search/?api=1&query=' + encodeURIComponent(this.streetOnly(address));
  }

  // Ruta kroz pijacu (tezge) do kupca; bicikl je najbliži skuteru, a za mape je dovoljna ulica i broj.
  // Tezga sa tačkom na mapi ide u rutu po koordinatama (tačno do tezge), ostale po adresi pijace.
  routeUrl(orders: Order[]): string {
    const stops = [...new Set(orders.map(o => {
      const p = stallPoint(o);
      return p ? pointText(p) : o.marketAddress || `${o.marketName}, Novi Sad`;
    }))];
    return 'https://www.google.com/maps/dir/?api=1&travelmode=bicycling'
      + `&destination=${encodeURIComponent(this.streetOnly(orders[0].deliveryAddress))}`
      + `&waypoints=${encodeURIComponent(stops.join('|'))}`;
  }

  // Tezga na mapi (prodavac je pritisnuo "Ovde sam"): mapa se otvara na dodir, da kartice ostanu kratke
  readonly stallPoint = stallPoint;
  readonly marketPoint = marketPoint;
  mapOpen: Record<number, boolean> = {};

  toggleMap(orderId: number) {
    this.mapOpen = { ...this.mapOpen, [orderId]: !this.mapOpen[orderId] };
  }

  stallDirections(p: GeoPoint): string {
    return directionsTo(p, 'bicycling');
  }

  // "Dunavska 5, ulaz B, sprat 2, stan 7, interfon 12" -> "Dunavska 5" (ostalo je za kurira, ne za pretragu mape)
  private streetOnly(address?: string | null): string {
    if (!address) return '';
    return address.split(/,\s*(?=(ulaz|sprat|stan|interfon)\b)/i)[0].trim();
  }

  // Poruke gore, da ne pokriju dugmad "Odbij"/"Prihvati" i "Nije dostavljeno" (kao kod prodavca)
  private async toast(message: string, color: string) {
    const toast = await this.toastController.create({ message, duration: 2500, color, position: 'top' });
    await toast.present();
  }
}

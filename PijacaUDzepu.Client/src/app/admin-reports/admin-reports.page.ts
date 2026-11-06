import { Component } from '@angular/core';
import { Observable, Subscription } from 'rxjs';
import { CourierReport, CustomerReport, OrdersReport, ProductsReport, ReportService, VendorReport, WaitingOrder } from '../services/report.service';
import { downloadCsv } from '../shared/csv';
import { FulfillmentType, OrderStatus } from '../models/order.model';
import { PagerComponent, pageOf } from '../shared/pager/pager.component';
import { TranslateService } from '../services/translate.service';

type Tab = 'orders' | 'customers' | 'couriers' | 'vendors' | 'products';
type Preset = 'today' | '7' | '30' | 'month' | 'lastMonth' | 'year' | 'all';

@Component({
  selector: 'app-admin-reports',
  templateUrl: './admin-reports.page.html',
  styleUrls: ['./admin-reports.page.scss'],
  standalone: false,
})
export class AdminReportsPage {
  tab: Tab = 'orders';

  readonly presets: { value: Preset; label: string }[] = [
    { value: 'today', label: 'Danas' },
    { value: '7', label: '7 dana' },
    { value: '30', label: '30 dana' },
    { value: 'month', label: 'Ovaj mesec' },
    { value: 'lastMonth', label: 'Prošli mesec' },
    { value: 'year', label: 'Ova godina' },
    { value: 'all', label: 'Sve' },
  ];
  // Datumi su na početku prazni = svi podaci (dugme "Sve")
  preset: Preset | null = 'all';
  from = '';
  to = '';

  loading = false;
  error: string | null = null;
  ordersReport: OrdersReport | null = null;
  customers: CustomerReport[] = [];
  couriers: CourierReport[] = [];
  vendors: VendorReport[] = [];
  productsReport: ProductsReport | null = null;
  waiting: WaitingOrder[] = [];
  productsPage = 0;
  // Izveštaj se učitava za karticu koja se gleda; ključ pamti za koji period je već učitan
  private loadedFor: Partial<Record<Tab, string>> = {};
  // Zahtev koji je u toku: isti se ne šalje ponovo, a novi (drugi period ili kartica) prekida stari
  private inFlight?: { key: string; sub: Subscription };

  customerSearch = '';
  customerSort: 'orders' | 'goods' = 'orders';
  expandedCourier: number | null = null;

  // Tabele se prikazuju po stranama (podaci su već učitani, ali dugačke tabele usporavaju prikaz)
  readonly tablePageSizes = [20, 50];
  readonly pageSizeKey = 'reportsPageSize';
  tablePageSize = PagerComponent.savedSize(this.pageSizeKey, this.tablePageSizes);
  customersPage = 0;
  daysPage = 0;
  deliveriesPage = 0;

  constructor(private reportService: ReportService, private ts: TranslateService) {}

  ionViewWillEnter() {
    // Podaci se menjaju, pa se pri svakom ulasku na stranicu ponovo učitavaju
    this.loadedFor = {};
    this.load();
  }

  applyPreset(preset: Preset) {
    const today = new Date();
    const start = new Date(today);
    switch (preset) {
      case 'today': break;
      case '7': start.setDate(today.getDate() - 6); break;
      case '30': start.setDate(today.getDate() - 29); break;
      case 'month': start.setDate(1); break;
      case 'lastMonth': {
        start.setDate(1);
        start.setMonth(start.getMonth() - 1);
        const end = new Date(today.getFullYear(), today.getMonth(), 0);
        this.setRange(start, end, preset);
        return;
      }
      case 'year': start.setMonth(0, 1); break;
      case 'all':
        // Prazni datumi: od prve porudžbine do danas
        this.preset = 'all';
        this.from = '';
        this.to = '';
        this.reload();
        return;
    }
    this.setRange(start, today, preset);
  }

  // Može i samo jedan datum: samo "od" = do danas, samo "do" = od početka
  onDateChange() {
    this.preset = this.from || this.to ? null : 'all';
    this.reload();
  }

  selectTab(tab: Tab) {
    this.tab = tab;
    this.load();
  }

  private setRange(start: Date, end: Date, preset: Preset) {
    this.preset = preset;
    this.from = toDateInput(start);
    this.to = toDateInput(end);
    this.reload();
  }

  private reload() {
    this.loadedFor = {};
    this.load();
  }

  private load() {
    const key = `${this.from}|${this.to}`;
    const requestKey = `${this.tab}|${key}`;
    if (this.loadedFor[this.tab] === key || this.inFlight?.key === requestKey) return;
    this.inFlight?.sub.unsubscribe();
    if (this.from && this.to && this.from > this.to) {
      this.error = 'Početni datum mora biti pre krajnjeg.';
      return;
    }

    const tab = this.tab;
    const request: Observable<unknown> = tab === 'orders' ? this.reportService.getOrders(this.from, this.to)
      : tab === 'customers' ? this.reportService.getCustomers(this.from, this.to)
      : tab === 'vendors' ? this.reportService.getVendors(this.from, this.to)
      : tab === 'products' ? this.reportService.getProducts(this.from, this.to)
      : this.reportService.getCouriers(this.from, this.to);
    // Porudžbine koje trenutno predugo čekaju (ne zavise od perioda)
    if (tab === 'orders') this.reportService.getWaiting().subscribe({ next: w => this.waiting = w, error: () => {} });

    this.loading = true;
    this.error = null;
    const sub = request.subscribe({
      next: data => {
        if (tab === 'orders') { this.ordersReport = data as OrdersReport; this.daysPage = 0; }
        else if (tab === 'customers') { this.customers = data as CustomerReport[]; this.customersPage = 0; }
        else if (tab === 'vendors') { this.vendors = data as VendorReport[]; }
        else if (tab === 'products') { this.productsReport = data as ProductsReport; this.productsPage = 0; }
        else { this.couriers = data as CourierReport[]; this.deliveriesPage = 0; }
        this.loadedFor[tab] = key;
        this.loading = false;
        this.inFlight = undefined;
      },
      error: err => {
        this.error = err.error?.message || 'Izveštaj nije učitan. Pokušajte ponovo.';
        this.loading = false;
        this.inFlight = undefined;
      }
    });
    if (!sub.closed) this.inFlight = { key: requestKey, sub };
  }

  doRefresh(event: any) {
    this.reload();
    setTimeout(() => event.target.complete(), 500);
  }

  // ---- porudžbine ----

  get maxStatusCount(): number {
    return Math.max(1, ...(this.ordersReport?.byStatus.map(s => s.count) ?? []));
  }

  get maxDayOrders(): number {
    return Math.max(1, ...(this.ordersReport?.byDay.map(d => d.orders) ?? []));
  }

  percent(value: number, max: number): number {
    return Math.round(value / max * 100);
  }

  // ---- kupci ----

  get filteredCustomers(): CustomerReport[] {
    const q = this.customerSearch.trim().toLowerCase();
    const list = q
      ? this.customers.filter(c => [c.name, c.phone, c.userName].some(v => v?.toLowerCase().includes(q)))
      : [...this.customers];
    return this.customerSort === 'goods'
      ? list.sort((a, b) => b.goodsTotal - a.goodsTotal || b.orders - a.orders)
      : list;
  }

  // Zbir za sve kupce koji su trenutno prikazani (uz pretragu - samo za pronađene)
  get customerTotals() {
    const list = this.filteredCustomers;
    return {
      customers: list.length,
      orders: list.reduce((n, c) => n + c.orders, 0),
      completed: list.reduce((n, c) => n + c.completed, 0),
      rejected: list.reduce((n, c) => n + c.rejected, 0),
      goodsTotal: list.reduce((n, c) => n + c.goodsTotal, 0)
    };
  }

  get pagedCustomers(): CustomerReport[] {
    return pageOf(this.filteredCustomers, this.customersPage, this.tablePageSize);
  }

  get pagedDays() {
    return pageOf(this.ordersReport?.byDay ?? [], this.daysPage, this.tablePageSize);
  }

  pagedDeliveries(c: CourierReport) {
    return pageOf(c.deliveries, this.deliveriesPage, this.tablePageSize);
  }

  // Pretraga ili drugi redosled kupaca vraćaju na prvu stranu
  resetCustomersPage() {
    this.customersPage = 0;
  }

  setTablePageSize(size: number, which: 'customers' | 'days' | 'deliveries' | 'products') {
    const pageOfTable = which === 'customers' ? this.customersPage : which === 'days' ? this.daysPage
      : which === 'products' ? this.productsPage : this.deliveriesPage;
    const next = PagerComponent.pageAfterResize(pageOfTable, this.tablePageSize, size);
    this.tablePageSize = size;
    this.customersPage = which === 'customers' ? next : 0;
    this.daysPage = which === 'days' ? next : 0;
    this.deliveriesPage = which === 'deliveries' ? next : 0;
    this.productsPage = which === 'products' ? next : 0;
  }

  get guestCount(): number {
    return this.customers.filter(c => c.isGuest).length;
  }

  // ---- kuriri ----

  // Zbir za sve kurire; prosečno trajanje je ponderisano brojem isporuka
  get courierTotals() {
    const timed = this.couriers.filter(c => c.averageDeliveryMinutes != null && c.delivered > 0);
    const timedCount = timed.reduce((n, c) => n + c.delivered, 0);
    return {
      couriers: this.couriers.length,
      onShift: this.couriers.filter(c => c.isOnShift).length,
      delivered: this.couriers.reduce((n, c) => n + c.delivered, 0),
      inProgress: this.couriers.reduce((n, c) => n + c.inProgress, 0),
      feesTotal: this.couriers.reduce((n, c) => n + c.feesTotal, 0),
      cashCollected: this.couriers.reduce((n, c) => n + c.cashCollected, 0),
      averageDeliveryMinutes: timedCount > 0
        ? Math.round(timed.reduce((n, c) => n + c.averageDeliveryMinutes! * c.delivered, 0) / timedCount)
        : null
    };
  }

  toggleCourier(id: number) {
    this.expandedCourier = this.expandedCourier === id ? null : id;
    this.deliveriesPage = 0;
  }

  mapsUrl(address?: string): string {
    return 'https://www.google.com/maps/search/?api=1&query=' + encodeURIComponent(address ?? '');
  }

  // ---- oznake ----

  statusLabel(status: OrderStatus): string {
    const labels: Record<string, string> = {
      'Pending': 'Na čekanju', 'Confirmed': 'Potvrđena', 'Rejected': 'Odbijena', 'ReadyForPickup': 'Spremna',
      'InDelivery': 'U dostavi', 'Completed': 'Završena', 'Cancelled': 'Otkazana', 'NotDelivered': 'Nije dostavljena'
    };
    return labels[status] || status;
  }

  statusColor(status: OrderStatus): string {
    const colors: Record<string, string> = {
      'Pending': 'warning', 'Confirmed': 'primary', 'Rejected': 'danger', 'ReadyForPickup': 'success',
      'InDelivery': 'tertiary', 'Completed': 'medium', 'Cancelled': 'medium', 'NotDelivered': 'danger'
    };
    return colors[status] || 'medium';
  }

  // Datum dolazi kao "yyyy-MM-dd" (lokalni dan); aplikacija nema srpski DatePipe lokal
  dayName(date: string): string {
    const [y, m, d] = date.split('-').map(Number);
    return ['ned', 'pon', 'uto', 'sre', 'čet', 'pet', 'sub'][new Date(y, m - 1, d).getDay()];
  }

  get pagedProducts() {
    return pageOf(this.productsReport?.products ?? [], this.productsPage, this.tablePageSize);
  }

  get maxCategoryRevenue(): number {
    return Math.max(1, ...(this.productsReport?.categories.map(c => c.revenue) ?? []));
  }

  categoryLabel(c?: string | null): string {
    return c ? this.ts.t('cat.' + c) : 'Obrisan proizvod';
  }

  unitLabel(u: string): string {
    const labels: Record<string, string> = { Kg: 'kg', Gram: 'g', Komad: 'kom', Veza: 'veza', Litar: 'l', Pakovanje: 'pak', Kutija: 'kut', Flasa: 'fl' };
    return labels[u] ?? u;
  }

  // ---- izvoz u Excel (CSV) ----

  private get periodSuffix(): string {
    return `${this.from || 'pocetak'}_${this.to || 'danas'}`;
  }

  exportDays() {
    if (!this.ordersReport) return;
    downloadCsv(`porudzbine-po-danima_${this.periodSuffix}`, ['Datum', 'Porudžbina', 'Završeno', 'Promet (din)'],
      this.ordersReport.byDay.map(d => [d.date, d.orders, d.completed, d.goodsTotal]));
  }

  exportCustomers() {
    downloadCsv(`kupci_${this.periodSuffix}`, ['Kupac', 'Korisničko ime', 'Gost', 'Telefon', 'Porudžbina', 'Završeno', 'Odbijeno', 'Promet (din)', 'Poslednja'],
      this.filteredCustomers.map(c => [c.name, c.userName, c.isGuest ? 'da' : 'ne', c.phone, c.orders, c.completed, c.rejected, c.goodsTotal, c.lastOrderAt.slice(0, 16).replace('T', ' ')]));
  }

  exportCouriers() {
    downloadCsv(`kuriri-voznje_${this.periodSuffix}`, ['Kurir', 'Porudžbina', 'Datum', 'Prodavac', 'Pijaca', 'Adresa', 'Status', 'Trajanje (min)', 'Naknada (din)', 'Naplata (din)'],
      this.couriers.flatMap(c => c.deliveries.map(d => [c.name, d.orderId, d.createdAt.slice(0, 16).replace('T', ' '), d.vendorName, d.marketName, d.deliveryAddress, this.statusLabel(d.status), d.minutes, d.deliveryFee, d.amountToPay])));
  }

  exportVendors() {
    downloadCsv(`prodavci_${this.periodSuffix}`, ['Prodavac', 'Pijaca', 'Porudžbina', 'Završeno', 'Odbijeno', 'Odbijeno (%)', 'Promet (din)', 'Odgovor (min)', 'Ocena', 'Broj ocena', 'Poslednji razlozi odbijanja'],
      this.vendors.map(v => [v.name, v.marketName, v.orders, v.completed, v.rejected, v.rejectionRate, v.goodsTotal, v.averageResponseMinutes, v.averageRating, v.ratingCount, v.recentRejectionReasons.join(' | ')]));
  }

  exportProducts() {
    if (!this.productsReport) return;
    downloadCsv(`proizvodi_${this.periodSuffix}`, ['Proizvod', 'Prodavac', 'Količina', 'Jedinica', 'Porudžbina', 'Promet (din)'],
      this.productsReport.products.map(p => [p.productName, p.vendorName, p.quantity, this.unitLabel(p.unit), p.orders, p.revenue]));
  }

  fulfillmentLabel(f: FulfillmentType): string {
    return f === 'Pickup' ? 'Lično preuzimanje' : 'Eko kurir';
  }
}

function toDateInput(d: Date): string {
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
}

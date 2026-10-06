import { Component, OnInit } from '@angular/core';
import { ProductService } from '../services/product.service';
import { CartService } from '../services/cart.service';
import { VendorService } from '../services/vendor.service';
import { MarketService } from '../services/market.service';
import { StallService } from '../services/stall.service';
import { Product, ProductCategory } from '../models/product.model';
import { Vendor } from '../models/vendor.model';
import { Market } from '../models/market.model';
import { Stall } from '../models/stall.model';
import { ToastController } from '@ionic/angular/lazy';
import { environment } from '../../environments/environment';

@Component({
  selector: 'app-home',
  templateUrl: 'home.page.html',
  styleUrls: ['home.page.scss'],
  standalone: false,
})
export class HomePage implements OnInit {
  products: Product[] = [];
  vendors: Vendor[] = [];
  markets: Market[] = [];
  stalls: Stall[] = [];
  searchTerm = '';
  selectedMarketId: number | null = null;
  selectedVendorId: number | null = null;
  selectedStallId: number | null = null;
  selectedCategory: ProductCategory | null = null;
  categories: { value: ProductCategory; label: string }[] = [
    { value: 'Voce', label: 'Voće' },
    { value: 'Povrce', label: 'Povrće' },
    { value: 'MlecniProizvodi', label: 'Mlečni proizvodi' },
    { value: 'MesniProizvodi', label: 'Mesni proizvodi' },
    { value: 'Konditori', label: 'Konditori' },
    { value: 'KucnaHemija', label: 'Kućna hemija' },
    { value: 'Ostalo', label: 'Ostalo' },
  ];
  loading = false;
  hasMore = true;
  private pageSize = 20;
  apiUrl = environment.apiUrl.replace('/api', '');
  selectedQty: Record<number, number> = {};
  serviceInfoCache: Record<number, { icon: string; text: string; color: string }[]> = {};
  canOrderCache: Record<number, boolean> = {};
  vendorBadgeCache: Record<number, { reservations: boolean; delivery: boolean }> = {};
  cardClassCache: Record<number, string> = {};
  filterDelivery = false;
  filterReservations = false;
  displayProducts: Product[] = [];

  showModal: 'market' | 'vendor' | 'stall' | 'category' | null = null;
  filterSearch = { market: '', vendor: '', stall: '', category: '' };

  get filteredVendors(): Vendor[] {
    if (!this.selectedMarketId) return this.vendors;
    return this.vendors.filter(v => v.marketId === this.selectedMarketId);
  }

  get searchedMarkets(): Market[] {
    if (!this.filterSearch.market) return this.markets;
    const term = this.normalize(this.filterSearch.market);
    return this.markets.filter(m => this.normalize(m.name).includes(term));
  }

  get searchedVendors(): Vendor[] {
    const vendors = this.filteredVendors;
    if (!this.filterSearch.vendor) return vendors;
    const term = this.normalize(this.filterSearch.vendor);
    return vendors.filter(v => this.normalize(v.name).includes(term));
  }

  get searchedStalls(): Stall[] {
    if (!this.filterSearch.stall) return this.stalls;
    const term = this.normalize(this.filterSearch.stall);
    return this.stalls.filter(s =>
      this.normalize(s.label).includes(term) ||
      this.normalize('Tezga ' + s.label).includes(term)
    );
  }

  private normalize(text: string): string {
    return text
      .toLowerCase()
      .normalize('NFD')
      .replace(/[̀-ͯ]/g, '')
      .replace(/đ/g, 'd');
  }

  constructor(
    private productService: ProductService,
    private cartService: CartService,
    private vendorService: VendorService,
    private marketService: MarketService,
    private stallService: StallService,
    private toastController: ToastController
  ) {}

  ngOnInit() {
    this.loadMarkets();
    this.loadVendors();
    this.loadProducts();
  }

  loadMarkets() {
    this.marketService.getAll().subscribe(m => this.markets = m);
  }

  loadVendors() {
    this.vendorService.getAll().subscribe(v => {
      this.vendors = v;
      this.rebuildServiceCache();
      this.rebuildDisplay();
    });
  }

  loadProducts(reset = true) {
    if (reset) {
      this.products = [];
      this.hasMore = true;
      this.serviceInfoCache = {};
      this.canOrderCache = {};
      this.vendorBadgeCache = {};
      this.cardClassCache = {};
    }
    this.loading = true;
    this.productService.getAll(
      this.searchTerm || undefined,
      this.selectedVendorId || undefined,
      this.selectedMarketId || undefined,
      this.selectedCategory || undefined,
      this.products.length,
      this.pageSize
    ).subscribe({
      next: result => {
        this.products = [...this.products, ...result.items];
        this.hasMore = result.hasMore;
        this.loading = false;
        this.rebuildServiceCache();
        this.rebuildDisplay();
      },
      error: () => this.loading = false
    });
  }

  loadMore(event: any) {
    this.productService.getAll(
      this.searchTerm || undefined,
      this.selectedVendorId || undefined,
      this.selectedMarketId || undefined,
      this.selectedCategory || undefined,
      this.products.length,
      this.pageSize
    ).subscribe({
      next: result => {
        this.products = [...this.products, ...result.items];
        this.hasMore = result.hasMore;
        this.rebuildServiceCache();
        this.rebuildDisplay();
        event.target.complete();
        if (!result.hasMore) event.target.disabled = true;
      },
      error: () => event.target.complete()
    });
  }

  onSearch() {
    this.loadProducts();
  }

  get searchedCategories() {
    if (!this.filterSearch.category) return this.categories;
    const term = this.normalize(this.filterSearch.category);
    return this.categories.filter(c => this.normalize(c.label).includes(term));
  }

  openFilterModal(type: 'market' | 'vendor' | 'stall' | 'category') {
    this.filterSearch = { market: '', vendor: '', stall: '', category: '' };
    this.showModal = type;
  }

  selectMarket(marketId: number | null) {
    this.selectedMarketId = marketId;
    this.selectedVendorId = null;
    this.selectedStallId = null;
    this.stalls = [];
    if (marketId) {
      this.stallService.getByMarket(marketId).subscribe(s => this.stalls = s);
    }
    this.loadProducts();
  }

  selectVendor(vendorId: number | null | undefined) {
    this.selectedVendorId = vendorId ?? null;
    if (this.selectedVendorId) {
      const vendor = this.vendors.find(v => v.id === this.selectedVendorId);
      this.selectedStallId = vendor?.stallId
        ? this.stalls.find(s => s.id === vendor.stallId)?.id ?? null
        : null;
    } else {
      this.selectedStallId = null;
    }
    this.loadProducts();
  }

  selectStall(stall: Stall | null) {
    if (!stall) {
      this.selectedStallId = null;
      this.selectedVendorId = null;
      this.loadProducts();
      return;
    }
    this.selectedStallId = stall.id;
    const vendor = this.vendors.find(v => v.stallId === stall.id);
    this.selectedVendorId = vendor?.id ?? -1;
    this.loadProducts();
  }

  selectCategory(category: ProductCategory | null) {
    this.selectedCategory = category;
    this.loadProducts();
  }

  getCategoryLabel(value: ProductCategory): string {
    return this.categories.find(c => c.value === value)?.label ?? value;
  }

  getMarketName(id: number): string {
    return this.markets.find(m => m.id === id)?.name ?? '';
  }

  getVendorName(id: number): string {
    return this.vendors.find(v => v.id === id)?.name ?? '';
  }

  getStallLabel(id: number): string {
    return this.stalls.find(s => s.id === id)?.label ?? '';
  }

  getVendorAtStall(stallId: number): string {
    const vendor = this.vendors.find(v => v.stallId === stallId);
    return vendor ? vendor.name : 'Slobodna';
  }

  getSelectedQty(productId: number): number {
    return this.selectedQty[productId] ?? 1;
  }

  increaseQty(product: Product) {
    this.selectedQty[product.id] = this.getSelectedQty(product.id) + 1;
  }

  decreaseQty(product: Product) {
    const current = this.getSelectedQty(product.id);
    if (current > 1) {
      this.selectedQty[product.id] = current - 1;
    }
  }

  async addSelectedToCart(product: Product) {
    const qty = this.getSelectedQty(product.id);
    this.cartService.addToCart(product, qty);
    this.selectedQty[product.id] = 1;
    const toast = await this.toastController.create({
      message: `${product.name} x${qty} dodato u korpu`,
      duration: 1500,
      position: 'bottom',
      color: 'success'
    });
    await toast.present();
  }

  toggleServiceFilter(type: 'delivery' | 'reservations') {
    if (type === 'delivery') this.filterDelivery = !this.filterDelivery;
    else this.filterReservations = !this.filterReservations;
    this.rebuildDisplay();
  }

  private rebuildDisplay() {
    let items = this.products;
    if (this.filterDelivery || this.filterReservations) {
      items = items.filter(p => {
        const v = this.vendors.find(vn => vn.id === p.vendorId);
        if (!v) return false;
        if (this.filterDelivery && !v.offersDelivery) return false;
        if (this.filterReservations && !v.acceptsReservations) return false;
        return true;
      });
    }
    this.displayProducts = items;
  }

  private rebuildServiceCache() {
    for (const product of this.products) {
      const v = this.vendors.find(vn => vn.id === product.vendorId);
      if (!v) continue;
      if (this.serviceInfoCache[product.id]?.length) continue;
      this.canOrderCache[product.id] = !!(v.acceptsReservations || v.offersDelivery);
      this.vendorBadgeCache[product.id] = { reservations: v.acceptsReservations, delivery: v.offersDelivery };
      if (v.acceptsReservations && v.offersDelivery) this.cardClassCache[product.id] = 'card-both';
      else if (v.acceptsReservations) this.cardClassCache[product.id] = 'card-reservation';
      else if (v.offersDelivery) this.cardClassCache[product.id] = 'card-delivery';
      else this.cardClassCache[product.id] = '';
      const msgs: { icon: string; text: string; color: string }[] = [];

      if (!v.acceptsReservations && !v.offersDelivery) {
        msgs.push({ icon: 'eye-outline', text: 'Samo pregled – posetite prodavca na pijaci', color: 'medium' });
      } else {
        if (v.acceptsReservations) {
          msgs.push({ icon: 'bag-check-outline', text: 'Poručite i dođite lično po robu', color: 'primary' });
        }
        if (v.offersDelivery) {
          let deliveryText = 'Prodavac dostavlja robu';
          if (v.minOrderAmount) deliveryText += ` (min. ${v.minOrderAmount} din)`;
          if (v.deliveryRadiusKm) deliveryText += `, do ${v.deliveryRadiusKm} km`;
          if (v.deliverySchedule && v.deliverySchedule.length > 0) {
            const shortNames: Record<number, string> = { 1: 'pon', 2: 'uto', 3: 'sre', 4: 'čet', 5: 'pet', 6: 'sub', 7: 'ned' };
            const days = v.deliverySchedule.map(d => (shortNames[d.day] ?? '') + ' ' + d.from + '-' + d.to).join(', ');
            deliveryText += ' – ' + days;
          }
          msgs.push({ icon: 'car-outline', text: deliveryText, color: 'success' });
        }
      }
      this.serviceInfoCache[product.id] = msgs;
    }
  }

  getUnitLabel(unit: string): string {
    switch (unit) {
      case 'Kg': return 'kg';
      case 'Gram': return 'g';
      case 'Komad': return 'kom';
      case 'Veza': return 'veza';
      case 'Litar': return 'l';
      case 'Pakovanje': return 'pak';
      case 'Kutija': return 'kut';
      case 'Flasa': return 'flaša';
      default: return unit;
    }
  }

  getImageUrl(imageUrl?: string): string {
    if (!imageUrl) return 'assets/placeholder.svg';
    if (imageUrl.startsWith('http')) return imageUrl;
    return this.apiUrl + imageUrl;
  }

  doRefresh(event: any) {
    this.loadProducts(true);
    setTimeout(() => event.target.complete(), 500);
  }
}

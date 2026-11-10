import { Component, OnInit } from '@angular/core';
import { ProductService } from '../services/product.service';
import { CartService } from '../services/cart.service';
import { VendorService } from '../services/vendor.service';
import { MarketService } from '../services/market.service';
import { StallService } from '../services/stall.service';
import { OrderService } from '../services/order.service';
import { Product, ProductCategory, PRODUCT_CATEGORIES, ProductSort, ProductUnit } from '../models/product.model';
import { vendorClosedText } from '../services/vendor-status';
import { FavoritesService } from '../services/favorites.service';
import { AuthService } from '../services/auth.service';
import { Vendor } from '../models/vendor.model';
import { Market } from '../models/market.model';
import { Stall } from '../models/stall.model';
import { ToastController } from '@ionic/angular/lazy';
import { environment } from '../../environments/environment';
import { TranslateService } from '../services/translate.service';
import { describeConnectionError, isConnectionError } from '../services/connection-error';

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
  selectedUnit: ProductUnit | null = null;
  // Gram se ne nudi posebno: filter Kg obuhvata i proizvode na gram
  unitOptions: { value: ProductUnit; label: string }[] = [];
  sortBy: ProductSort = 'NameAsc';
  sortOptions: { value: ProductSort; label: string }[] = [];
  categories: { value: ProductCategory; label: string }[] = [];
  loading = false;
  hasMore = true;
  private pageSize = 20;
  apiUrl = environment.apiUrl.replace('/api', '');
  selectedQty: Record<number, number> = {};
  vendorPhoneCache: Record<number, string[]> = {};
  connectionError: string | null = null;
  connectionTitle = '';

  showModal: 'market' | 'vendor' | 'stall' | 'category' | 'sort' | null = null;
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

  // Samo tezge sa prodavcem, poređane kao brojevi (1, 2, … 10), ne kao tekst (1, 10, 11, 2)
  get searchedStalls(): Stall[] {
    const occupied = this.stalls
      .filter(s => this.vendors.some(v => v.stallId === s.id))
      .sort((a, b) => a.label.localeCompare(b.label, undefined, { numeric: true }));
    if (!this.filterSearch.stall) return occupied;
    const term = this.normalize(this.filterSearch.stall);
    return occupied.filter(s =>
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
    private toastController: ToastController,
    public ts: TranslateService,
    public favorites: FavoritesService,
    private authService: AuthService,
    private orderService: OrderService
  ) {
    this.ts.lang$.subscribe(() => this.rebuildCategories());
  }

  private rebuildCategories() {
    this.categories = PRODUCT_CATEGORIES.map(value => ({ value, label: this.ts.t('cat.' + value) }));
    this.unitOptions = (['Kg', 'Komad', 'Veza', 'Litar', 'Pakovanje', 'Kutija', 'Flasa'] as ProductUnit[])
      .map(value => ({ value, label: this.ts.t('unitFilter.' + value) }));
    this.sortOptions = [
      { value: 'NameAsc', label: this.ts.t('sort.NameAsc') },
      { value: 'NameDesc', label: this.ts.t('sort.NameDesc') },
      { value: 'PriceAsc', label: this.ts.t('sort.PriceAsc') },
      { value: 'PriceDesc', label: this.ts.t('sort.PriceDesc') },
      { value: 'Newest', label: this.ts.t('sort.Newest') },
    ];
  }

  // Cena dostave po tezgi (za objašnjenje na početnoj i upozorenje o novoj tezgi u korpi)
  courierFee = 0;
  // Kratko objašnjenje "Kako radi" dok ga kupac ne zatvori
  showHowItWorks = (() => { try { return localStorage.getItem('howItWorksSeen') !== '1'; } catch { return true; } })();
  // Grupe u kojima ima robe (prazne se ne nude u filteru); null = još nije učitano
  private availableCategories: string[] | null = null;

  ngOnInit() {
    this.loadMarkets();
    this.loadVendors();
    this.loadProducts();
    this.loadCategories();
    this.orderService.getDeliveryOptions().subscribe({ next: o => this.courierFee = o.courierFee, error: () => {} });
  }

  closeHowItWorks() {
    this.showHowItWorks = false;
    try { localStorage.setItem('howItWorksSeen', '1'); } catch {}
  }

  howFeeText(): string {
    return this.ts.t('home.how2').replace('{0}', String(this.courierFee || 300));
  }

  loadCategories() {
    this.productService.getCategories(this.selectedMarketId, this.selectedVendorId && this.selectedVendorId > 0 ? this.selectedVendorId : null)
      .subscribe({ next: c => this.availableCategories = c, error: () => this.availableCategories = null });
  }

  // U korpi je već ovoliko ovog proizvoda (brojač na kartici se posle dodavanja vraća na 0)
  inCart(productId: number): number {
    return this.cartService.getQuantity(productId);
  }

  loadMarkets() {
    this.marketService.getAll().subscribe(m => this.markets = m);
  }

  loadVendors() {
    this.vendorService.getAll().subscribe(v => {
      this.vendors = v;
      this.rebuildPhoneCache();
    });
  }

  loadProducts(reset = true) {
    if (reset) {
      this.products = [];
      this.hasMore = true;
      this.vendorPhoneCache = {};
    }
    this.loading = true;
    this.productService.getAll(
      this.searchTerm || undefined,
      this.selectedVendorId || undefined,
      this.selectedMarketId || undefined,
      this.selectedCategory || undefined,
      this.selectedUnit || undefined,
      this.sortBy,
      this.products.length,
      this.pageSize,
      this.favoritesFilter()
    ).subscribe({
      next: result => {
        this.products = [...this.products, ...result.items];
        this.hasMore = result.hasMore;
        this.loading = false;
        this.connectionError = null;
        this.rebuildPhoneCache();
      },
      error: err => {
        this.loading = false;
        this.connectionError = describeConnectionError(err, k => this.ts.t(k));
        this.connectionTitle = this.ts.t(isConnectionError(err) ? 'home.noConnection' : 'error.serverTitle');
      }
    });
  }

  loadMore(event: any) {
    this.productService.getAll(
      this.searchTerm || undefined,
      this.selectedVendorId || undefined,
      this.selectedMarketId || undefined,
      this.selectedCategory || undefined,
      this.selectedUnit || undefined,
      this.sortBy,
      this.products.length,
      this.pageSize,
      this.favoritesFilter()
    ).subscribe({
      next: result => {
        this.products = [...this.products, ...result.items];
        this.hasMore = result.hasMore;
        this.rebuildPhoneCache();
        event.target.complete();
        if (!result.hasMore) event.target.disabled = true;
      },
      error: () => event.target.complete()
    });
  }

  onSearch() {
    this.loadProducts();
  }

  // ---- omiljeni ----
  showFavorites = false;

  private favoritesFilter() {
    return this.showFavorites ? { productIds: this.favorites.productIds, vendorIds: [] } : undefined;
  }

  toggleFavoritesFilter() {
    this.showFavorites = !this.showFavorites;
    if (this.showFavorites && this.favorites.isEmpty) {
      this.products = [];
      this.hasMore = false;
      return;
    }
    this.loadProducts();
  }

  toggleFavoriteProduct(product: Product, event: Event) {
    event.stopPropagation();
    this.favorites.toggleProduct(product.id);
    if (this.showFavorites) this.loadProducts();
  }


  async toggleAlert(product: Product) {
    if (!this.favorites.toggleAlert(product.id)) {
      const toast = await this.toastController.create({ message: this.ts.t('fav.alertLogin'), duration: 3000, color: 'warning' });
      await toast.present();
      return;
    }
    const on = this.favorites.hasAlert(product.id);
    const toast = await this.toastController.create({ message: this.ts.t(on ? 'fav.alertOn' : 'fav.alertOff'), duration: 2500, color: 'success' });
    await toast.present();
  }

  get searchedCategories() {
    const offered = this.availableCategories
      ? this.categories.filter(c => this.availableCategories!.includes(c.value) || c.value === this.selectedCategory)
      : this.categories;
    if (!this.filterSearch.category) return offered;
    const term = this.normalize(this.filterSearch.category);
    return offered.filter(c => this.normalize(c.label).includes(term));
  }

  openFilterModal(type: 'market' | 'vendor' | 'stall' | 'category' | 'sort') {
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
    this.loadCategories();
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
    this.loadCategories();
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

  selectUnit(unit: ProductUnit | null) {
    this.selectedUnit = unit;
    this.loadProducts();
  }

  getUnitFilterLabel(value: ProductUnit): string {
    return this.unitOptions.find(u => u.value === value)?.label ?? value;
  }

  // Za proizvode na gram prikazuje se i preračunata cena po kg
  getPricePerKg(product: Product): number | null {
    return product.unit === 'Gram' ? product.price * 1000 : null;
  }

  selectSort(sort: ProductSort) {
    if (this.sortBy === sort) return;
    this.sortBy = sort;
    // Jedinica mere služi samo za poređenje cena; uz druge redoslede se ne zadržava
    if (!this.isPriceSort(sort)) this.selectedUnit = null;
    this.loadProducts();
  }

  // Kod sortiranja po ceni prozor ostaje otvoren da kupac izabere jedinicu za poređenje
  chooseSort(sort: ProductSort) {
    this.selectSort(sort);
    if (!this.isPriceSort(sort)) this.showModal = null;
  }

  isPriceSort(sort: ProductSort): boolean {
    return sort === 'PriceAsc' || sort === 'PriceDesc';
  }

  getSortLabel(value: ProductSort): string {
    return this.sortOptions.find(s => s.value === value)?.label ?? value;
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
    return vendor ? vendor.name : this.ts.t('home.free');
  }

  getSelectedQty(productId: number): number {
    return this.selectedQty[productId] ?? 0;
  }

  increaseQty(product: Product) {
    // Ne više nego što ima na stanju (kad prodavac prati stanje)
    const next = this.getSelectedQty(product.id) + 1;
    if (product.stockQuantity != null && next > product.stockQuantity) return;
    this.selectedQty[product.id] = next;
  }

  decreaseQty(product: Product) {
    const current = this.getSelectedQty(product.id);
    if (current > 0) {
      this.selectedQty[product.id] = current - 1;
    }
  }

  async addSelectedToCart(product: Product) {
    // "Dodaj" bez biranja količine dodaje 1 (ranije nije radilo ništa)
    const qty = this.getSelectedQty(product.id) || 1;
    // Nova tezga u korpi: dostava kurirom se plaća po tezgi, pa kupac to sazna odmah, a ne tek u korpi
    const items = this.cartService.getItems();
    const newVendor = items.length > 0 && !items.some(i => i.product.vendorId === product.vendorId);
    this.cartService.addToCart(product, qty);
    this.selectedQty[product.id] = 0;
    const feeNote = newVendor && this.courierFee > 0 ? ' ' + this.ts.t('home.newVendorFee').replace('{0}', String(this.courierFee)) : '';
    const toast = await this.toastController.create({
      message: `${product.name} x${qty} ${this.ts.t('cart.addedToCart')}.${feeNote}`,
      duration: feeNote ? 4500 : 1500,
      position: 'bottom',
      color: 'success'
    });
    await toast.present();
  }

  private rebuildPhoneCache() {
    for (const product of this.products) {
      const v = this.vendors.find(vn => vn.id === product.vendorId);
      if (v) this.vendorPhoneCache[product.id] = v.phones ?? [];
    }
  }

  getUnitLabel(unit: string): string {
    return this.ts.t('unit.' + unit) || unit;
  }

  closedText(product: Product): string {
    return vendorClosedText(this.ts, product.vendorClosedReason, product.vendorOpensAt);
  }

  // Neispravna ili nedostupna slika se menja zamenskom, umesto da se prikaže tekst
  onImageError(event: Event) {
    const img = event.target as HTMLImageElement;
    if (!img.src.endsWith('assets/placeholder.svg')) img.src = 'assets/placeholder.svg';
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

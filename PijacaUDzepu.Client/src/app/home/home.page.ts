import { Component, OnInit } from '@angular/core';
import { ProductService } from '../services/product.service';
import { CartService } from '../services/cart.service';
import { VendorService } from '../services/vendor.service';
import { MarketService } from '../services/market.service';
import { StallService } from '../services/stall.service';
import { Product, ProductCategory, ProductSort, ProductUnit } from '../models/product.model';
import { Vendor } from '../models/vendor.model';
import { Market } from '../models/market.model';
import { Stall } from '../models/stall.model';
import { ToastController } from '@ionic/angular/lazy';
import { environment } from '../../environments/environment';
import { TranslateService } from '../services/translate.service';
import { describeConnectionError } from '../services/connection-error';

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
  vendorPhoneCache: Record<number, string[]> = {};
  connectionError: string | null = null;

  showModal: 'market' | 'vendor' | 'stall' | 'category' | 'unit' | 'sort' | null = null;
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
    private toastController: ToastController,
    public ts: TranslateService
  ) {
    this.ts.lang$.subscribe(() => this.rebuildCategories());
  }

  private rebuildCategories() {
    this.categories = [
      { value: 'Voce', label: this.ts.t('cat.Voce') },
      { value: 'Povrce', label: this.ts.t('cat.Povrce') },
      { value: 'MlecniProizvodi', label: this.ts.t('cat.MlecniProizvodi') },
      { value: 'MesniProizvodi', label: this.ts.t('cat.MesniProizvodi') },
      { value: 'Konditori', label: this.ts.t('cat.Konditori') },
      { value: 'KucnaHemija', label: this.ts.t('cat.KucnaHemija') },
      { value: 'Ostalo', label: this.ts.t('cat.Ostalo') },
    ];
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
      this.pageSize
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
        this.connectionError = describeConnectionError(err);
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
      this.pageSize
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

  get searchedCategories() {
    if (!this.filterSearch.category) return this.categories;
    const term = this.normalize(this.filterSearch.category);
    return this.categories.filter(c => this.normalize(c.label).includes(term));
  }

  openFilterModal(type: 'market' | 'vendor' | 'stall' | 'category' | 'unit' | 'sort') {
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
    this.loadProducts();
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
    this.selectedQty[product.id] = this.getSelectedQty(product.id) + 1;
  }

  decreaseQty(product: Product) {
    const current = this.getSelectedQty(product.id);
    if (current > 0) {
      this.selectedQty[product.id] = current - 1;
    }
  }

  async addSelectedToCart(product: Product) {
    const qty = this.getSelectedQty(product.id);
    if (qty <= 0) return;
    this.cartService.addToCart(product, qty);
    this.selectedQty[product.id] = 0;
    const toast = await this.toastController.create({
      message: `${product.name} x${qty} ${this.ts.t('cart.addedToCart')}`,
      duration: 1500,
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

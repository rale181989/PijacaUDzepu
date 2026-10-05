import { Component, OnInit } from '@angular/core';
import { ProductService } from '../services/product.service';
import { CartService } from '../services/cart.service';
import { VendorService } from '../services/vendor.service';
import { MarketService } from '../services/market.service';
import { Product } from '../models/product.model';
import { Vendor } from '../models/vendor.model';
import { Market } from '../models/market.model';
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
  searchTerm = '';
  selectedMarketId: number | null = null;
  selectedVendorId: number | null = null;
  loading = false;
  hasMore = true;
  private pageSize = 20;
  apiUrl = environment.apiUrl.replace('/api', '');
  cartQuantities: Record<number, number> = {};
  selectedQty: Record<number, number> = {};

  get filteredVendors(): Vendor[] {
    if (!this.selectedMarketId) return this.vendors;
    return this.vendors.filter(v => v.marketId === this.selectedMarketId);
  }

  constructor(
    private productService: ProductService,
    private cartService: CartService,
    private vendorService: VendorService,
    private marketService: MarketService,
    private toastController: ToastController
  ) {
    this.cartService.cart$.subscribe(() => this.refreshCartQuantities());
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
    this.vendorService.getAll().subscribe(v => this.vendors = v);
  }

  loadProducts(reset = true) {
    if (reset) {
      this.products = [];
      this.hasMore = true;
    }
    this.loading = true;
    this.productService.getAll(
      this.searchTerm || undefined,
      this.selectedVendorId || undefined,
      this.selectedMarketId || undefined,
      this.products.length,
      this.pageSize
    ).subscribe({
      next: result => {
        this.products = [...this.products, ...result.items];
        this.hasMore = result.hasMore;
        this.loading = false;
      },
      error: () => this.loading = false
    });
  }

  loadMore(event: any) {
    this.productService.getAll(
      this.searchTerm || undefined,
      this.selectedVendorId || undefined,
      this.selectedMarketId || undefined,
      this.products.length,
      this.pageSize
    ).subscribe({
      next: result => {
        this.products = [...this.products, ...result.items];
        this.hasMore = result.hasMore;
        event.target.complete();
        if (!result.hasMore) event.target.disabled = true;
      },
      error: () => event.target.complete()
    });
  }

  onSearch() {
    this.loadProducts();
  }

  onMarketChange(event: any) {
    const val = event.detail.value;
    this.selectedMarketId = val === 'all' ? null : Number(val);
    this.selectedVendorId = null;
    this.loadProducts();
  }

  onVendorChange(event: any) {
    const val = event.detail.value;
    this.selectedVendorId = val === 'all' ? null : Number(val);
    this.loadProducts();
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

  private refreshCartQuantities() {
    this.cartQuantities = {};
    for (const item of this.cartService.getItems()) {
      this.cartQuantities[item.product.id] = item.quantity;
    }
  }

  getUnitLabel(unit: string): string {
    switch (unit) {
      case 'Kg': return 'kg';
      case 'Komad': return 'kom';
      case 'Veza': return 'veza';
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

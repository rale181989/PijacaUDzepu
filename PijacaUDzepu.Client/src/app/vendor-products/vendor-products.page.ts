import { Component, OnInit } from '@angular/core';
import { ProductService } from '../services/product.service';
import { Product } from '../models/product.model';
import { AlertController, ToastController } from '@ionic/angular/lazy';
import { environment } from '../../environments/environment';

@Component({
  selector: 'app-vendor-products',
  templateUrl: './vendor-products.page.html',
  styleUrls: ['./vendor-products.page.scss'],
  standalone: false,
})
export class VendorProductsPage implements OnInit {
  products: Product[] = [];
  loading = false;
  apiUrl = environment.apiUrl.replace('/api', '');

  constructor(
    private productService: ProductService,
    private alertController: AlertController,
    private toastController: ToastController
  ) {}

  ngOnInit() { this.loadProducts(); }
  ionViewWillEnter() { this.loadProducts(); }

  loadProducts() {
    this.loading = true;
    this.productService.getMyProducts().subscribe({
      next: p => { this.products = p; this.loading = false; },
      error: () => this.loading = false
    });
  }

  async toggleAvailability(product: Product) {
    const input = { name: product.name, price: product.price, unit: product.unit, category: product.category, note: product.note, isAvailable: !product.isAvailable };
    this.productService.update(product.id, input).subscribe({
      next: updated => {
        product.isAvailable = updated.isAvailable;
      }
    });
  }

  async confirmDelete(product: Product) {
    const alert = await this.alertController.create({
      header: 'Brisanje',
      message: `Da li želite da obrišete "${product.name}"?`,
      buttons: [
        { text: 'Otkaži', role: 'cancel' },
        { text: 'Obriši', handler: () => this.deleteProduct(product) }
      ]
    });
    await alert.present();
  }

  private deleteProduct(product: Product) {
    this.productService.delete(product.id).subscribe({
      next: async () => {
        this.products = this.products.filter(p => p.id !== product.id);
        const toast = await this.toastController.create({
          message: 'Proizvod obrisan', duration: 2000, color: 'success'
        });
        await toast.present();
      }
    });
  }

  getUnitLabel(unit: string): string {
    switch (unit) { case 'Kg': return 'kg'; case 'Gram': return 'g'; case 'Komad': return 'kom'; case 'Veza': return 'veza'; case 'Litar': return 'l'; case 'Pakovanje': return 'pak'; case 'Kutija': return 'kut'; case 'Flasa': return 'flaša'; default: return unit; }
  }

  getImageUrl(imageUrl?: string): string {
    if (!imageUrl) return 'assets/placeholder.svg';
    if (imageUrl.startsWith('http')) return imageUrl;
    return this.apiUrl + imageUrl;
  }

  doRefresh(event: any) {
    this.loadProducts();
    setTimeout(() => event.target.complete(), 500);
  }
}

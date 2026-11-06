import { Component, OnDestroy, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { ToastController } from '@ionic/angular/lazy';
import { ProductService } from '../services/product.service';
import { PRODUCT_CATEGORIES, ProductInput } from '../models/product.model';
import { TranslateService } from '../services/translate.service';
import { environment } from '../../environments/environment';
import { shrinkImage } from '../shared/image-file';

@Component({
  selector: 'app-vendor-product-form',
  templateUrl: './vendor-product-form.page.html',
  styleUrls: ['./vendor-product-form.page.scss'],
  standalone: false,
})
export class VendorProductFormPage implements OnInit, OnDestroy {
  productId: number | null = null;
  model: ProductInput = { name: '', price: 0, unit: 'Kg', category: 'Povrce', productClass: null, isAvailable: true };
  imageUrl: string | null = null;
  selectedFile: File | null = null;
  previewUrl: string | null = null;
  loading = false;
  saving = false;
  apiUrl = environment.apiUrl.replace('/api', '');
  categories = PRODUCT_CATEGORIES.map(value => ({ value, label: this.ts.t('cat.' + value) }));

  get isEdit(): boolean { return this.productId !== null; }

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private productService: ProductService,
    private toastController: ToastController,
    private ts: TranslateService
  ) {}

  ngOnInit() {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.productId = Number(id);
      this.loading = true;
      this.productService.getById(this.productId).subscribe({
        next: p => {
          this.model = { name: p.name, price: p.price, unit: p.unit, category: p.category, productClass: p.productClass, note: p.note, isAvailable: p.isAvailable, stockQuantity: p.stockQuantity ?? null };
          this.imageUrl = p.imageUrl || null;
          this.loading = false;
        },
        error: () => this.loading = false
      });
    }
  }

  async onFileSelected(event: any) {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = ''; // ista slika izabrana ponovo opet okida (change)
    if (!file) return;
    this.selectedFile = await shrinkImage(file);
    if (this.previewUrl) URL.revokeObjectURL(this.previewUrl);
    this.previewUrl = URL.createObjectURL(this.selectedFile);
  }

  ngOnDestroy() {
    if (this.previewUrl) URL.revokeObjectURL(this.previewUrl);
  }

  async save() {
    this.saving = true;
    const obs = this.isEdit
      ? this.productService.update(this.productId!, this.model)
      : this.productService.create(this.model);

    obs.subscribe({
      next: async (product) => {
        if (this.selectedFile) {
          this.productService.uploadImage(product.id, this.selectedFile).subscribe({
            next: async () => {
              this.saving = false;
              await this.showSuccess();
              this.router.navigateByUrl('/vendor-products');
            },
            // Proizvod je sačuvan, ali slika nije: ostaje se na formi da prodavac vidi zašto i pokuša ponovo
            error: async (err) => {
              this.saving = false;
              if (!this.isEdit) this.productId = product.id;
              const toast = await this.toastController.create({
                message: 'Proizvod je sačuvan, ali slika nije: ' + (err.error?.message || 'greška pri slanju slike.'),
                duration: 5000, color: 'danger'
              });
              await toast.present();
            }
          });
        } else {
          this.saving = false;
          await this.showSuccess();
          this.router.navigateByUrl('/vendor-products');
        }
      },
      error: async (err) => {
        this.saving = false;
        const toast = await this.toastController.create({
          message: err.error?.message || 'Greška pri čuvanju', duration: 3000, color: 'danger'
        });
        await toast.present();
      }
    });
  }

  private async showSuccess() {
    const toast = await this.toastController.create({
      message: this.isEdit ? 'Proizvod ažuriran' : 'Proizvod dodat', duration: 2000, color: 'success'
    });
    await toast.present();
  }

  getImageUrl(imageUrl?: string | null): string {
    if (!imageUrl) return 'assets/placeholder.svg';
    if (imageUrl.startsWith('http')) return imageUrl;
    return this.apiUrl + imageUrl;
  }
}

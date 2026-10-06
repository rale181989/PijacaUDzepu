import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { ToastController } from '@ionic/angular/lazy';
import { ProductService } from '../services/product.service';
import { ProductInput } from '../models/product.model';
import { environment } from '../../environments/environment';

@Component({
  selector: 'app-vendor-product-form',
  templateUrl: './vendor-product-form.page.html',
  styleUrls: ['./vendor-product-form.page.scss'],
  standalone: false,
})
export class VendorProductFormPage implements OnInit {
  productId: number | null = null;
  model: ProductInput = { name: '', price: 0, unit: 'Kg', category: 'Povrce', isAvailable: true };
  imageUrl: string | null = null;
  selectedFile: File | null = null;
  loading = false;
  saving = false;
  apiUrl = environment.apiUrl.replace('/api', '');

  get isEdit(): boolean { return this.productId !== null; }

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private productService: ProductService,
    private toastController: ToastController
  ) {}

  ngOnInit() {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.productId = Number(id);
      this.loading = true;
      this.productService.getById(this.productId).subscribe({
        next: p => {
          this.model = { name: p.name, price: p.price, unit: p.unit, category: p.category, note: p.note, isAvailable: p.isAvailable };
          this.imageUrl = p.imageUrl || null;
          this.loading = false;
        },
        error: () => this.loading = false
      });
    }
  }

  onFileSelected(event: any) {
    const file = event.target.files[0];
    if (file) this.selectedFile = file;
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
            error: async () => {
              this.saving = false;
              await this.showSuccess();
              this.router.navigateByUrl('/vendor-products');
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

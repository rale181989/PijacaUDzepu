import { Component, OnDestroy } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { NavController, ToastController } from '@ionic/angular/lazy';
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
export class VendorProductFormPage implements OnDestroy {
  productId: number | null = null;
  model: ProductInput = VendorProductFormPage.emptyModel();
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
    private navController: NavController,
    private productService: ProductService,
    private toastController: ToastController,
    private ts: TranslateService
  ) {}

  private static emptyModel(): ProductInput {
    return { name: '', price: 0, unit: 'Kg', category: 'Povrce', productClass: null, isAvailable: true };
  }

  // Ionic čuva stranicu u istoriji: pri svakom ulasku forma kreće od čistog stanja, inače bi "Dodaj proizvod"
  // mogao da otvori staru formu sa podacima prethodnog proizvoda, pa bi "Sačuvaj" napravio kopiju
  ionViewWillEnter() {
    this.productId = null;
    this.model = VendorProductFormPage.emptyModel();
    this.imageUrl = null;
    this.selectedFile = null;
    if (this.previewUrl) URL.revokeObjectURL(this.previewUrl);
    this.previewUrl = null;
    this.saving = false;
    this.loading = false;

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
    // Dupli dodir: dugme se onemogući tek posle osvežavanja ekrana, pa bi drugi dodir napravio duplikat proizvoda
    if (this.saving) return;
    this.saving = true;
    const obs = this.isEdit
      ? this.productService.update(this.productId!, this.model)
      : this.productService.create(this.model);

    obs.subscribe({
      next: async (product) => {
        if (this.selectedFile) {
          this.productService.uploadImage(product.id, this.selectedFile).subscribe({
            next: () => this.done(),
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
          this.done();
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

  // Sačuvano: dugme ostaje zaključano (drugi dodir ne sme da napravi duplikat), a forma se zatvara
  private done() {
    this.showSuccess();
    this.navController.navigateBack('/vendor-products');
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

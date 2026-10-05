import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { AlertController, ToastController } from '@ionic/angular/lazy';
import { VendorService } from '../services/vendor.service';
import { AuthService } from '../services/auth.service';
import { MarketService } from '../services/market.service';
import { StallService } from '../services/stall.service';
import { VendorInput } from '../models/vendor.model';
import { Market } from '../models/market.model';
import { Stall } from '../models/stall.model';

@Component({
  selector: 'app-admin-vendor-form',
  templateUrl: './admin-vendor-form.page.html',
  styleUrls: ['./admin-vendor-form.page.scss'],
  standalone: false,
})
export class AdminVendorFormPage implements OnInit {
  vendorId: number | null = null;
  model: VendorInput = { marketId: 0, name: '', description: '', address: '', phone: '' };
  markets: Market[] = [];
  stalls: Stall[] = [];
  adminFirstName = '';
  adminLastName = '';
  adminUserName = '';
  showAdminForm = false;
  showCredentials = false;
  generatedPassword = '';
  generatedUserName = '';
  loading = false;
  saving = false;

  get isEdit(): boolean { return this.vendorId !== null; }

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private vendorService: VendorService,
    private marketService: MarketService,
    private stallService: StallService,
    private authService: AuthService,
    private alertController: AlertController,
    private toastController: ToastController
  ) {}

  ngOnInit() {
    this.marketService.getAll().subscribe(m => this.markets = m);
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.vendorId = Number(id);
      this.loading = true;
      this.vendorService.getById(this.vendorId).subscribe({
        next: v => {
          this.model = { marketId: v.marketId, stallId: v.stallId, name: v.name, description: v.description, address: v.address, phone: v.phone };
          if (v.marketId) this.loadStalls(v.marketId);
          this.loading = false;
        },
        error: () => this.loading = false
      });
    }
  }

  onMarketChange() {
    this.model.stallId = undefined;
    this.stalls = [];
    if (this.model.marketId) {
      this.loadStalls(this.model.marketId);
      const market = this.markets.find(m => m.id === this.model.marketId);
      if (market?.address) {
        this.model.address = market.address;
      }
    }
  }

  loadStalls(marketId: number) {
    this.stallService.getByMarket(marketId).subscribe(s => this.stalls = s);
  }

  save() {
    this.saving = true;
    const obs = this.isEdit
      ? this.vendorService.update(this.vendorId!, this.model)
      : this.vendorService.create(this.model);

    obs.subscribe({
      next: async (vendor) => {
        this.saving = false;
        if (!this.isEdit) {
          this.vendorId = vendor.id;
          this.showAdminForm = true;
          const toast = await this.toastController.create({
            message: 'Prodavac kreiran. Kreirajte nalog.', duration: 3000, color: 'success'
          });
          await toast.present();
        } else {
          const toast = await this.toastController.create({
            message: 'Prodavac ažuriran', duration: 2000, color: 'success'
          });
          await toast.present();
          this.router.navigateByUrl('/admin-vendors');
        }
      },
      error: async (err) => {
        this.saving = false;
        const toast = await this.toastController.create({
          message: err.error?.message || 'Greška', duration: 3000, color: 'danger'
        });
        await toast.present();
      }
    });
  }

  createAdmin() {
    if (!this.vendorId || !this.adminFirstName || !this.adminLastName || !this.adminUserName) return;

    this.saving = true;
    this.generatedPassword = 'vendor1234';

    this.authService.createVendorAdmin({
      vendorId: this.vendorId,
      userName: this.adminUserName,
      password: this.generatedPassword,
      firstName: this.adminFirstName,
      lastName: this.adminLastName
    }).subscribe({
      next: async () => {
        this.saving = false;
        this.generatedUserName = this.adminUserName;
        this.showAdminForm = false;
        this.showCredentials = true;
      },
      error: async (err) => {
        this.saving = false;
        const toast = await this.toastController.create({
          message: err.error?.message || 'Greška', duration: 3000, color: 'danger'
        });
        await toast.present();
      }
    });
  }

  async copyCredentials() {
    const text = `Korisničko ime: ${this.generatedUserName}\nLozinka: ${this.generatedPassword}`;
    await navigator.clipboard.writeText(text);
    const toast = await this.toastController.create({
      message: 'Podaci kopirani!', duration: 1500, color: 'success'
    });
    await toast.present();
  }

}

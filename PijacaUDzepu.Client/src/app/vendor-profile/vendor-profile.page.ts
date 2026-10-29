import { Component, OnInit } from '@angular/core';
import { ToastController } from '@ionic/angular/lazy';
import { VendorService } from '../services/vendor.service';
import { AuthService } from '../services/auth.service';
import { MarketService } from '../services/market.service';
import { StallService } from '../services/stall.service';
import { VendorInput } from '../models/vendor.model';
import { Market } from '../models/market.model';
import { Stall } from '../models/stall.model';

@Component({
  selector: 'app-vendor-profile',
  templateUrl: './vendor-profile.page.html',
  styleUrls: ['./vendor-profile.page.scss'],
  standalone: false,
})
export class VendorProfilePage implements OnInit {
  model: VendorInput = { marketId: 0, name: '', description: '', phones: [''] };
  markets: Market[] = [];
  stalls: Stall[] = [];
  loading = true;
  saving = false;
  currentPassword = '';
  newPassword = '';
  confirmPassword = '';
  changingPassword = false;

  get passwordsMatch(): boolean {
    return this.newPassword === this.confirmPassword;
  }

  readonly maxPhones = 5;

  addPhone() {
    this.model.phones.push('');
  }

  removePhone(index: number) {
    this.model.phones.splice(index, 1);
  }

  trackByIndex(index: number): number {
    return index;
  }

  constructor(
    private vendorService: VendorService,
    private authService: AuthService,
    private marketService: MarketService,
    private stallService: StallService,
    private toastController: ToastController
  ) {}

  ngOnInit() {
    this.marketService.getAll().subscribe(m => this.markets = m);
    this.vendorService.getMyVendor().subscribe({
      next: v => {
        this.model = {
          marketId: v.marketId,
          stallId: v.stallId,
          name: v.name,
          description: v.description,
          address: v.address,
          phones: v.phones?.length ? [...v.phones] : [''],
          // Usluge se više ne menjaju u profilu (dostavu preuzima kurirska služba),
          // ali se šalju nazad nepromenjene da se postojeća podešavanja ne bi izgubila
          acceptsReservations: v.acceptsReservations,
          offersDelivery: v.offersDelivery,
          minOrderAmount: v.minOrderAmount,
          deliveryRadiusKm: v.deliveryRadiusKm,
          deliverySchedule: v.deliverySchedule ?? []
        };
        if (v.marketId) this.loadStalls(v.marketId);
        this.loading = false;
      },
      error: () => this.loading = false
    });
  }

  onMarketChange() {
    this.model.stallId = undefined;
    this.stalls = [];
    if (this.model.marketId) {
      this.loadStalls(this.model.marketId);
    }
  }

  loadStalls(marketId: number) {
    this.stallService.getByMarket(marketId).subscribe(s => this.stalls = s);
  }

  async save() {
    this.saving = true;
    this.vendorService.updateMyVendor(this.model).subscribe({
      next: async () => {
        this.saving = false;
        const toast = await this.toastController.create({
          message: 'Profil ažuriran', duration: 2000, color: 'success'
        });
        await toast.present();
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

  async changePassword() {
    if (!this.currentPassword || !this.newPassword || !this.passwordsMatch || this.newPassword.length < 4) return;
    this.changingPassword = true;
    this.authService.changePassword(this.currentPassword, this.newPassword).subscribe({
      next: async () => {
        this.changingPassword = false;
        this.currentPassword = '';
        this.newPassword = '';
        this.confirmPassword = '';
        const toast = await this.toastController.create({
          message: 'Lozinka promenjena!', duration: 2000, color: 'success'
        });
        await toast.present();
      },
      error: async (err) => {
        this.changingPassword = false;
        const toast = await this.toastController.create({
          message: err.error?.message || 'Greška', duration: 3000, color: 'danger'
        });
        await toast.present();
      }
    });
  }
}

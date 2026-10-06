import { Component, OnInit } from '@angular/core';
import { ToastController } from '@ionic/angular/lazy';
import { VendorService } from '../services/vendor.service';
import { AuthService } from '../services/auth.service';
import { MarketService } from '../services/market.service';
import { StallService } from '../services/stall.service';
import { VendorInput, DeliveryScheduleEntry } from '../models/vendor.model';
import { Market } from '../models/market.model';
import { Stall } from '../models/stall.model';

@Component({
  selector: 'app-vendor-profile',
  templateUrl: './vendor-profile.page.html',
  styleUrls: ['./vendor-profile.page.scss'],
  standalone: false,
})
export class VendorProfilePage implements OnInit {
  model: VendorInput = { marketId: 0, name: '', description: '', phone: '' };
  markets: Market[] = [];
  stalls: Stall[] = [];
  loading = true;
  saving = false;
  currentPassword = '';
  newPassword = '';
  confirmPassword = '';
  changingPassword = false;

  allDays = [
    { value: 1, label: 'Ponedeljak' },
    { value: 2, label: 'Utorak' },
    { value: 3, label: 'Sreda' },
    { value: 4, label: 'Četvrtak' },
    { value: 5, label: 'Petak' },
    { value: 6, label: 'Subota' },
    { value: 7, label: 'Nedelja' },
  ];

  get passwordsMatch(): boolean {
    return this.newPassword === this.confirmPassword;
  }

  isDayEnabled(day: number): boolean {
    return !!(this.model.deliverySchedule ?? []).find(e => e.day === day);
  }

  getEntry(day: number): DeliveryScheduleEntry | undefined {
    return (this.model.deliverySchedule ?? []).find(e => e.day === day);
  }

  toggleDay(day: number) {
    if (!this.model.deliverySchedule) this.model.deliverySchedule = [];
    const idx = this.model.deliverySchedule.findIndex(e => e.day === day);
    if (idx >= 0) {
      this.model.deliverySchedule.splice(idx, 1);
    } else {
      this.model.deliverySchedule.push({ day, from: '08:00', to: '18:00' });
      this.model.deliverySchedule.sort((a, b) => a.day - b.day);
    }
  }

  onTimeChange(day: number, field: 'from' | 'to', value: string) {
    const entry = this.getEntry(day);
    if (entry) entry[field] = value;
  }

  onDeliveryToggle() {
    if (!this.model.offersDelivery) {
      this.model.minOrderAmount = undefined;
      this.model.deliveryRadiusKm = undefined;
      this.model.deliverySchedule = [];
    }
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
          phone: v.phone,
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

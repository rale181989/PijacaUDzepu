import { Component, OnInit } from '@angular/core';
import { ToastController } from '@ionic/angular/lazy';
import { VendorService } from '../services/vendor.service';
import { AuthService } from '../services/auth.service';
import { MarketService } from '../services/market.service';
import { StallService } from '../services/stall.service';
import { VendorInput } from '../models/vendor.model';
import { TranslateService } from '../services/translate.service';
import { vendorClosedText } from '../services/vendor-status';
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

  // Radno vreme i dostupnost (posebno čuvanje: PUT my-vendor/availability)
  readonly dayLabels = ['Ponedeljak', 'Utorak', 'Sreda', 'Četvrtak', 'Petak', 'Subota', 'Nedelja'];
  // Kratki nazivi da stanu na uzak telefon ("Ponedeljak" je bio odsečen)
  readonly dayShort = ['Pon', 'Uto', 'Sre', 'Čet', 'Pet', 'Sub', 'Ned'];
  days = this.dayLabels.map((label, i) => ({ day: i + 1, label, short: this.dayShort[i], open: false, from: '07:00', to: '14:00' }));
  ordersPaused = false;
  earlyCloseToday: string | null = null;
  earlyCloseInput = '';
  // Poslednje sačuvano stanje: pauza i "samo danas" čuvaju se odmah, bez nesačuvanih izmena rasporeda
  private saved: { ordersPaused: boolean; openingHours: { day: number; from: string; to: string }[]; closedDates: string[]; earlyCloseToday: string | null } | null = null;
  closedDates: string[] = [];
  newClosedDate = '';
  statusText = '';
  isOpen = true;
  savingAvailability = false;
  readonly today = new Date().toISOString().slice(0, 10);

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
    private toastController: ToastController,
    private ts: TranslateService
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
        this.applyAvailability(v);
        this.loading = false;
      },
      error: () => this.loading = false
    });
  }

  private applyAvailability(v: { ordersPaused: boolean; openingHours: { day: number; from: string; to: string }[]; closedDates: string[]; earlyCloseToday?: string | null; isOpen: boolean; closedReason?: string | null; opensAt?: string | null; closesAt?: string | null }) {
    this.ordersPaused = v.ordersPaused;
    this.closedDates = [...(v.closedDates ?? [])];
    this.earlyCloseToday = v.earlyCloseToday ?? null;
    this.saved = { ordersPaused: v.ordersPaused, openingHours: [...(v.openingHours ?? [])], closedDates: [...(v.closedDates ?? [])], earlyCloseToday: this.earlyCloseToday };
    this.days = this.dayLabels.map((label, i) => {
      const h = v.openingHours?.find(x => x.day === i + 1);
      return { day: i + 1, label, short: this.dayShort[i], open: !!h, from: h?.from ?? '07:00', to: h?.to ?? '14:00' };
    });
    this.isOpen = v.isOpen;
    this.statusText = v.isOpen
      ? (v.closesAt ? `Sada primate porudžbine (do ${v.closesAt})` : 'Sada primate porudžbine')
      : vendorClosedText(this.ts, v.closedReason, v.opensAt);
  }

  // Vreme prvog uključenog dana prepisuje se na sve uključene dane
  copyFirstDay() {
    const first = this.days.find(d => d.open);
    if (!first) return;
    this.days.forEach(d => { if (d.open) { d.from = first.from; d.to = first.to; } });
  }

  addClosedDate() {
    if (!this.newClosedDate || this.closedDates.includes(this.newClosedDate)) return;
    this.closedDates = [...this.closedDates, this.newClosedDate].sort();
    this.newClosedDate = '';
  }

  removeClosedDate(date: string) {
    this.closedDates = this.closedDates.filter(d => d !== date);
  }

  // Pauza važi odmah (ranije je čekala dugme na dnu, pa su kupci i dalje poručivali)
  setPaused(paused: boolean) {
    if (!this.saved) return;
    this.saveQuick({ ...this.saved, ordersPaused: paused }, paused ? 'Pauza uključena – kupci ne mogu da poručuju' : 'Ponovo primate porudžbine');
  }

  closeToday() {
    if (!this.saved) return;
    this.saveQuick({ ...this.saved, closedDates: [...new Set([...this.saved.closedDates, this.today])].sort() }, 'Danas ne radite – kupci vide kada se otvarate');
  }

  setEarlyClose(time: string | null) {
    if (!this.saved) return;
    this.saveQuick({ ...this.saved, earlyCloseToday: time }, time ? `Danas zatvarate u ${time}` : 'Danas radite po rasporedu');
    this.earlyCloseInput = '';
  }

  // Čuva samo pauzu ili izuzetak za danas, uz poslednji sačuvani raspored (nesačuvane izmene rasporeda ostaju na ekranu)
  private saveQuick(state: NonNullable<VendorProfilePage['saved']>, message: string) {
    const draftDays = this.days.map(d => ({ ...d }));
    this.savingAvailability = true;
    this.vendorService.updateMyAvailability(state).subscribe({
      next: async v => {
        this.savingAvailability = false;
        this.applyAvailability(v);
        this.days = draftDays;
        const toast = await this.toastController.create({ message, duration: 2500, color: 'success', position: 'top' });
        await toast.present();
      },
      error: async err => {
        this.savingAvailability = false;
        if (this.saved) this.ordersPaused = this.saved.ordersPaused;
        const toast = await this.toastController.create({ message: err.error?.message || 'Greška pri čuvanju', duration: 3000, color: 'danger' });
        await toast.present();
      }
    });
  }

  saveAvailability() {
    this.savingAvailability = true;
    this.vendorService.updateMyAvailability({
      ordersPaused: this.ordersPaused,
      openingHours: this.days.filter(d => d.open).map(d => ({ day: d.day, from: d.from, to: d.to })),
      closedDates: this.closedDates,
      earlyCloseToday: this.earlyCloseToday
    }).subscribe({
      next: async v => {
        this.savingAvailability = false;
        this.applyAvailability(v);
        const toast = await this.toastController.create({ message: 'Radno vreme sačuvano', duration: 2000, color: 'success' });
        await toast.present();
      },
      error: async err => {
        this.savingAvailability = false;
        const toast = await this.toastController.create({ message: err.error?.message || 'Greška pri čuvanju', duration: 3000, color: 'danger' });
        await toast.present();
      }
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
    if (!this.currentPassword || !this.newPassword || !this.passwordsMatch || this.newPassword.length < 8) return;
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

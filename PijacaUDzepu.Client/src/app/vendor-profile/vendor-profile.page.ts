import { Component, OnInit, inject } from '@angular/core';
import { AlertController, ToastController } from '@ionic/angular/lazy';
import { Capacitor } from '@capacitor/core';
import { VendorService } from '../services/vendor.service';
import { AuthService } from '../services/auth.service';
import { MarketService } from '../services/market.service';
import { StallService } from '../services/stall.service';
import { VendorInput } from '../models/vendor.model';
import { TranslateService } from '../services/translate.service';
import { vendorClosedText } from '../services/vendor-status';
import { Market } from '../models/market.model';
import { Stall } from '../models/stall.model';
import { FeaturesService } from '../services/features.service';
import { GeoPoint } from '../shared/maps';
import { Vendor } from '../models/vendor.model';

@Component({
  selector: 'app-vendor-profile',
  templateUrl: './vendor-profile.page.html',
  styleUrls: ['./vendor-profile.page.scss'],
  standalone: false,
})
export class VendorProfilePage implements OnInit {
  private vendorService = inject(VendorService);
  private authService = inject(AuthService);
  private marketService = inject(MarketService);
  private stallService = inject(StallService);
  private toastController = inject(ToastController);
  private ts = inject(TranslateService);
  features = inject(FeaturesService);
  private alertController = inject(AlertController);

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

  // Lokacija tezge na mapi (funkcija stallLocation): "Ovde sam" uzima GPS i odmah čuva; dodir na mapu pomera tačku
  marketPoint: GeoPoint | null = null;
  marketName = '';
  savedPoint: GeoPoint | null = null;
  draftPoint: GeoPoint | null = null;
  locating = false;
  savingLocation = false;
  locationNote = '';
  locationWarn = false;
  gpsAccuracy: number | null = null;
  savedAt: string | null = null;

  get locationDirty(): boolean {
    const a = this.draftPoint, b = this.savedPoint;
    return !!a && (!b || a.lat !== b.lat || a.lng !== b.lng);
  }

  addPhone() {
    this.model.phones.push('');
  }

  removePhone(index: number) {
    this.model.phones.splice(index, 1);
  }

  trackByIndex(index: number): number {
    return index;
  }

  ngOnInit() {
    this.features.load();
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
        this.applyLocation(v);
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

  private applyLocation(v: Vendor) {
    this.marketName = v.marketName;
    this.marketPoint = v.marketLatitude != null && v.marketLongitude != null ? { lat: v.marketLatitude, lng: v.marketLongitude } : null;
    this.savedPoint = v.stallLatitude != null && v.stallLongitude != null ? { lat: v.stallLatitude, lng: v.stallLongitude } : null;
    this.savedAt = v.stallLocationAt ?? null;
    this.draftPoint = this.savedPoint;
    this.gpsAccuracy = null;
  }

  // "Ovde sam": telefon uzima GPS (traži dozvolu za lokaciju). Precizan GPS (do ±60 m) se odmah čuva; neprecizan se
  // samo prikaže na mapi sa krugom preciznosti i čeka da prodavac dodirne tačno mesto i sačuva (ne prepisuje dobru tačku).
  useGps() {
    if (!navigator.geolocation) {
      this.setNote('Ovaj uređaj ne daje lokaciju. Dodirnite mapu na mesto tezge, pa „Sačuvaj tačku”.', true);
      return;
    }
    this.locating = true;
    navigator.geolocation.getCurrentPosition(
      pos => {
        this.locating = false;
        const accuracy = Math.round(pos.coords.accuracy);
        this.draftPoint = { lat: pos.coords.latitude, lng: pos.coords.longitude };
        this.gpsAccuracy = accuracy;
        if (accuracy <= 60) {
          this.saveLocation(`Tačka je sačuvana (GPS ± ${accuracy} m).`);
        } else {
          this.setNote(`GPS je neprecizan (± ${accuracy} m), pa tačka NIJE sačuvana. Plavi krug pokazuje gde ste možda. `
            + 'Dodirnite tačno mesto tezge na mapi, pa „Sačuvaj tačku”, ili pokušajte ponovo napolju.', true);
        }
      },
      err => {
        this.locating = false;
        const settings = Capacitor.isNativePlatform()
          ? 'u podešavanjima telefona (Aplikacije → Pijaca u Džepu → Dozvole → Lokacija)'
          : 'u podešavanjima pregledača za ovaj sajt (ikonica katanca pored adrese)';
        this.setNote(err.code === err.PERMISSION_DENIED
          ? `Aplikacija nema dozvolu za lokaciju. Uključite je ${settings} ili dodirnite mapu na mesto tezge, pa „Sačuvaj tačku”.`
          : 'Lokacija trenutno nije dostupna. Pokušajte ponovo napolju ili dodirnite mapu na mesto tezge, pa „Sačuvaj tačku”.', true);
      },
      { enableHighAccuracy: true, timeout: 20000, maximumAge: 0 }
    );
  }

  onMapPick(p: GeoPoint) {
    this.draftPoint = p;
    this.gpsAccuracy = null;
    this.setNote('');
  }

  private setNote(text: string, warn = false) {
    this.locationNote = text;
    this.locationWarn = warn;
  }

  // Nesačuvana tačka se odbacuje, prikazuje se sačuvana
  discardLocation() {
    this.draftPoint = this.savedPoint;
    this.gpsAccuracy = null;
    this.setNote('');
  }

  saveLocation(note = 'Tačka je sačuvana.'): Promise<boolean> {
    const p = this.draftPoint;
    if (!p || this.savingLocation) return Promise.resolve(false);
    this.savingLocation = true;
    return new Promise(resolve => this.vendorService.setMyLocation(p.lat, p.lng).subscribe({
      next: v => {
        this.savingLocation = false;
        const accuracy = this.gpsAccuracy;
        this.applyLocation(v);
        this.gpsAccuracy = accuracy;
        this.setNote(note);
        resolve(true);
      },
      error: err => {
        this.savingLocation = false;
        this.setNote(err.error?.message || 'Tačka nije sačuvana.', true);
        resolve(false);
      }
    }));
  }

  async clearLocation() {
    if (this.savingLocation) return;
    const alert = await this.alertController.create({
      header: 'Ukloniti tačku tezge?',
      message: 'Kurir i kupci će videti samo pijacu i broj tezge, dok ponovo ne pritisnete „Ovde sam”.',
      buttons: [
        { text: 'Odustani', role: 'cancel' },
        { text: 'Ukloni', role: 'destructive', handler: () => this.doClearLocation() }
      ]
    });
    await alert.present();
  }

  private doClearLocation() {
    this.savingLocation = true;
    this.vendorService.clearMyLocation().subscribe({
      next: v => {
        this.savingLocation = false;
        this.applyLocation(v);
        this.setNote('Tačka je uklonjena – kurir i kupci vide samo pijacu i broj tezge.');
      },
      error: err => {
        this.savingLocation = false;
        this.setNote(err.error?.message || 'Tačka nije uklonjena.', true);
      }
    });
  }

  // Odlazak sa stranice sa nesačuvanom tačkom: pitanje umesto tihog gubitka (canDeactivate u modulu)
  async canLeave(): Promise<boolean> {
    if (!this.locationDirty) return true;
    const alert = await this.alertController.create({
      header: 'Nova tačka tezge nije sačuvana',
      message: 'Da li da je sačuvam pre izlaska?',
      buttons: [
        { text: 'Ostani', role: 'cancel' },
        { text: 'Ne čuvaj', role: 'discard' },
        { text: 'Sačuvaj', role: 'save' }
      ]
    });
    await alert.present();
    const { role } = await alert.onDidDismiss();
    if (role === 'save') return this.saveLocation();
    if (role === 'discard') { this.discardLocation(); return true; }
    return false;
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
    if (this.locationDirty) this.saveLocation();
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

  private async toast(message: string, color: string) {
    const toast = await this.toastController.create({ message, duration: 4000, color, position: 'top' });
    await toast.present();
  }
}

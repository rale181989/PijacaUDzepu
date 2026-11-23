import { Component, OnInit, inject } from '@angular/core';
import { AlertController, ToastController } from '@ionic/angular/lazy';
import { MarketService } from '../services/market.service';
import { StallService } from '../services/stall.service';
import { Market, MarketInput } from '../models/market.model';
import { Stall } from '../models/stall.model';

@Component({
  selector: 'app-admin-markets',
  templateUrl: './admin-markets.page.html',
  styleUrls: ['./admin-markets.page.scss'],
  standalone: false,
})
export class AdminMarketsPage implements OnInit {
  private marketService = inject(MarketService);
  private stallService = inject(StallService);
  private alertController = inject(AlertController);
  private toastController = inject(ToastController);

  markets: Market[] = [];
  stalls: Record<number, Stall[]> = {};
  expandedMarketId: number | null = null;
  loading = true;

  ngOnInit() {
    this.loadMarkets();
  }

  loadMarkets() {
    this.loading = true;
    this.marketService.getAllIncludingInactive().subscribe({
      next: m => { this.markets = m; this.loading = false; },
      error: () => this.loading = false
    });
  }

  toggleExpand(market: Market) {
    if (this.expandedMarketId === market.id) {
      this.expandedMarketId = null;
    } else {
      this.expandedMarketId = market.id;
      if (!this.stalls[market.id]) {
        this.stallService.getByMarket(market.id).subscribe(s => this.stalls[market.id] = s);
      }
    }
  }

  // Redosled polja u dijalogu (Ionic alert ne prikazuje natpise uz tekstualna polja)
  readonly fieldsHelp = 'Polja redom: naziv, adresa, centar na mapi, opis. Centar: u Google mapama dugo držite prst '
    + '(na računaru desni klik) na sredini pijace i kopirajte brojeve, npr. 45.2529, 19.8371.';

  // "45.2529, 19.8371" (kopirano iz Google mapa) -> koordinate; prazno = bez centra; greška = poruka za administratora
  private parseCenter(text?: string): { latitude: number | null; longitude: number | null } | string {
    if (!text?.trim()) return { latitude: null, longitude: null };
    const m = text.trim().replace(/[()]/g, '').match(/^(-?\d{1,2}(?:\.\d+)?)\s*[,; ]\s*(-?\d{1,3}(?:\.\d+)?)$/);
    if (!m) return 'Centar upišite kao dva broja sa tačkom, odvojena zarezom: 45.2529, 19.8371.';
    const latitude = Number(m[1]), longitude = Number(m[2]);
    const inSerbia = (lat: number, lng: number) => lat >= 41.8 && lat <= 46.2 && lng >= 18.8 && lng <= 23.1;
    if (inSerbia(latitude, longitude)) return { latitude, longitude };
    return inSerbia(longitude, latitude)
      ? `Širina i dužina su zamenjene: upišite ${longitude}, ${latitude} (prvo broj oko 45).`
      : 'Centar pijace mora biti u Srbiji (npr. 45.2529, 19.8371).';
  }

  hasCenter(market: Market): boolean {
    return market.latitude != null && market.longitude != null;
  }

  centerText(market: Market): string {
    return this.hasCenter(market) ? `Centar na mapi: ${market.latitude}, ${market.longitude}` : 'Centar na mapi nije upisan';
  }

  async addMarket() {
    const alert = await this.alertController.create({
      header: 'Nova pijaca',
      message: this.fieldsHelp,
      inputs: [
        { name: 'name', placeholder: 'Naziv pijace', type: 'text', attributes: { 'aria-label': 'Naziv' } },
        { name: 'address', placeholder: 'Adresa', type: 'text', attributes: { 'aria-label': 'Adresa' } },
        { name: 'center', placeholder: 'Centar na mapi: 45.2529, 19.8371', type: 'text', attributes: { 'aria-label': 'Centar na mapi' } },
        { name: 'description', placeholder: 'Opis', type: 'textarea' },
      ],
      buttons: [
        { text: 'Otkaži', role: 'cancel' },
        {
          text: 'Kreiraj',
          handler: (data) => {
            if (!data.name?.trim()) return false;
            const center = this.parseCenter(data.center);
            if (typeof center === 'string') { this.showToast(center, 'warning'); return false; }
            const dto: MarketInput = { name: data.name.trim(), address: data.address?.trim(), description: data.description?.trim(), ...center };
            this.marketService.create(dto).subscribe({
              next: () => { this.loadMarkets(); this.showToast('Pijaca kreirana', 'success'); },
              error: err => this.showToast(err.error?.message || 'Greška', 'danger')
            });
            return true;
          }
        }
      ]
    });
    await alert.present();
  }

  async editMarket(market: Market) {
    const alert = await this.alertController.create({
      header: 'Izmeni pijacu',
      message: this.fieldsHelp,
      inputs: [
        { name: 'name', value: market.name, placeholder: 'Naziv', type: 'text', attributes: { 'aria-label': 'Naziv' } },
        { name: 'address', value: market.address || '', placeholder: 'Adresa', type: 'text', attributes: { 'aria-label': 'Adresa' } },
        { name: 'center', value: market.latitude != null ? `${market.latitude}, ${market.longitude}` : '', placeholder: 'Centar na mapi: 45.2529, 19.8371', type: 'text', attributes: { 'aria-label': 'Centar na mapi' } },
        { name: 'description', value: market.description || '', placeholder: 'Opis', type: 'textarea' },
      ],
      buttons: [
        { text: 'Otkaži', role: 'cancel' },
        {
          text: 'Sačuvaj',
          handler: (data) => {
            if (!data.name?.trim()) return false;
            const center = this.parseCenter(data.center);
            if (typeof center === 'string') { this.showToast(center, 'warning'); return false; }
            const dto: MarketInput = { name: data.name.trim(), address: data.address?.trim(), description: data.description?.trim(), ...center };
            this.marketService.update(market.id, dto).subscribe({
              next: () => { this.loadMarkets(); this.showToast('Pijaca ažurirana', 'success'); },
              error: err => this.showToast(err.error?.message || 'Greška', 'danger')
            });
            return true;
          }
        }
      ]
    });
    await alert.present();
  }

  toggleMarketActive(market: Market) {
    this.marketService.toggleActive(market.id).subscribe({
      next: () => { this.loadMarkets(); this.showToast(market.isActive ? 'Pijaca deaktivirana' : 'Pijaca aktivirana', 'success'); },
      error: err => this.showToast(err.error?.message || 'Greška', 'danger')
    });
  }

  async addStall(marketId: number) {
    const alert = await this.alertController.create({
      header: 'Nova tezga',
      inputs: [
        { name: 'label', placeholder: 'Broj/oznaka tezge', type: 'text' },
      ],
      buttons: [
        { text: 'Otkaži', role: 'cancel' },
        {
          text: 'Dodaj',
          handler: (data) => {
            if (!data.label?.trim()) return false;
            this.stallService.create({ marketId, label: data.label.trim() }).subscribe({
              next: () => {
                this.stallService.getByMarket(marketId).subscribe(s => this.stalls[marketId] = s);
                this.loadMarkets();
                this.showToast('Tezga dodata', 'success');
              },
              error: () => this.showToast('Greška (možda duplikat?)', 'danger')
            });
            return true;
          }
        }
      ]
    });
    await alert.present();
  }

  async editStall(stall: Stall) {
    const alert = await this.alertController.create({
      header: 'Izmeni tezgu',
      inputs: [
        { name: 'label', value: stall.label, placeholder: 'Broj/oznaka', type: 'text' },
      ],
      buttons: [
        { text: 'Otkaži', role: 'cancel' },
        {
          text: 'Sačuvaj',
          handler: (data) => {
            if (!data.label?.trim()) return false;
            this.stallService.update(stall.id, { marketId: stall.marketId, label: data.label.trim() }).subscribe({
              next: () => {
                this.stallService.getByMarket(stall.marketId).subscribe(s => this.stalls[stall.marketId] = s);
                this.showToast('Tezga ažurirana', 'success');
              },
              error: err => this.showToast(err.error?.message || 'Greška', 'danger')
            });
            return true;
          }
        }
      ]
    });
    await alert.present();
  }

  async deleteStall(stall: Stall) {
    const alert = await this.alertController.create({
      header: 'Obriši tezgu',
      message: `Da li ste sigurni da želite da obrišete tezgu ${stall.label}?`,
      buttons: [
        { text: 'Ne', role: 'cancel' },
        {
          text: 'Da, obriši',
          role: 'destructive',
          handler: () => {
            this.stallService.delete(stall.id).subscribe({
              next: () => {
                this.stallService.getByMarket(stall.marketId).subscribe(s => this.stalls[stall.marketId] = s);
                this.showToast('Tezga obrisana', 'success');
              },
              error: err => this.showToast(err.error?.message || 'Greška', 'danger')
            });
          }
        }
      ]
    });
    await alert.present();
  }

  private async showToast(message: string, color: string) {
    const toast = await this.toastController.create({ message, duration: 2000, color });
    await toast.present();
  }
}

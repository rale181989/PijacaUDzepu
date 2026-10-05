import { Component, OnInit } from '@angular/core';
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
  markets: Market[] = [];
  stalls: Record<number, Stall[]> = {};
  expandedMarketId: number | null = null;
  loading = true;

  constructor(
    private marketService: MarketService,
    private stallService: StallService,
    private alertController: AlertController,
    private toastController: ToastController
  ) {}

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

  async addMarket() {
    const alert = await this.alertController.create({
      header: 'Nova pijaca',
      inputs: [
        { name: 'name', placeholder: 'Naziv pijace', type: 'text' },
        { name: 'address', placeholder: 'Adresa', type: 'text' },
        { name: 'description', placeholder: 'Opis', type: 'textarea' },
      ],
      buttons: [
        { text: 'Otkaži', role: 'cancel' },
        {
          text: 'Kreiraj',
          handler: (data) => {
            if (!data.name?.trim()) return false;
            const dto: MarketInput = { name: data.name.trim(), address: data.address?.trim(), description: data.description?.trim() };
            this.marketService.create(dto).subscribe({
              next: () => { this.loadMarkets(); this.showToast('Pijaca kreirana', 'success'); },
              error: () => this.showToast('Greška', 'danger')
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
      inputs: [
        { name: 'name', value: market.name, placeholder: 'Naziv', type: 'text' },
        { name: 'address', value: market.address || '', placeholder: 'Adresa', type: 'text' },
        { name: 'description', value: market.description || '', placeholder: 'Opis', type: 'textarea' },
      ],
      buttons: [
        { text: 'Otkaži', role: 'cancel' },
        {
          text: 'Sačuvaj',
          handler: (data) => {
            if (!data.name?.trim()) return false;
            const dto: MarketInput = { name: data.name.trim(), address: data.address?.trim(), description: data.description?.trim() };
            this.marketService.update(market.id, dto).subscribe({
              next: () => { this.loadMarkets(); this.showToast('Pijaca ažurirana', 'success'); },
              error: () => this.showToast('Greška', 'danger')
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
      error: () => this.showToast('Greška', 'danger')
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
              error: () => this.showToast('Greška', 'danger')
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
              error: () => this.showToast('Greška', 'danger')
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

import { Component, OnInit } from '@angular/core';
import { VendorService } from '../services/vendor.service';
import { Vendor } from '../models/vendor.model';
import { ToastController } from '@ionic/angular/lazy';

@Component({
  selector: 'app-admin-vendors',
  templateUrl: './admin-vendors.page.html',
  styleUrls: ['./admin-vendors.page.scss'],
  standalone: false,
})
export class AdminVendorsPage implements OnInit {
  vendors: Vendor[] = [];
  loading = false;

  constructor(
    private vendorService: VendorService,
    private toastController: ToastController
  ) {}

  ngOnInit() { this.loadVendors(); }
  ionViewWillEnter() { this.loadVendors(); }

  loadVendors() {
    this.loading = true;
    this.vendorService.getAllIncludingInactive().subscribe({
      next: v => { this.vendors = v; this.loading = false; },
      error: () => this.loading = false
    });
  }

  async toggleActive(vendor: Vendor) {
    this.vendorService.toggleActive(vendor.id).subscribe({
      next: async () => {
        vendor.isActive = !vendor.isActive;
        const toast = await this.toastController.create({
          message: vendor.isActive ? 'Prodavac aktiviran' : 'Prodavac deaktiviran',
          duration: 2000, color: 'success'
        });
        await toast.present();
      }
    });
  }

  doRefresh(event: any) {
    this.loadVendors();
    setTimeout(() => event.target.complete(), 500);
  }
}

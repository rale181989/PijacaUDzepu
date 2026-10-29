import { Component, OnInit } from '@angular/core';
import { ToastController } from '@ionic/angular/lazy';
import { CourierService } from '../services/courier.service';
import { AuthService } from '../services/auth.service';
import { CourierProfileInput, CourierVehicle } from '../models/courier.model';

@Component({
  selector: 'app-courier-profile',
  templateUrl: './courier-profile.page.html',
  styleUrls: ['./courier-profile.page.scss'],
  standalone: false,
})
export class CourierProfilePage implements OnInit {
  model: CourierProfileInput = { firstName: '', lastName: '', phoneNumber: '', vehicle: null, licensePlate: '' };
  userName = '';
  loading = true;
  saving = false;
  currentPassword = '';
  newPassword = '';
  confirmPassword = '';
  changingPassword = false;

  vehicles: { value: CourierVehicle; label: string }[] = [
    { value: 'Bicikl', label: 'Bicikl' },
    { value: 'Skuter', label: 'Skuter' },
    { value: 'Motor', label: 'Motor' },
    { value: 'Automobil', label: 'Automobil' },
    { value: 'Kombi', label: 'Kombi' },
  ];

  get passwordsMatch(): boolean {
    return this.newPassword === this.confirmPassword;
  }

  get canSave(): boolean {
    return !!(this.model.firstName.trim() && this.model.lastName.trim() && this.model.phoneNumber.trim());
  }

  constructor(
    private courierService: CourierService,
    private authService: AuthService,
    private toastController: ToastController
  ) {}

  ngOnInit() {
    this.courierService.getProfile().subscribe({
      next: p => {
        this.userName = p.userName;
        this.model = {
          firstName: p.firstName,
          lastName: p.lastName,
          phoneNumber: p.phoneNumber ?? '',
          vehicle: p.vehicle ?? null,
          licensePlate: p.licensePlate ?? ''
        };
        this.loading = false;
      },
      error: () => this.loading = false
    });
  }

  save() {
    if (!this.canSave) return;
    this.saving = true;
    this.courierService.updateProfile(this.model).subscribe({
      next: async () => {
        this.saving = false;
        await this.toast('Podaci sačuvani', 'success');
      },
      error: async err => {
        this.saving = false;
        await this.toast(err.error?.message || 'Greška pri čuvanju', 'danger');
      }
    });
  }

  changePassword() {
    if (!this.currentPassword || !this.newPassword || !this.passwordsMatch || this.newPassword.length < 4) return;
    this.changingPassword = true;
    this.authService.changePassword(this.currentPassword, this.newPassword).subscribe({
      next: async () => {
        this.changingPassword = false;
        this.currentPassword = '';
        this.newPassword = '';
        this.confirmPassword = '';
        await this.toast('Lozinka promenjena!', 'success');
      },
      error: async err => {
        this.changingPassword = false;
        await this.toast(err.error?.message || 'Greška', 'danger');
      }
    });
  }

  private async toast(message: string, color: string) {
    const toast = await this.toastController.create({ message, duration: 2000, color });
    await toast.present();
  }
}

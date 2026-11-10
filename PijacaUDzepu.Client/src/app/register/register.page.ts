import { Component } from '@angular/core';
import { Router } from '@angular/router';
import { ToastController } from '@ionic/angular/lazy';
import { AuthService } from '../services/auth.service';
import { TranslateService } from '../services/translate.service';
import { RegisterDto } from '../models/user.model';

@Component({
  selector: 'app-register',
  templateUrl: './register.page.html',
  styleUrls: ['./register.page.scss'],
  standalone: false,
})
export class RegisterPage {
  model: RegisterDto = {
    userName: '', password: '', firstName: '', lastName: '', email: '', phoneNumber: '', address: '', floor: '', apartment: ''
  };
  loading = false;

  constructor(
    private authService: AuthService,
    private router: Router,
    private toastController: ToastController,
    private ts: TranslateService
  ) {}

  // Telefon i adresa su obavezni (dostava); sprat i stan nisu
  get canSubmit(): boolean {
    const m = this.model;
    return !this.loading && !!m.firstName.trim() && !!m.lastName.trim() && m.userName.trim().length >= 3
      && m.password.length >= 8 && !!m.phoneNumber.trim() && !!m.address.trim();
  }

  async register() {
    if (!this.canSubmit) return;
    this.loading = true;
    const dto = { ...this.model, email: this.model.email?.trim() || undefined };
    this.authService.register(dto).subscribe({
      next: async res => {
        this.loading = false;
        this.router.navigateByUrl('/home');
        // Dobrodošlica: bez nje nije bilo jasno da je kupac prijavljen
        const toast = await this.toastController.create({
          message: this.ts.t('register.welcome').replace('{0}', res.user.firstName), duration: 3000, color: 'success', position: 'top'
        });
        await toast.present();
      },
      error: async (err) => {
        this.loading = false;
        const toast = await this.toastController.create({
          message: err.error?.message || this.ts.t('register.error'),
          duration: 3000,
          color: 'danger'
        });
        await toast.present();
      }
    });
  }
}

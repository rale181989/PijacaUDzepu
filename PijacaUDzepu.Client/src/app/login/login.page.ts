import { Component } from '@angular/core';
import { Router } from '@angular/router';
import { ToastController } from '@ionic/angular/lazy';
import { AuthService } from '../services/auth.service';
import { LoginDto } from '../models/user.model';

@Component({
  selector: 'app-login',
  templateUrl: './login.page.html',
  styleUrls: ['./login.page.scss'],
  standalone: false,
})
export class LoginPage {
  model: LoginDto = { userName: '', password: '' };
  loading = false;

  constructor(
    private authService: AuthService,
    private router: Router,
    private toastController: ToastController
  ) {}

  async login() {
    this.loading = true;
    this.authService.login(this.model).subscribe({
      next: (res) => {
        this.loading = false;
        if (res.user.roles.includes('VendorAdmin')) {
          this.router.navigateByUrl('/vendor-products');
        } else if (res.user.roles.includes('SuperAdmin')) {
          this.router.navigateByUrl('/admin-vendors');
        } else {
          this.router.navigateByUrl('/home');
        }
      },
      error: async (err) => {
        this.loading = false;
        const toast = await this.toastController.create({
          message: err.error?.message || 'Greška pri prijavi',
          duration: 3000,
          color: 'danger'
        });
        await toast.present();
      }
    });
  }
}

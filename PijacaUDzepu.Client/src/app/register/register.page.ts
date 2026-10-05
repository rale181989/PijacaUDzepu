import { Component } from '@angular/core';
import { Router } from '@angular/router';
import { ToastController } from '@ionic/angular/lazy';
import { AuthService } from '../services/auth.service';
import { RegisterDto } from '../models/user.model';

@Component({
  selector: 'app-register',
  templateUrl: './register.page.html',
  styleUrls: ['./register.page.scss'],
  standalone: false,
})
export class RegisterPage {
  model: RegisterDto = {
    userName: '', password: '', firstName: '', lastName: '', phoneNumber: ''
  };
  loading = false;

  constructor(
    private authService: AuthService,
    private router: Router,
    private toastController: ToastController
  ) {}

  async register() {
    this.loading = true;
    this.authService.register(this.model).subscribe({
      next: () => {
        this.loading = false;
        this.router.navigateByUrl('/home');
      },
      error: async (err) => {
        this.loading = false;
        const toast = await this.toastController.create({
          message: err.error?.message || 'Greška pri registraciji',
          duration: 3000,
          color: 'danger'
        });
        await toast.present();
      }
    });
  }
}

import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { ToastController } from '@ionic/angular/lazy';
import { AuthService } from '../services/auth.service';

@Component({
  selector: 'app-setup-account',
  templateUrl: './setup-account.page.html',
  styleUrls: ['./setup-account.page.scss'],
  standalone: false,
})
export class SetupAccountPage implements OnInit {
  token = '';
  userName = '';
  password = '';
  confirmPassword = '';
  firstName = '';
  lastName = '';
  saving = false;
  tokenValid = true;

  get passwordsMatch(): boolean {
    return this.password === this.confirmPassword;
  }

  get canSubmit(): boolean {
    return !!(this.userName && this.password && this.confirmPassword
      && this.firstName && this.lastName && this.passwordsMatch
      && this.password.length >= 4 && this.userName.length >= 3);
  }

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private authService: AuthService,
    private toastController: ToastController
  ) {}

  ngOnInit() {
    this.token = this.route.snapshot.queryParamMap.get('token') || '';
    if (!this.token) {
      this.tokenValid = false;
    }
  }

  async submit() {
    if (!this.canSubmit) return;
    this.saving = true;

    this.authService.acceptInvitation({
      token: this.token,
      userName: this.userName,
      password: this.password,
      firstName: this.firstName,
      lastName: this.lastName
    }).subscribe({
      next: async () => {
        this.saving = false;
        const toast = await this.toastController.create({
          message: 'Nalog kreiran! Dobrodošli!', duration: 3000, color: 'success'
        });
        await toast.present();
        this.router.navigateByUrl('/vendor-orders');
      },
      error: async (err) => {
        this.saving = false;
        const toast = await this.toastController.create({
          message: err.error?.message || 'Greška pri kreiranju naloga', duration: 3000, color: 'danger'
        });
        await toast.present();
      }
    });
  }
}

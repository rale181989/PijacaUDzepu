import { Component } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { ToastController } from '@ionic/angular/lazy';
import { AuthService } from '../services/auth.service';
import { TranslateService } from '../services/translate.service';

// Otvara se iz e-maila: /reset-password?token=...
@Component({
  selector: 'app-reset-password',
  templateUrl: './reset-password.page.html',
  styleUrls: ['./reset-password.page.scss'],
  standalone: false,
})
export class ResetPasswordPage {
  token = '';
  password = '';
  confirm = '';
  loading = false;
  error = '';

  constructor(
    route: ActivatedRoute,
    private authService: AuthService,
    private router: Router,
    private toastController: ToastController,
    private ts: TranslateService
  ) {
    this.token = route.snapshot.queryParamMap.get('token') ?? '';
  }

  get passwordsMatch(): boolean {
    return this.password === this.confirm;
  }

  get canSubmit(): boolean {
    return !!this.token && this.password.length >= 8 && this.passwordsMatch && !this.loading;
  }

  submit() {
    if (!this.canSubmit) return;
    this.loading = true;
    this.error = '';
    this.authService.resetPassword(this.token, this.password).subscribe({
      next: async () => {
        this.loading = false;
        const toast = await this.toastController.create({ message: this.ts.t('reset.done'), duration: 3000, color: 'success' });
        await toast.present();
        this.router.navigateByUrl('/login', { replaceUrl: true });
      },
      error: err => {
        this.loading = false;
        this.error = err.error?.message || err.error?.errors?.NewPassword?.[0] || 'Greška. Pokušajte ponovo.';
      }
    });
  }
}

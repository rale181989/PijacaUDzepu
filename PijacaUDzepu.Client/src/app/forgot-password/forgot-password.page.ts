import { Component, inject } from '@angular/core';
import { AuthService } from '../services/auth.service';
import { TranslateService } from '../services/translate.service';

// "Zaboravili ste lozinku?" - šalje link za novu lozinku na e-mail naloga
@Component({
  selector: 'app-forgot-password',
  templateUrl: './forgot-password.page.html',
  styleUrls: ['./forgot-password.page.scss'],
  standalone: false,
})
export class ForgotPasswordPage {
  private authService = inject(AuthService);
  private ts = inject(TranslateService);

  login = '';
  loading = false;
  sent = false;
  error = '';

  submit() {
    if (!this.login.trim()) return;
    this.loading = true;
    this.error = '';
    this.authService.forgotPassword(this.login.trim()).subscribe({
      next: () => { this.loading = false; this.sent = true; },
      error: err => {
        this.loading = false;
        this.error = err.error?.message || this.ts.t('error.generic');
      }
    });
  }
}

import { Component } from '@angular/core';
import { AuthService } from '../services/auth.service';

// "Zaboravili ste lozinku?" - šalje link za novu lozinku na e-mail naloga
@Component({
  selector: 'app-forgot-password',
  templateUrl: './forgot-password.page.html',
  styleUrls: ['./forgot-password.page.scss'],
  standalone: false,
})
export class ForgotPasswordPage {
  login = '';
  loading = false;
  sent = false;
  error = '';

  constructor(private authService: AuthService) {}

  submit() {
    if (!this.login.trim()) return;
    this.loading = true;
    this.error = '';
    this.authService.forgotPassword(this.login.trim()).subscribe({
      next: () => { this.loading = false; this.sent = true; },
      error: err => {
        this.loading = false;
        this.error = err.error?.message || (err.status === 429 ? 'Previše pokušaja, sačekajte minut.' : 'Greška. Pokušajte ponovo.');
      }
    });
  }
}

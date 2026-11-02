import { Component } from '@angular/core';
import { Router } from '@angular/router';
import { AlertController, ToastController } from '@ionic/angular/lazy';
import { AuthService } from '../services/auth.service';
import { TranslateService } from '../services/translate.service';

// Moj nalog (sve uloge): promena lozinke i brisanje naloga (obaveza za Google Play)
@Component({
  selector: 'app-account',
  templateUrl: './account.page.html',
  styleUrls: ['./account.page.scss'],
  standalone: false,
})
export class AccountPage {
  currentPassword = '';
  newPassword = '';
  confirmPassword = '';
  changing = false;

  constructor(
    public authService: AuthService,
    private router: Router,
    private alertController: AlertController,
    private toastController: ToastController,
    private ts: TranslateService
  ) {}

  get passwordsMatch(): boolean {
    return this.newPassword === this.confirmPassword;
  }

  get canChange(): boolean {
    return !!this.currentPassword && this.newPassword.length >= 8 && this.passwordsMatch && !this.changing;
  }

  changePassword() {
    if (!this.canChange) return;
    this.changing = true;
    this.authService.changePassword(this.currentPassword, this.newPassword).subscribe({
      next: async () => {
        this.changing = false;
        this.currentPassword = this.newPassword = this.confirmPassword = '';
        await this.toast(this.ts.t('account.changed'), 'success');
      },
      error: async err => {
        this.changing = false;
        await this.toast(err.error?.message || err.error?.errors?.NewPassword?.[0] || 'Greška', 'danger');
      }
    });
  }

  async deleteAccount() {
    const alert = await this.alertController.create({
      header: this.ts.t('account.delete'),
      message: this.ts.t('account.deleteConfirm'),
      inputs: [{ name: 'password', type: 'password', placeholder: this.ts.t('account.current') }],
      buttons: [
        { text: this.ts.t('account.cancel'), role: 'cancel' },
        {
          text: this.ts.t('account.deleteButton'),
          role: 'destructive',
          handler: data => {
            if (!data?.password) return false;
            this.authService.deleteAccount(data.password).subscribe({
              next: async () => {
                this.authService.logout();
                await this.toast(this.ts.t('account.deleted'), 'medium');
                this.router.navigateByUrl('/home', { replaceUrl: true });
              },
              error: async err => this.toast(err.error?.message || 'Greška', 'danger')
            });
            return true;
          }
        }
      ]
    });
    await alert.present();
  }

  private async toast(message: string, color: string) {
    const t = await this.toastController.create({ message, duration: 3500, color });
    await t.present();
  }
}

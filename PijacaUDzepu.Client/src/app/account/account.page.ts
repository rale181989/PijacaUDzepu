import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { AlertController, ToastController } from '@ionic/angular/lazy';
import { AuthService } from '../services/auth.service';
import { TranslateService } from '../services/translate.service';
import { UpdateProfileDto } from '../models/user.model';

// Moj nalog (sve uloge): telefon i adresa za dostavu (kupac), promena lozinke i brisanje naloga (obaveza za Google Play)
@Component({
  selector: 'app-account',
  templateUrl: './account.page.html',
  styleUrls: ['./account.page.scss'],
  standalone: false,
})
export class AccountPage implements OnInit {
  profile: UpdateProfileDto = { phoneNumber: '', address: '', floor: '', apartment: '' };
  saving = false;

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

  ngOnInit() {
    this.fillProfile();
    // Sačuvani korisnik je možda od pre nego što je adresa postojala
    this.authService.loadCurrentUser().subscribe({ next: () => this.fillProfile(), error: () => {} });
  }

  private fillProfile() {
    const u = this.authService.currentUser;
    this.profile = { phoneNumber: u?.phoneNumber ?? '', address: u?.address ?? '', floor: u?.floor ?? '', apartment: u?.apartment ?? '' };
  }

  get canSaveProfile(): boolean {
    return !this.saving && !!this.profile.phoneNumber.trim();
  }

  saveProfile() {
    if (!this.canSaveProfile) return;
    this.saving = true;
    this.authService.updateProfile(this.profile).subscribe({
      next: async () => {
        this.saving = false;
        this.fillProfile();
        await this.toast(this.ts.t('account.saved'), 'success');
      },
      error: async err => {
        this.saving = false;
        await this.toast(err.error?.message || this.ts.t('error.generic'), 'danger');
      }
    });
  }

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
        await this.toast(err.error?.message || err.error?.errors?.NewPassword?.[0] || this.ts.t('error.generic'), 'danger');
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
              error: async err => this.toast(err.error?.message || this.ts.t('error.generic'), 'danger')
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

import { Component, OnInit } from '@angular/core';
import { ToastController } from '@ionic/angular/lazy';
import { Capacitor } from '@capacitor/core';
import { SupportCategory, SupportService } from '../services/support.service';
import { AuthService } from '../services/auth.service';
import { TranslateService } from '../services/translate.service';
import { LEGAL_INFO } from '../legal/legal-content';

// Forma za tehnička pitanja i probleme; pitanje stiže SuperAdmin-u i na e-mail podrške
@Component({
  selector: 'app-support',
  templateUrl: './support.page.html',
  styleUrls: ['./support.page.scss'],
  standalone: false,
})
export class SupportPage implements OnInit {
  name = '';
  email = '';
  category: SupportCategory = 'Problem';
  message = '';
  // Skriveno polje za botove
  website = '';
  sending = false;
  sent = false;
  supportEmail = LEGAL_INFO.supportEmail;
  categories: SupportCategory[] = ['Problem', 'Question', 'Account', 'Other'];

  constructor(
    private supportService: SupportService,
    private authService: AuthService,
    private toastController: ToastController,
    private ts: TranslateService
  ) {}

  ngOnInit() {
    const user = this.authService.currentUser;
    if (user) {
      this.name = `${user.firstName} ${user.lastName}`.trim();
      this.email = user.email ?? '';
    }
  }

  get emailValid(): boolean {
    return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(this.email.trim());
  }

  get canSend(): boolean {
    return this.name.trim().length > 0 && this.emailValid && this.message.trim().length >= 10;
  }

  send() {
    if (!this.canSend) return;
    this.sending = true;
    this.supportService.send({
      name: this.name.trim(),
      email: this.email.trim(),
      category: this.category,
      message: this.message.trim(),
      platform: Capacitor.getPlatform(),
      userAgent: navigator.userAgent.slice(0, 300),
      website: this.website || undefined
    }).subscribe({
      next: () => {
        this.sending = false;
        this.sent = true;
        this.message = '';
      },
      error: async err => {
        this.sending = false;
        const message = err.status === 429 ? this.ts.t('support.tooMany') : this.ts.t('support.error');
        const toast = await this.toastController.create({ message, duration: 3500, color: 'danger' });
        await toast.present();
      }
    });
  }
}

import { Component } from '@angular/core';
import { AlertController, MenuController } from '@ionic/angular/lazy';
import { Capacitor } from '@capacitor/core';
import { App } from '@capacitor/app';
import { AuthService } from './services/auth.service';
import { CartService } from './services/cart.service';
import { NotificationService } from './services/notification.service';
import { PushService } from './services/push.service';
import { GuestOrdersService } from './services/guest-orders.service';
import { TranslateService, Lang } from './services/translate.service';
import { Router } from '@angular/router';

@Component({
  selector: 'app-root',
  templateUrl: 'app.component.html',
  styleUrls: ['app.component.scss'],
  standalone: false,
})
export class AppComponent {
  cartCount = 0;
  // Dugme "Zatvori aplikaciju" ima smisla samo u APK-u (pregledač se zatvara sam)
  readonly isNative = Capacitor.isNativePlatform();

  constructor(
    public authService: AuthService,
    private cartService: CartService,
    public notificationService: NotificationService,
    private pushService: PushService,
    public guestOrders: GuestOrdersService,
    public ts: TranslateService,
    private menuController: MenuController,
    private router: Router,
    private alertController: AlertController
  ) {
    this.cartService.cart$.subscribe(items => {
      this.cartCount = items.length; // broj stavki, ne zbir količina
    });
  }

  setLang(lang: Lang) {
    this.ts.setLang(lang);
  }

  closeMenu() {
    this.menuController.close();
  }

  // Zatvara Android aplikaciju; nalog ostaje prijavljen, a obaveštenja o porudžbinama stižu i dalje (FCM)
  async exitApp() {
    await this.closeMenu();
    const alert = await this.alertController.create({
      header: this.ts.t('nav.exitConfirm'),
      message: this.ts.t('nav.exitHint'),
      buttons: [
        { text: this.ts.t('nav.exitStay'), role: 'cancel' },
        { text: this.ts.t('nav.exit'), handler: () => { App.exitApp(); } }
      ]
    });
    await alert.present();
  }

  async logout() {
    await this.pushService.unregister();
    this.authService.logout();
    this.closeMenu();
    this.router.navigateByUrl('/home');
  }
}

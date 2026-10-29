import { Injectable, NgZone } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { Capacitor } from '@capacitor/core';
import { PushNotifications } from '@capacitor/push-notifications';
import { firstValueFrom, timeout } from 'rxjs';
import { AuthService } from './auth.service';
import { environment } from '../../environments/environment';

// Push obaveštenja (Firebase) stižu i kada je aplikacija zatvorena.
// Radi samo u Android/iOS aplikaciji; u browseru obaveštenja idu preko SignalR-a.
@Injectable({ providedIn: 'root' })
export class PushService {
  // Mora da se poklapa sa PushService.AndroidChannelId na serveru
  private static readonly ChannelId = 'orders';

  private baseUrl = environment.apiUrl + '/devices';
  private token: string | null = null;
  private listenersAdded = false;

  constructor(
    private http: HttpClient,
    private authService: AuthService,
    private router: Router,
    private ngZone: NgZone
  ) {
    if (!Capacitor.isNativePlatform() || !environment.pushEnabled) return;
    this.authService.currentUser$.subscribe(user => {
      if (user) this.register();
    });
  }

  private async register() {
    try {
      await this.addListeners();

      if (Capacitor.getPlatform() === 'android') {
        await PushNotifications.createChannel({
          id: PushService.ChannelId,
          name: 'Porudžbine',
          description: 'Nove porudžbine i promene statusa',
          importance: 5,
          sound: 'default',
          vibration: true,
          visibility: 1
        });
      }

      let perm = await PushNotifications.checkPermissions();
      if (perm.receive === 'prompt') perm = await PushNotifications.requestPermissions();
      if (perm.receive !== 'granted') return;

      await PushNotifications.register();
    } catch {
      // Bez google-services.json registracija ne uspeva - aplikacija radi i bez push-a
    }
  }

  private async addListeners() {
    if (this.listenersAdded) return;
    this.listenersAdded = true;

    await PushNotifications.addListener('registration', ({ value }) => {
      this.token = value;
      this.http.post(this.baseUrl, { token: value, platform: Capacitor.getPlatform() }).subscribe({ error: () => {} });
    });

    // Dodir na obaveštenje otvara odgovarajuću stranicu
    await PushNotifications.addListener('pushNotificationActionPerformed', action => {
      const url = action.notification.data?.url;
      if (url) this.ngZone.run(() => this.router.navigateByUrl(url));
    });
  }

  // Poziva se pre odjave, dok je JWT još važeći, da odjavljeni uređaj ne dobija tuđa obaveštenja
  async unregister() {
    if (!this.token) return;
    try {
      await firstValueFrom(this.http.post(`${this.baseUrl}/unregister`, { token: this.token }).pipe(timeout(3000)));
    } catch {}
  }
}

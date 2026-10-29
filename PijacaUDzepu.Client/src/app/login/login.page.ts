import { Component } from '@angular/core';
import { Router } from '@angular/router';
import { AlertController, ToastController } from '@ionic/angular/lazy';
import { AuthService } from '../services/auth.service';
import { TranslateService } from '../services/translate.service';
import { LoginDto } from '../models/user.model';
import { environment } from '../../environments/environment';
import { DEFAULT_API_SERVER, getApiServer, normalizeApiServer, setApiServer } from '../services/api-server';

@Component({
  selector: 'app-login',
  templateUrl: './login.page.html',
  styleUrls: ['./login.page.scss'],
  standalone: false,
})
export class LoginPage {
  model: LoginDto = { userName: '', password: '' };
  loading = false;
  // Samo u mobilnoj aplikaciji: izbor između Render-a i lokalnog servera
  serverSelectable = environment.serverSelectable;
  apiServer = getApiServer();
  isDefaultServer = this.apiServer === DEFAULT_API_SERVER;

  constructor(
    private authService: AuthService,
    private router: Router,
    private toastController: ToastController,
    private alertController: AlertController,
    private ts: TranslateService
  ) {}

  async chooseServer() {
    const alert = await this.alertController.create({
      header: 'Server',
      message: `Podrazumevano: ${DEFAULT_API_SERVER}. Za lokalni API upišite adresu računara u istoj Wi-Fi mreži, npr. http://192.168.1.10:5000`,
      inputs: [{ name: 'server', type: 'url', value: this.apiServer, placeholder: 'http://192.168.1.10:5000' }],
      buttons: [
        { text: 'Otkaži', role: 'cancel' },
        { text: 'Render', handler: () => setApiServer(null) },
        {
          text: 'Sačuvaj',
          handler: data => {
            const server = normalizeApiServer(data?.server ?? '');
            if (!server) {
              this.toastController.create({ message: 'Neispravna adresa servera', duration: 2500, color: 'danger' })
                .then(t => t.present());
              return false;
            }
            setApiServer(server);
            return true;
          }
        }
      ]
    });
    await alert.present();
  }

  async login() {
    this.loading = true;
    this.authService.login(this.model).subscribe({
      next: (res) => {
        this.loading = false;
        if (res.user.roles.includes('VendorAdmin')) {
          this.router.navigateByUrl('/vendor-products');
        } else if (res.user.roles.includes('Courier')) {
          this.router.navigateByUrl('/courier-orders');
        } else if (res.user.roles.includes('SuperAdmin')) {
          this.router.navigateByUrl('/admin-vendors');
        } else {
          this.router.navigateByUrl('/home');
        }
      },
      error: async (err) => {
        this.loading = false;
        const toast = await this.toastController.create({
          message: err.error?.message || this.ts.t('login.error'),
          duration: 3000,
          color: 'danger'
        });
        await toast.present();
      }
    });
  }
}

import { Component, OnInit } from '@angular/core';
import { UserService } from '../services/user.service';
import { User } from '../models/user.model';
import { AlertController, ToastController } from '@ionic/angular/lazy';

@Component({
  selector: 'app-admin-users',
  templateUrl: './admin-users.page.html',
  styleUrls: ['./admin-users.page.scss'],
  standalone: false,
})
export class AdminUsersPage implements OnInit {
  users: User[] = [];
  loading = false;
  selectedTab: 'Customer' | 'VendorAdmin' | 'Courier' | 'SuperAdmin' = 'Customer';

  get filteredUsers(): User[] {
    return this.users.filter(u => u.roles.includes(this.selectedTab));
  }

  constructor(
    private userService: UserService,
    private alertController: AlertController,
    private toastController: ToastController
  ) {}

  ngOnInit() { this.loadUsers(); }
  ionViewWillEnter() { this.loadUsers(); }

  loadUsers() {
    this.loading = true;
    this.userService.getAll().subscribe({
      next: u => { this.users = u; this.loading = false; },
      error: () => this.loading = false
    });
  }

  async toggleActive(user: User) {
    this.userService.toggleActive(user.id).subscribe({
      next: async () => {
        user.isActive = !user.isActive;
        const toast = await this.toastController.create({
          message: user.isActive ? 'Korisnik aktiviran' : 'Korisnik deaktiviran',
          duration: 2000, color: 'success'
        });
        await toast.present();
      }
    });
  }

  // Admin otvara nalog, a kurir posle sam dopunjuje podatke u "Moji podaci"
  async createCourier() {
    const alert = await this.alertController.create({
      header: 'Novi kurir',
      inputs: [
        { name: 'firstName', placeholder: 'Ime' },
        { name: 'lastName', placeholder: 'Prezime' },
        { name: 'phoneNumber', placeholder: 'Telefon', type: 'tel' },
        { name: 'userName', placeholder: 'Korisničko ime' },
        { name: 'password', placeholder: 'Lozinka (min. 4 znaka)', type: 'password' },
      ],
      buttons: [
        { text: 'Otkaži', role: 'cancel' },
        {
          text: 'Napravi nalog',
          handler: data => {
            if (!data.firstName?.trim() || !data.lastName?.trim() || !data.userName?.trim() || (data.password ?? '').length < 4) {
              this.showToast('Popunite ime, prezime, korisničko ime i lozinku', 'warning');
              return false;
            }
            this.userService.createCourier({
              userName: data.userName.trim(),
              password: data.password,
              firstName: data.firstName.trim(),
              lastName: data.lastName.trim(),
              phoneNumber: data.phoneNumber?.trim() || undefined
            }).subscribe({
              next: () => {
                this.showToast(`Kurir ${data.userName.trim()} je napravljen`, 'success');
                this.loadUsers();
              },
              error: err => this.showToast(err.error?.message || 'Greška pri pravljenju naloga', 'danger')
            });
            return true;
          }
        }
      ]
    });
    await alert.present();
  }

  private async showToast(message: string, color: string) {
    const toast = await this.toastController.create({ message, duration: 2500, color });
    await toast.present();
  }

  doRefresh(event: any) {
    this.loadUsers();
    setTimeout(() => event.target.complete(), 500);
  }
}

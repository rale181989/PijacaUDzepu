import { Component, OnInit } from '@angular/core';
import { UserService } from '../services/user.service';
import { User } from '../models/user.model';
import { AlertController, ToastController } from '@ionic/angular/lazy';
import { Subscription } from 'rxjs';
import { PagerComponent } from '../shared/pager/pager.component';

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

  // Server vraća samo jednu stranu korisnika izabrane uloge (kupaca može biti hiljade)
  search = '';
  page = 0;
  totalCount = 0;
  readonly pageSizeKey = 'adminUsersPageSize';
  pageSize = PagerComponent.savedSize(this.pageSizeKey);
  private loadSub?: Subscription;

  constructor(
    private userService: UserService,
    private alertController: AlertController,
    private toastController: ToastController
  ) {}

  ngOnInit() { this.loadUsers(); }
  ionViewWillEnter() { this.loadUsers(); }

  loadUsers() {
    this.loadSub?.unsubscribe();
    this.loading = true;
    this.loadSub = this.userService.getPage(this.selectedTab, this.search, this.page * this.pageSize, this.pageSize).subscribe({
      next: r => {
        this.users = r.items;
        this.totalCount = r.totalCount;
        this.loading = false;
        if (this.users.length === 0 && this.page > 0 && this.totalCount > 0) {
          this.page = Math.ceil(this.totalCount / this.pageSize) - 1;
          this.loadUsers();
        }
      },
      error: () => this.loading = false
    });
  }

  selectTab(tab: 'Customer' | 'VendorAdmin' | 'Courier' | 'SuperAdmin') {
    if (tab === this.selectedTab) return;
    this.selectedTab = tab;
    this.page = 0;
    this.loadUsers();
  }

  onSearch() {
    this.page = 0;
    this.loadUsers();
  }

  goToPage(page: number) {
    this.page = page;
    this.loadUsers();
  }

  setPageSize(size: number) {
    this.page = PagerComponent.pageAfterResize(this.page, this.pageSize, size);
    this.pageSize = size;
    this.loadUsers();
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

  // Privremena lozinka za korisnika koji nema e-mail (kurir, prodavac); prikazuje se samo jednom
  async resetPassword(user: User) {
    const confirm = await this.alertController.create({
      header: 'Resetuj lozinku',
      message: `Korisnik ${user.userName} dobija novu privremenu lozinku, a stara prestaje da važi. Nalog se otključava ako je bio zaključan.`,
      buttons: [
        { text: 'Otkaži', role: 'cancel' },
        {
          text: 'Resetuj',
          handler: () => {
            this.userService.resetPassword(user.id).subscribe({
              next: async r => {
                const shown = await this.alertController.create({
                  header: 'Nova lozinka',
                  message: `Lozinka za ${user.userName}: ${r.password}  -  zapišite je i prosledite korisniku; posle zatvaranja se više ne vidi. Korisnik je može promeniti u „Moj nalog”.`,
                  buttons: ['Zapisao sam']
                });
                await shown.present();
              },
              error: err => this.showToast(err.error?.message || 'Greška pri resetovanju lozinke', 'danger')
            });
          }
        }
      ]
    });
    await confirm.present();
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
        { name: 'password', placeholder: 'Lozinka (min. 8 znakova)', type: 'password' },
      ],
      buttons: [
        { text: 'Otkaži', role: 'cancel' },
        {
          text: 'Napravi nalog',
          handler: data => {
            if (!data.firstName?.trim() || !data.lastName?.trim() || !data.userName?.trim() || (data.password ?? '').length < 8) {
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

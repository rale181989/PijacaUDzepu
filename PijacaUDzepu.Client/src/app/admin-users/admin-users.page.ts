import { Component, OnInit } from '@angular/core';
import { UserService } from '../services/user.service';
import { User } from '../models/user.model';
import { ToastController } from '@ionic/angular/lazy';

@Component({
  selector: 'app-admin-users',
  templateUrl: './admin-users.page.html',
  styleUrls: ['./admin-users.page.scss'],
  standalone: false,
})
export class AdminUsersPage implements OnInit {
  users: User[] = [];
  loading = false;
  selectedTab: 'Customer' | 'VendorAdmin' | 'SuperAdmin' = 'Customer';

  get filteredUsers(): User[] {
    return this.users.filter(u => u.roles.includes(this.selectedTab));
  }

  constructor(
    private userService: UserService,
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

  doRefresh(event: any) {
    this.loadUsers();
    setTimeout(() => event.target.complete(), 500);
  }
}

import { Component } from '@angular/core';
import { MenuController } from '@ionic/angular/lazy';
import { AuthService } from './services/auth.service';
import { CartService } from './services/cart.service';
import { NotificationService } from './services/notification.service';
import { Router } from '@angular/router';

@Component({
  selector: 'app-root',
  templateUrl: 'app.component.html',
  styleUrls: ['app.component.scss'],
  standalone: false,
})
export class AppComponent {
  cartCount = 0;

  constructor(
    public authService: AuthService,
    private cartService: CartService,
    public notificationService: NotificationService,
    private menuController: MenuController,
    private router: Router
  ) {
    this.cartService.cart$.subscribe(items => {
      this.cartCount = items.reduce((sum, i) => sum + i.quantity, 0);
    });
  }

  closeMenu() {
    this.menuController.close();
  }

  logout() {
    this.authService.logout();
    this.closeMenu();
    this.router.navigateByUrl('/home');
  }
}

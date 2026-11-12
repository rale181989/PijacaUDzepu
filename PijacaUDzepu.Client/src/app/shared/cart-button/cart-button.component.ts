import { Component, OnDestroy } from '@angular/core';
import { Subscription } from 'rxjs';
import { CartService } from '../../services/cart.service';
import { AuthService } from '../../services/auth.service';

// Korpa u gornjem desnom uglu na telefonu; na računaru je korpa u gornjem meniju.
// Prodavac i kurir ne kupuju, pa je ne vide ni na zajedničkim stranicama (podrška, pravne strane).
@Component({
  selector: 'app-cart-button',
  templateUrl: './cart-button.component.html',
  styleUrls: ['./cart-button.component.scss'],
  standalone: false,
})
export class CartButtonComponent implements OnDestroy {
  count = 0;
  private sub: Subscription;

  constructor(cartService: CartService, public authService: AuthService) {
    this.sub = cartService.cart$.subscribe(items => {
      this.count = items.length; // broj stavki (1,5 kg + 2 veze = 2), ne zbir količina
    });
  }

  ngOnDestroy() {
    this.sub.unsubscribe();
  }
}

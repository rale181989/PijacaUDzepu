import { Component, OnDestroy } from '@angular/core';
import { Subscription } from 'rxjs';
import { CartService } from '../../services/cart.service';

// Korpa u gornjem desnom uglu na telefonu; na računaru je korpa u gornjem meniju
@Component({
  selector: 'app-cart-button',
  templateUrl: './cart-button.component.html',
  styleUrls: ['./cart-button.component.scss'],
  standalone: false,
})
export class CartButtonComponent implements OnDestroy {
  count = 0;
  private sub: Subscription;

  constructor(cartService: CartService) {
    this.sub = cartService.cart$.subscribe(items => {
      this.count = items.reduce((sum, i) => sum + i.quantity, 0);
    });
  }

  ngOnDestroy() {
    this.sub.unsubscribe();
  }
}

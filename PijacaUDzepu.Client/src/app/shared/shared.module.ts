import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { IonicModule } from '@ionic/angular/lazy';
import { TranslatePipe } from '../pipes/translate.pipe';
import { AppFooterComponent } from './app-footer/app-footer.component';
import { CartButtonComponent } from './cart-button/cart-button.component';
import { PagerComponent } from './pager/pager.component';
import { RatingFormComponent } from './rating-form/rating-form.component';
import { StarsPipe } from '../pipes/stars.pipe';
import { OrderProgressComponent } from './order-progress/order-progress.component';
import { OrdersButtonComponent } from './orders-button/orders-button.component';
import { SkeletonComponent } from './skeleton/skeleton.component';

@NgModule({
  imports: [CommonModule, RouterModule, IonicModule, FormsModule],
  declarations: [TranslatePipe, StarsPipe, AppFooterComponent, CartButtonComponent, PagerComponent, RatingFormComponent, OrderProgressComponent, OrdersButtonComponent, SkeletonComponent],
  exports: [TranslatePipe, StarsPipe, AppFooterComponent, CartButtonComponent, PagerComponent, RatingFormComponent, OrderProgressComponent, OrdersButtonComponent, SkeletonComponent]
})
export class SharedModule {}

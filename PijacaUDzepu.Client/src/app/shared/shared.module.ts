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

@NgModule({
  imports: [CommonModule, RouterModule, IonicModule, FormsModule],
  declarations: [TranslatePipe, StarsPipe, AppFooterComponent, CartButtonComponent, PagerComponent, RatingFormComponent],
  exports: [TranslatePipe, StarsPipe, AppFooterComponent, CartButtonComponent, PagerComponent, RatingFormComponent]
})
export class SharedModule {}

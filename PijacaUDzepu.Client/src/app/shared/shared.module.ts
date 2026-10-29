import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { IonicModule } from '@ionic/angular/lazy';
import { TranslatePipe } from '../pipes/translate.pipe';
import { AppFooterComponent } from './app-footer/app-footer.component';
import { CartButtonComponent } from './cart-button/cart-button.component';

@NgModule({
  imports: [CommonModule, RouterModule, IonicModule],
  declarations: [TranslatePipe, AppFooterComponent, CartButtonComponent],
  exports: [TranslatePipe, AppFooterComponent, CartButtonComponent]
})
export class SharedModule {}

import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { IonicModule } from '@ionic/angular/lazy';
import { RouterModule } from '@angular/router';
import { VendorProductsPage } from './vendor-products.page';

@NgModule({
  imports: [
    CommonModule, IonicModule,
    RouterModule.forChild([{ path: '', component: VendorProductsPage }])
  ],
  declarations: [VendorProductsPage]
})
export class VendorProductsPageModule {}

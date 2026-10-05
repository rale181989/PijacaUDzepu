import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { IonicModule } from '@ionic/angular/lazy';
import { RouterModule } from '@angular/router';
import { VendorOrdersPage } from './vendor-orders.page';

@NgModule({
  imports: [
    CommonModule, IonicModule,
    RouterModule.forChild([{ path: '', component: VendorOrdersPage }])
  ],
  declarations: [VendorOrdersPage]
})
export class VendorOrdersPageModule {}

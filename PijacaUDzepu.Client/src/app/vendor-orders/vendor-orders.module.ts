import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { IonicModule } from '@ionic/angular/lazy';
import { SharedModule } from '../shared/shared.module';
import { RouterModule } from '@angular/router';
import { VendorOrdersPage } from './vendor-orders.page';

@NgModule({
  imports: [
    CommonModule, IonicModule, SharedModule,
    RouterModule.forChild([{ path: '', component: VendorOrdersPage }])
  ],
  declarations: [VendorOrdersPage]
})
export class VendorOrdersPageModule {}

import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { IonicModule } from '@ionic/angular/lazy';
import { RouterModule } from '@angular/router';
import { SharedModule } from '../shared/shared.module';
import { VendorTodayPage } from './vendor-today.page';

@NgModule({
  imports: [
    CommonModule, IonicModule, SharedModule,
    RouterModule.forChild([{ path: '', component: VendorTodayPage }])
  ],
  declarations: [VendorTodayPage]
})
export class VendorTodayPageModule {}

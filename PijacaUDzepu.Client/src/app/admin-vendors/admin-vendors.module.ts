import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { IonicModule } from '@ionic/angular/lazy';
import { RouterModule } from '@angular/router';
import { AdminVendorsPage } from './admin-vendors.page';

@NgModule({
  imports: [
    CommonModule, IonicModule,
    RouterModule.forChild([{ path: '', component: AdminVendorsPage }])
  ],
  declarations: [AdminVendorsPage]
})
export class AdminVendorsPageModule {}

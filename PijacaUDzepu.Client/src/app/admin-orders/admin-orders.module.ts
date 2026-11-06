import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { IonicModule } from '@ionic/angular/lazy';
import { SharedModule } from '../shared/shared.module';
import { RouterModule } from '@angular/router';
import { AdminOrdersPage } from './admin-orders.page';

@NgModule({
  imports: [
    CommonModule, IonicModule, SharedModule,
    RouterModule.forChild([{ path: '', component: AdminOrdersPage }])
  ],
  declarations: [AdminOrdersPage]
})
export class AdminOrdersPageModule {}

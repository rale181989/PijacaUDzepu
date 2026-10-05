import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { IonicModule } from '@ionic/angular/lazy';
import { RouterModule } from '@angular/router';
import { OrderDetailPage } from './order-detail.page';

@NgModule({
  imports: [
    CommonModule, IonicModule,
    RouterModule.forChild([{ path: '', component: OrderDetailPage }])
  ],
  declarations: [OrderDetailPage]
})
export class OrderDetailPageModule {}

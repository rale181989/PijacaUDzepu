import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { IonicModule } from '@ionic/angular/lazy';
import { RouterModule } from '@angular/router';
import { OrderDetailPage } from './order-detail.page';
import { SharedModule } from '../shared/shared.module';

@NgModule({
  imports: [
    CommonModule, IonicModule,
    RouterModule.forChild([{ path: '', component: OrderDetailPage }]),
    SharedModule
  ],
  declarations: [OrderDetailPage]
})
export class OrderDetailPageModule {}

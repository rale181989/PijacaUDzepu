import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { IonicModule } from '@ionic/angular/lazy';
import { RouterModule } from '@angular/router';
import { MyOrdersPage } from './my-orders.page';
import { SharedModule } from '../shared/shared.module';

@NgModule({
  imports: [
    CommonModule, IonicModule,
    RouterModule.forChild([{ path: '', component: MyOrdersPage }]),
    SharedModule
  ],
  declarations: [MyOrdersPage]
})
export class MyOrdersPageModule {}

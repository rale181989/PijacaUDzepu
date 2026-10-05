import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { IonicModule } from '@ionic/angular/lazy';
import { RouterModule } from '@angular/router';
import { MyOrdersPage } from './my-orders.page';

@NgModule({
  imports: [
    CommonModule, IonicModule,
    RouterModule.forChild([{ path: '', component: MyOrdersPage }])
  ],
  declarations: [MyOrdersPage]
})
export class MyOrdersPageModule {}

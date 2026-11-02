import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { IonicModule } from '@ionic/angular/lazy';
import { SharedModule } from '../shared/shared.module';
import { RouterModule } from '@angular/router';
import { CourierOrdersPage } from './courier-orders.page';

@NgModule({
  imports: [
    CommonModule, IonicModule, SharedModule,
    RouterModule.forChild([{ path: '', component: CourierOrdersPage }])
  ],
  declarations: [CourierOrdersPage]
})
export class CourierOrdersPageModule {}

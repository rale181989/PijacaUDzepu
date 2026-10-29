import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { IonicModule } from '@ionic/angular/lazy';
import { RouterModule } from '@angular/router';
import { TrackOrderPage } from './track-order.page';
import { SharedModule } from '../shared/shared.module';

@NgModule({
  imports: [
    CommonModule, IonicModule,
    RouterModule.forChild([{ path: '', component: TrackOrderPage }]),
    SharedModule
  ],
  declarations: [TrackOrderPage]
})
export class TrackOrderPageModule {}

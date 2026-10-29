import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { IonicModule } from '@ionic/angular/lazy';
import { RouterModule } from '@angular/router';
import { AdminSupportPage } from './admin-support.page';

@NgModule({
  imports: [
    CommonModule, IonicModule,
    RouterModule.forChild([{ path: '', component: AdminSupportPage }])
  ],
  declarations: [AdminSupportPage]
})
export class AdminSupportPageModule {}

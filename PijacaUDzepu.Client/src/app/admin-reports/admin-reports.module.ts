import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { IonicModule } from '@ionic/angular/lazy';
import { SharedModule } from '../shared/shared.module';
import { RouterModule } from '@angular/router';
import { AdminReportsPage } from './admin-reports.page';

@NgModule({
  imports: [
    CommonModule, FormsModule, IonicModule, SharedModule,
    RouterModule.forChild([{ path: '', component: AdminReportsPage }])
  ],
  declarations: [AdminReportsPage]
})
export class AdminReportsPageModule {}

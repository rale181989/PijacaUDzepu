import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { IonicModule } from '@ionic/angular/lazy';
import { RouterModule } from '@angular/router';
import { SupportPage } from './support.page';
import { SharedModule } from '../shared/shared.module';

@NgModule({
  imports: [
    CommonModule, FormsModule, IonicModule,
    RouterModule.forChild([{ path: '', component: SupportPage }]),
    SharedModule
  ],
  declarations: [SupportPage]
})
export class SupportPageModule {}

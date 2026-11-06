import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { IonicModule } from '@ionic/angular/lazy';
import { RouterModule } from '@angular/router';
import { SharedModule } from '../shared/shared.module';
import { VendorProductFormPage } from './vendor-product-form.page';

@NgModule({
  imports: [
    SharedModule,
    CommonModule, FormsModule, IonicModule,
    RouterModule.forChild([{ path: '', component: VendorProductFormPage }])
  ],
  declarations: [VendorProductFormPage]
})
export class VendorProductFormPageModule {}

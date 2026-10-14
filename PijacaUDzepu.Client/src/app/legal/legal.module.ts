import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { IonicModule } from '@ionic/angular/lazy';
import { LegalPage } from './legal.page';

import { LegalPageRoutingModule } from './legal-routing.module';
import { SharedModule } from '../shared/shared.module';

@NgModule({
  imports: [
    CommonModule,
    IonicModule,
    LegalPageRoutingModule,
    SharedModule
  ],
  declarations: [LegalPage]
})
export class LegalPageModule {}

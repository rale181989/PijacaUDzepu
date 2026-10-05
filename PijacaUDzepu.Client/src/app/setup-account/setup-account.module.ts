import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

import { IonicModule } from '@ionic/angular/lazy';

import { SetupAccountPageRoutingModule } from './setup-account-routing.module';

import { SetupAccountPage } from './setup-account.page';

@NgModule({
  imports: [
    CommonModule,
    FormsModule,
    IonicModule,
    SetupAccountPageRoutingModule
  ],
  declarations: [SetupAccountPage]
})
export class SetupAccountPageModule {}

import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { IonicModule } from '@ionic/angular/lazy';
import { RouterModule } from '@angular/router';
import { AdminMarketsPage } from './admin-markets.page';

@NgModule({
  imports: [
    CommonModule, FormsModule, IonicModule,
    RouterModule.forChild([{ path: '', component: AdminMarketsPage }])
  ],
  declarations: [AdminMarketsPage]
})
export class AdminMarketsPageModule {}

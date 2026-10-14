import { NgModule } from '@angular/core';
import { RouterModule } from '@angular/router';
import { TranslatePipe } from '../pipes/translate.pipe';
import { AppFooterComponent } from './app-footer/app-footer.component';

@NgModule({
  imports: [RouterModule],
  declarations: [TranslatePipe, AppFooterComponent],
  exports: [TranslatePipe, AppFooterComponent]
})
export class SharedModule {}

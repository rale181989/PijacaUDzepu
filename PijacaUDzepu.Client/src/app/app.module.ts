import { LOCALE_ID, NgModule } from '@angular/core';
import { registerLocaleData } from '@angular/common';
import localeSrLatn from '@angular/common/locales/sr-Latn';
import { BrowserModule } from '@angular/platform-browser';
import { RouteReuseStrategy } from '@angular/router';
import { HttpClientModule, HTTP_INTERCEPTORS } from '@angular/common/http';

import { IonicModule, IonicRouteStrategy } from '@ionic/angular/lazy';

import { AppComponent } from './app.component';
import { AppRoutingModule } from './app-routing.module';
import { TokenInterceptor } from './interceptors/token.interceptor';
import { SharedModule } from './shared/shared.module';

// Brojevi i iznosi kao u Srbiji: 1.200 din, 1,5 kg (bez ovoga Angular piše 1,200 kao u SAD)
registerLocaleData(localeSrLatn);

@NgModule({
  declarations: [AppComponent],
  imports: [
    BrowserModule,
    HttpClientModule,
    IonicModule.forRoot(),
    AppRoutingModule,
    SharedModule
  ],
  providers: [
    { provide: LOCALE_ID, useValue: 'sr-Latn' },
    { provide: RouteReuseStrategy, useClass: IonicRouteStrategy },
    { provide: HTTP_INTERCEPTORS, useClass: TokenInterceptor, multi: true }
  ],
  bootstrap: [AppComponent],
})
export class AppModule {}

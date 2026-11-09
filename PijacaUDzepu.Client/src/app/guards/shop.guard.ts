import { Injectable } from '@angular/core';
import { CanActivate, Router, UrlTree } from '@angular/router';
import { AuthService } from '../services/auth.service';

// Kupčev deo (ponuda, korpa, porudžbine kupca, praćenje) nije za prodavce i kurire: oni ne kupuju
// (server to i odbija), pa ih šaljemo na njihovu početnu stranu da ne dođe do zabune
@Injectable({ providedIn: 'root' })
export class ShopGuard implements CanActivate {
  constructor(private authService: AuthService, private router: Router) {}

  canActivate(): boolean | UrlTree {
    return this.authService.isStaff ? this.router.parseUrl(this.authService.staffHome) : true;
  }
}

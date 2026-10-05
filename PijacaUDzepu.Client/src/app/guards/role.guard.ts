import { Injectable } from '@angular/core';
import { CanActivate, ActivatedRouteSnapshot, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

@Injectable({ providedIn: 'root' })
export class RoleGuard implements CanActivate {
  constructor(private authService: AuthService, private router: Router) {}

  canActivate(route: ActivatedRouteSnapshot): boolean {
    const expectedRoles = route.data['roles'] as string[];
    if (!expectedRoles || !this.authService.isLoggedIn) {
      this.router.navigateByUrl('/login');
      return false;
    }

    const hasRole = expectedRoles.some(role => this.authService.hasRole(role));
    if (!hasRole) {
      this.router.navigateByUrl('/home');
      return false;
    }

    return true;
  }
}

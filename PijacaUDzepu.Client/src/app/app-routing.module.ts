import { NgModule } from '@angular/core';
import { PreloadAllModules, RouterModule, Routes } from '@angular/router';
import { AuthGuard } from './guards/auth.guard';
import { RoleGuard } from './guards/role.guard';

const routes: Routes = [
  { path: '', redirectTo: 'home', pathMatch: 'full' },
  {
    path: 'home',
    loadChildren: () => import('./home/home.module').then(m => m.HomePageModule)
  },
  {
    path: 'login',
    loadChildren: () => import('./login/login.module').then(m => m.LoginPageModule)
  },
  {
    path: 'register',
    loadChildren: () => import('./register/register.module').then(m => m.RegisterPageModule)
  },
  {
    path: 'cart',
    loadChildren: () => import('./cart/cart.module').then(m => m.CartPageModule)
  },
  {
    path: 'my-orders',
    loadChildren: () => import('./my-orders/my-orders.module').then(m => m.MyOrdersPageModule),
    canActivate: [AuthGuard]
  },
  {
    path: 'order-detail/:id',
    loadChildren: () => import('./order-detail/order-detail.module').then(m => m.OrderDetailPageModule),
    canActivate: [AuthGuard]
  },
  {
    path: 'vendor-profile',
    loadChildren: () => import('./vendor-profile/vendor-profile.module').then(m => m.VendorProfilePageModule),
    canActivate: [RoleGuard],
    data: { roles: ['VendorAdmin'] }
  },
  {
    path: 'vendor-products',
    loadChildren: () => import('./vendor-products/vendor-products.module').then(m => m.VendorProductsPageModule),
    canActivate: [RoleGuard],
    data: { roles: ['VendorAdmin', 'SuperAdmin'] }
  },
  {
    path: 'vendor-product-form',
    loadChildren: () => import('./vendor-product-form/vendor-product-form.module').then(m => m.VendorProductFormPageModule),
    canActivate: [RoleGuard],
    data: { roles: ['VendorAdmin', 'SuperAdmin'] }
  },
  {
    path: 'vendor-product-form/:id',
    loadChildren: () => import('./vendor-product-form/vendor-product-form.module').then(m => m.VendorProductFormPageModule),
    canActivate: [RoleGuard],
    data: { roles: ['VendorAdmin', 'SuperAdmin'] }
  },
  {
    path: 'vendor-orders',
    loadChildren: () => import('./vendor-orders/vendor-orders.module').then(m => m.VendorOrdersPageModule),
    canActivate: [RoleGuard],
    data: { roles: ['VendorAdmin', 'SuperAdmin'] }
  },
  {
    path: 'admin-vendors',
    loadChildren: () => import('./admin-vendors/admin-vendors.module').then(m => m.AdminVendorsPageModule),
    canActivate: [RoleGuard],
    data: { roles: ['SuperAdmin'] }
  },
  {
    path: 'admin-vendor-form',
    loadChildren: () => import('./admin-vendor-form/admin-vendor-form.module').then(m => m.AdminVendorFormPageModule),
    canActivate: [RoleGuard],
    data: { roles: ['SuperAdmin'] }
  },
  {
    path: 'admin-vendor-form/:id',
    loadChildren: () => import('./admin-vendor-form/admin-vendor-form.module').then(m => m.AdminVendorFormPageModule),
    canActivate: [RoleGuard],
    data: { roles: ['SuperAdmin'] }
  },
  {
    path: 'admin-markets',
    loadChildren: () => import('./admin-markets/admin-markets.module').then(m => m.AdminMarketsPageModule),
    canActivate: [RoleGuard],
    data: { roles: ['SuperAdmin'] }
  },
  {
    path: 'admin-users',
    loadChildren: () => import('./admin-users/admin-users.module').then(m => m.AdminUsersPageModule),
    canActivate: [RoleGuard],
    data: { roles: ['SuperAdmin'] }
  },
  {
    path: 'setup-account',
    loadChildren: () => import('./setup-account/setup-account.module').then( m => m.SetupAccountPageModule)
  },
];

@NgModule({
  imports: [
    RouterModule.forRoot(routes, { preloadingStrategy: PreloadAllModules })
  ],
  exports: [RouterModule]
})
export class AppRoutingModule {}

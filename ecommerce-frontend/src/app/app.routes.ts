import { Routes } from '@angular/router';

import { adminGuard } from './core/guards/admin.guard';
import { authGuard } from './core/guards/auth.guard';
import { Home } from './home/home';

// Everything except the home page is loaded on demand to keep the first load small.
export const routes: Routes = [

  { path: '', redirectTo: 'home', pathMatch: 'full' },

  { path: 'home', component: Home },

  // Authentication
  { path: 'login', loadComponent: () => import('./auth/login/login').then(m => m.Login) },
  { path: 'register', loadComponent: () => import('./auth/register/register').then(m => m.Register) },
  { path: 'signup', redirectTo: 'register' },
  { path: 'forgot-password', loadComponent: () => import('./auth/forgot-password/forgot-password').then(m => m.ForgotPassword) },
  { path: 'verify-otp', loadComponent: () => import('./auth/verify-otp/verify-otp').then(m => m.VerifyOtp) },
  { path: 'reset-password', loadComponent: () => import('./auth/reset-password/reset-password').then(m => m.ResetPassword) },

  // Catalog
  { path: 'products', loadComponent: () => import('./products/product-list/product-list').then(m => m.ProductList) },
  { path: 'products/:id', loadComponent: () => import('./products/product-details/product-details').then(m => m.ProductDetails) },

  // Purchasing
  { path: 'cart', loadComponent: () => import('./cart/cart').then(m => m.CartComponent) },
  { path: 'checkout', loadComponent: () => import('./checkout/checkout').then(m => m.Checkout) },
  { path: 'orders', loadComponent: () => import('./orders/my-orders/my-orders').then(m => m.MyOrders) },
  { path: 'orders/:id', loadComponent: () => import('./orders/order-details/order-details').then(m => m.OrderDetails) },
  { path: 'order-confirmation/:id', loadComponent: () => import('./orders/order-confirmation/order-confirmation').then(m => m.OrderConfirmation) },

  // Information and account
  { path: 'about', loadComponent: () => import('./pages/about/about').then(m => m.About) },
  { path: 'contact', loadComponent: () => import('./pages/contact/contact').then(m => m.Contact) },
  { path: 'profile', loadComponent: () => import('./profile/profile').then(m => m.Profile), canActivate: [authGuard] },

  // Admin: one guarded shell with the sidebar; every admin page renders inside it
  {
    path: 'admin',
    loadComponent: () => import('./admin/shell/admin-shell').then(m => m.AdminShell),
    canActivate: [adminGuard],
    children: [
      { path: '', loadComponent: () => import('./admin/dashboard/dashboard').then(m => m.AdminDashboard) },
      { path: 'products', loadComponent: () => import('./admin/products/admin-products').then(m => m.AdminProducts) },
      { path: 'products/add', loadComponent: () => import('./admin/products/add/add-product').then(m => m.AddProduct) },
      { path: 'products/import', loadComponent: () => import('./admin/products/import/import-products').then(m => m.ImportProducts) },
      { path: 'products/bulk-update', loadComponent: () => import('./admin/products/bulk-update/bulk-update-products').then(m => m.BulkUpdateProducts) },
      { path: 'products/edit/:id', loadComponent: () => import('./admin/products/edit/edit-product').then(m => m.EditProduct) },
      { path: 'categories', loadComponent: () => import('./admin/categories/categories').then(m => m.Categories) },
      { path: 'categories/add', loadComponent: () => import('./admin/categories/add/add-category').then(m => m.AddCategory) },
      { path: 'categories/edit/:id', loadComponent: () => import('./admin/categories/edit/edit-category').then(m => m.EditCategory) },
      { path: 'inventory', loadComponent: () => import('./admin/inventory/inventory').then(m => m.InventoryComponent) },
      { path: 'orders', loadComponent: () => import('./admin/orders/orders').then(m => m.AdminOrders) },
      { path: 'orders/details/:id', loadComponent: () => import('./admin/orders/details/order-details').then(m => m.AdminOrderDetails) }
    ]
  }
];

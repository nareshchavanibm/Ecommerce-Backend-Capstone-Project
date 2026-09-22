import { Routes } from '@angular/router';

import { AdminProducts } from './admin/products/admin-products';
import { AdminOrderDetails } from './admin/orders/details/order-details';

import { Home } from './home/home';
import { Admin } from './admin/admin';
import { adminGuard } from './core/guards/admin.guard';
import { InventoryComponent } from './admin/inventory/inventory';

import { Login } from './auth/login/login';
import { Signup } from './auth/signup/signup';

import { Categories } from './admin/categories/categories';
import { AddCategory } from './admin/categories/add/add-category';
import { EditCategory } from './admin/categories/edit/edit-category';

import { AdminOrders } from './admin/orders/orders';

import { EditProduct } from './admin/products/edit/edit-product';

import { AddProduct } from './admin/products/add/add-product';
import { ImportProducts } from './admin/products/import/import-products';

import { ProductList } from './products/product-list/product-list';
import { ProductDetails } from './products/product-details/product-details';

import { CartComponent } from './cart/cart';

import { MyOrders } from './orders/my-orders/my-orders';
import { OrderDetails } from './orders/order-details/order-details';
import { OrderConfirmation } from './orders/order-confirmation/order-confirmation';

export const routes: Routes = [

  // Home is public
  {
    path: '',
    redirectTo: 'home',
    pathMatch: 'full'
  },

  // Authentication
  {
    path: 'login',
    component: Login
  },

  {
    path: 'signup',
    component: Signup
  },

  // Public pages
  {
    path: 'home',
    component: Home
  },

  {
    path: 'products',
    component: ProductList
  },

  {
    path: 'products/:id',
    component: ProductDetails
  },

  // User-specific pages
  {
    path: 'cart',
    component: CartComponent
  },

  {
    path: 'orders',
    component: MyOrders
  },

  {
    path: 'orders/:id',
    component: OrderDetails
  },

  {
    path: 'order-confirmation/:id',
    component: OrderConfirmation
  },
  //Admin
{
    path: 'admin',
    component: Admin,
    canActivate: [adminGuard]
  },
  {
    path: 'admin/products',
    component: AdminProducts,
    canActivate: [adminGuard]
  },

  {
    path: 'admin/products/add',
    component: AddProduct,
    canActivate: [adminGuard]
  },

  {
    path: 'admin/products/import',
    component: ImportProducts,
    canActivate: [adminGuard]
  },

  {
    path: 'admin/products/edit/:id',
    component: EditProduct,
    canActivate: [adminGuard]
  },
  {
    path: 'admin/categories',
    component: Categories,
    canActivate: [adminGuard]
  },
  {
    path: 'admin/categories/add',
    component: AddCategory,
    canActivate: [adminGuard]
  },
  {
    path: 'admin/categories/edit/:id',
    component: EditCategory,
    canActivate: [adminGuard]
  },
  {
    path: 'admin/inventory',
    component: InventoryComponent,
    canActivate: [adminGuard]
  },
  {
    path: 'admin/orders',
    component: AdminOrders,
    canActivate: [adminGuard]
  },
  {
    path: 'admin/orders/details/:id',
    component: AdminOrderDetails,
    canActivate: [adminGuard]
  }

];

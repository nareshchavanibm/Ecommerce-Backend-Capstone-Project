import { Injectable, inject, signal } from '@angular/core';
import { Router } from '@angular/router';

import { AuthService } from '../../auth/auth';
import { Product } from '../../shared/models/product.model';
import { ToastService } from '../../shared/ui/toast/toast.service';
import { CartService } from './cart';

// Shared "add to cart" behaviour for product cards.
@Injectable({
  providedIn: 'root'
})
export class CartActions {

  private readonly cartService = inject(CartService);

  private readonly authService = inject(AuthService);

  private readonly toast = inject(ToastService);

  private readonly router = inject(Router);

  readonly addingId = signal<number | null>(null);

  // Increments after every successful add so views can refresh the cart.
  readonly added = signal(0);

  add(product: Product): void {

    if (!this.authService.isLoggedIn()) {
      this.router.navigate(['/login']);
      return;
    }

    this.addingId.set(product.id);

    this.cartService.addToCart(product.id, 1).subscribe({
      next: () => {
        this.addingId.set(null);
        this.added.update(count => count + 1);
        this.toast.success(`${product.name} added to your cart.`);
      },
      error: (error) => {
        this.addingId.set(null);
        this.toast.error(error.error?.message ?? 'Unable to add this item to your cart.');
      }
    });
  }
}

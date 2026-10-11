import { CurrencyPipe } from '@angular/common';
import { Component, OnInit, computed, effect, inject, signal, untracked } from '@angular/core';
import { RouterLink } from '@angular/router';
import { catchError, finalize, forkJoin, of } from 'rxjs';

import { CartActions } from '../core/services/cart-actions';
import { CartService } from '../core/services/cart';
import { ProductService } from '../core/services/product';
import { Cart, CartItem } from '../shared/models/cart.model';
import { Product } from '../shared/models/product.model';
import { Alert } from '../shared/ui/alert/alert';
import { Button } from '../shared/ui/button/button';
import { Dialog } from '../shared/ui/dialog/dialog';
import { ProductCard } from '../shared/ui/product-card/product-card';
import { QuantityStepper } from '../shared/ui/quantity-stepper/quantity-stepper';
import { Skeleton } from '../shared/ui/skeleton/skeleton';
import { ToastService } from '../shared/ui/toast/toast.service';

const SUGGESTION_COUNT = 4;

@Component({
  selector: 'app-cart',
  imports: [RouterLink, CurrencyPipe, Alert, Button, Dialog, ProductCard, QuantityStepper, Skeleton],
  templateUrl: './cart.html',
  styleUrl: './cart.css'
})
export class CartComponent implements OnInit {

  private readonly cartService = inject(CartService);

  private readonly productService = inject(ProductService);

  private readonly toast = inject(ToastService);

  protected readonly cartActions = inject(CartActions);

  protected readonly cart = signal<Cart | null>(null);

  protected readonly loading = signal(true);

  protected readonly errorMessage = signal('');

  // Product ids whose quantity change or removal is in flight.
  protected readonly busy = signal<ReadonlySet<number>>(new Set());

  protected readonly images = signal<Record<number, string>>({});

  protected readonly suggestions = signal<Product[]>([]);

  protected readonly clearOpen = signal(false);

  protected readonly clearing = signal(false);

  protected readonly placeholders = [1, 2, 3];

  protected readonly items = computed(() => this.cart()?.items ?? []);

  protected readonly itemCount = computed(() => this.items().reduce((sum, item) => sum + item.quantity, 0));

  private readonly imagesRequested = new Set<number>();

  constructor() {

    effect(() => {
      if (this.cartActions.added() > 0) {
        untracked(() => this.reload());
      }
    });
  }

  ngOnInit(): void {
    this.load();
  }

  protected load(): void {

    this.loading.set(true);
    this.errorMessage.set('');

    this.cartService.getCart().subscribe({
      next: cart => {
        this.applyCart(cart);
        this.loading.set(false);
      },
      error: error => {
        this.errorMessage.set(error.error?.message ?? 'Unable to load your cart.');
        this.loading.set(false);
      }
    });
  }

  protected lineTotal(item: CartItem): number {
    return item.totalPrice ?? item.price * item.quantity;
  }

  protected updateQuantity(item: CartItem, quantity: number): void {

    if (quantity < 1 || this.busy().has(item.productId)) {
      return;
    }

    this.setBusy(item.productId, true);

    this.cartService.updateCartItem(item.productId, quantity).pipe(
      finalize(() => this.setBusy(item.productId, false))
    ).subscribe({
      next: cart => this.applyOrReload(cart),
      error: error => {
        this.toast.error(error.error?.message ?? 'Could not update the quantity.');
        this.reload();
      }
    });
  }

  protected removeItem(item: CartItem): void {

    if (this.busy().has(item.productId)) {
      return;
    }

    this.setBusy(item.productId, true);

    this.cartService.removeCartItem(item.productId).pipe(
      finalize(() => this.setBusy(item.productId, false))
    ).subscribe({
      next: cart => {
        this.applyOrReload(cart);
        this.toast.success(`${item.productName} removed from your cart.`);
      },
      error: error => {
        this.toast.error(error.error?.message ?? 'Could not remove this item.');
        this.reload();
      }
    });
  }

  protected clearCart(): void {

    if (this.clearing()) {
      return;
    }

    this.clearing.set(true);

    this.cartService.clearCart().pipe(
      finalize(() => this.clearing.set(false))
    ).subscribe({
      next: () => {
        this.clearOpen.set(false);
        this.applyCart({ ...(this.cart() as Cart), items: [], totalAmount: 0 });
      },
      error: error => {
        this.clearOpen.set(false);
        this.toast.error(error.error?.message ?? 'Could not clear your cart.');
      }
    });
  }

  private setBusy(productId: number, busy: boolean): void {

    this.busy.update(current => {
      const next = new Set(current);

      if (busy) {
        next.add(productId);
      } else {
        next.delete(productId);
      }

      return next;
    });
  }

  // A response without a cart body (e.g. after DELETE) is refreshed from the server.
  private applyOrReload(cart: Cart | null | undefined): void {

    if (cart && Array.isArray(cart.items)) {
      this.applyCart(cart);
    } else {
      this.reload();
    }
  }

  private reload(): void {
    this.cartService.getCart().subscribe({ next: cart => this.applyCart(cart), error: () => { } });
  }

  private applyCart(cart: Cart): void {

    this.cart.set(cart);

    if (cart.items.length === 0) {
      this.loadSuggestions();
    } else {
      this.loadImages(cart.items);
    }
  }

  // The cart API has no image field, so thumbnails come from the product API.
  private loadImages(items: CartItem[]): void {

    const missing = items.filter(item => !this.imagesRequested.has(item.productId));

    if (missing.length === 0) {
      return;
    }

    missing.forEach(item => this.imagesRequested.add(item.productId));

    forkJoin(
      missing.map(item => this.productService.getProductById(item.productId).pipe(catchError(() => of(null))))
    ).subscribe(products => {

      const found: Record<number, string> = {};

      products.forEach(product => {
        if (product?.imageUrl) {
          found[product.id] = product.imageUrl;
        }
      });

      this.images.update(current => ({ ...current, ...found }));
    });
  }

  private loadSuggestions(): void {

    if (this.suggestions().length > 0) {
      return;
    }

    this.productService.searchProducts({ pageSize: SUGGESTION_COUNT }).pipe(
      catchError(() => of(null))
    ).subscribe(result => this.suggestions.set(result?.items ?? []));
  }
}

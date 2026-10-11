import { CurrencyPipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable, toSignal } from '@angular/core/rxjs-interop';
import { Title } from '@angular/platform-browser';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { catchError, forkJoin, map, Observable, of, switchMap, tap } from 'rxjs';

import { AuthService } from '../../auth/auth';
import { CartActions } from '../../core/services/cart-actions';
import { CartService } from '../../core/services/cart';
import { InventoryService } from '../../core/services/inventory';
import { ProductService } from '../../core/services/product';
import { Inventory } from '../../shared/models/inventory.model';
import { Product } from '../../shared/models/product.model';
import { Alert } from '../../shared/ui/alert/alert';
import { Badge } from '../../shared/ui/badge/badge';
import { Breadcrumbs, Crumb } from '../../shared/ui/breadcrumbs/breadcrumbs';
import { Button } from '../../shared/ui/button/button';
import { Dialog } from '../../shared/ui/dialog/dialog';
import { ProductCard } from '../../shared/ui/product-card/product-card';
import { QuantityStepper } from '../../shared/ui/quantity-stepper/quantity-stepper';
import { Skeleton } from '../../shared/ui/skeleton/skeleton';
import { ToastService } from '../../shared/ui/toast/toast.service';
import { productImageUrl } from '../../shared/utils/product-image';

const RELATED_COUNT = 4;

interface ProductExtras {
  inventory: Inventory | null;
  related: Product[];
}

@Component({
  selector: 'app-product-details',
  imports: [
    RouterLink,
    CurrencyPipe,
    Alert,
    Badge,
    Breadcrumbs,
    Button,
    Dialog,
    ProductCard,
    QuantityStepper,
    Skeleton
  ],
  templateUrl: './product-details.html',
  styleUrl: './product-details.css'
})
export class ProductDetails {

  private readonly route = inject(ActivatedRoute);

  private readonly router = inject(Router);

  private readonly productService = inject(ProductService);

  private readonly inventoryService = inject(InventoryService);

  private readonly cartService = inject(CartService);

  protected readonly authService = inject(AuthService);

  private readonly toast = inject(ToastService);

  private readonly title = inject(Title);

  protected readonly cartActions = inject(CartActions);

  protected readonly product = signal<Product | null>(null);

  protected readonly inventory = signal<Inventory | null>(null);

  protected readonly related = signal<Product[]>([]);

  protected readonly loading = signal(true);

  protected readonly errorMessage = signal('');

  protected readonly quantity = signal(1);

  protected readonly adding = signal(false);

  protected readonly zoomOpen = signal(false);

  protected readonly imageFailed = signal(false);

  protected readonly image = computed(() => {

    const product = this.product();

    return product && !this.imageFailed() ? productImageUrl(product, 800) : '';
  });

  private readonly productId = toSignal(
    this.route.paramMap.pipe(map(params => Number(params.get('id')))),
    { initialValue: Number(this.route.snapshot.paramMap.get('id')) }
  );

  protected readonly total = computed(() => (this.product()?.price ?? 0) * this.quantity());

  // Stock is only known for signed-in users; never guessed otherwise.
  protected readonly maxQuantity = computed(() => {

    const stock = this.inventory()?.stockQuantity;

    return typeof stock === 'number' && stock > 0 ? stock : null;
  });

  protected readonly outOfStock = computed(() => {

    const stock = this.inventory()?.stockQuantity;

    return typeof stock === 'number' && stock <= 0;
  });

  protected readonly crumbs = computed<Crumb[]>(() => {

    const product = this.product();
    const items: Crumb[] = [
      { label: 'Home', link: ['/home'] },
      { label: 'Products', link: ['/products'] }
    ];

    if (product?.categoryName) {
      items.push({ label: product.categoryName, link: ['/products'], queryParams: { category: product.categoryName } });
    }

    items.push({ label: product?.name ?? '' });

    return items;
  });

  constructor() {

    toObservable(this.productId).pipe(
      tap(() => this.resetState()),
      switchMap(id => this.load(id)),
      takeUntilDestroyed()
    ).subscribe(extras => {

      if (extras) {
        this.applyExtras(extras);
      }
    });
  }

  protected addToCart(): void {

    const product = this.product();

    if (!product || this.adding() || this.outOfStock()) {
      return;
    }

    if (!this.authService.isLoggedIn()) {
      this.router.navigate(['/login']);
      return;
    }

    this.adding.set(true);

    this.cartService.addToCart(product.id, this.quantity()).subscribe({
      next: () => {
        this.adding.set(false);
        this.toast.success(`${product.name} added to your cart.`);
      },
      error: (error) => {
        this.adding.set(false);
        this.toast.error(error.error?.message ?? 'Unable to add this item to your cart.');
      }
    });
  }

  private resetState(): void {
    this.loading.set(true);
    this.errorMessage.set('');
    this.product.set(null);
    this.inventory.set(null);
    this.related.set([]);
    this.quantity.set(1);
    this.imageFailed.set(false);
    this.zoomOpen.set(false);
  }

  // Shows the product as soon as it loads; stock and related items follow.
  private load(id: number): Observable<ProductExtras | null> {

    return this.productService.getProductById(id).pipe(
      tap(product => {
        this.product.set(product);
        this.loading.set(false);
        this.title.setTitle(`${product.name} | Verdant`);
      }),
      switchMap(product => forkJoin({
        inventory: this.loadInventory(product),
        related: this.loadRelated(product)
      })),
      catchError(error => {
        this.errorMessage.set(error.status === 404 ? 'This product could not be found.' : 'Unable to load product details.');
        this.loading.set(false);
        return of(null);
      })
    );
  }

  private loadInventory(product: Product): Observable<Inventory | null> {

    return this.authService.isLoggedIn()
      ? this.inventoryService.getInventory(product.id).pipe(catchError(() => of(null)))
      : of(null);
  }

  private loadRelated(product: Product): Observable<Product[]> {

    return this.productService.searchProducts({
      category: product.categoryName || undefined,
      categoryId: product.categoryId,
      pageSize: RELATED_COUNT + 1
    }).pipe(
      map(result => result.items.filter(item => item.id !== product.id).slice(0, RELATED_COUNT)),
      catchError(() => of<Product[]>([]))
    );
  }

  private applyExtras(extras: ProductExtras): void {

    this.inventory.set(extras.inventory);
    this.related.set(extras.related);

    const stock = extras.inventory?.stockQuantity;

    if (typeof stock === 'number' && stock > 0 && this.quantity() > stock) {
      this.quantity.set(stock);
    }
  }
}

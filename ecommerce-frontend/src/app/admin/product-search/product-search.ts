import { CurrencyPipe } from '@angular/common';
import { Component, ElementRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { Observable, catchError, debounceTime, distinctUntilChanged, forkJoin, map, of, switchMap } from 'rxjs';

import { ProductService } from '../../core/services/product';
import { Product } from '../../shared/models/product.model';
import { Badge } from '../../shared/ui/badge/badge';
import { TextInput } from '../../shared/ui/text-input/text-input';
import { productImageUrl } from '../../shared/utils/product-image';

const SEARCH_LIMIT = 8;

// Admin-wide product finder: by id, name, description or specification.
@Component({
  selector: 'app-admin-product-search',
  imports: [CurrencyPipe, ReactiveFormsModule, RouterLink, Badge, TextInput],
  templateUrl: './product-search.html',
  styleUrl: './product-search.css'
})
export class AdminProductSearch {

  private readonly productService = inject(ProductService);

  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);

  protected readonly searchControl = new FormControl('', { nonNullable: true });

  // Null until something has been searched for.
  protected readonly results = signal<Product[] | null>(null);

  protected readonly searching = signal(false);

  protected readonly failed = signal(false);

  protected readonly open = signal(false);

  constructor() {

    inject(Router).events.pipe(takeUntilDestroyed()).subscribe(() => this.open.set(false));

    this.searchControl.valueChanges.pipe(
      map(value => value.trim()),
      distinctUntilChanged(),
      debounceTime(300),
      switchMap(term => {

        this.failed.set(false);

        if (!term) {
          this.searching.set(false);
          return of(null);
        }

        this.searching.set(true);
        this.open.set(true);

        return this.find(term);
      }),
      takeUntilDestroyed()
    ).subscribe(results => {
      this.results.set(results);
      this.searching.set(false);
    });
  }

  protected thumb(product: Product): string {
    return productImageUrl(product, 80);
  }

  protected hideImage(event: Event): void {
    (event.target as HTMLElement).style.display = 'none';
  }

  protected close(): void {
    this.open.set(false);
  }

  protected onFocusOut(event: FocusEvent): void {

    const next = event.relatedTarget as Node | null;

    if (!next || !this.host.nativeElement.contains(next)) {
      this.open.set(false);
    }
  }

  // A number is also tried as a product id; text matches name, description and specifications.
  private find(term: string): Observable<Product[] | null> {

    const byId$ = /^\d{1,9}$/.test(term)
      ? this.productService.lookupProduct(Number(term)).pipe(catchError(() => of(null)))
      : of(null);

    const byText$ = this.productService.searchProducts({ search: term, pageNumber: 1, pageSize: SEARCH_LIMIT }).pipe(
      map(result => result.items),
      catchError(() => {
        this.failed.set(true);
        return of<Product[]>([]);
      })
    );

    return forkJoin({ byId: byId$, byText: byText$ }).pipe(
      map(({ byId, byText }) => byId ? [byId, ...byText.filter(item => item.id !== byId.id)] : byText)
    );
  }
}

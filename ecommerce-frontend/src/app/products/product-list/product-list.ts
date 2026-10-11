import { isPlatformBrowser } from '@angular/common';
import { Component, ElementRef, PLATFORM_ID, computed, effect, inject, signal, viewChild } from '@angular/core';
import { takeUntilDestroyed, toObservable, toSignal } from '@angular/core/rxjs-interop';
import { Title } from '@angular/platform-browser';
import { ActivatedRoute, ParamMap, Router } from '@angular/router';
import { BehaviorSubject, catchError, combineLatest, map, of, switchMap, tap } from 'rxjs';

import { CartActions } from '../../core/services/cart-actions';
import { CategoryService } from '../../core/services/category';
import { ProductService } from '../../core/services/product';
import { Category } from '../../shared/models/category.model';
import { Product, ProductQuery } from '../../shared/models/product.model';
import { Alert } from '../../shared/ui/alert/alert';
import { Dialog } from '../../shared/ui/dialog/dialog';
import { Pagination } from '../../shared/ui/pagination/pagination';
import { ProductCard } from '../../shared/ui/product-card/product-card';
import { Skeleton } from '../../shared/ui/skeleton/skeleton';
import { ProductFilterValue, ProductFilters } from '../product-filters/product-filters';

const PAGE_SIZE = 12;

interface ListState {
  search: string;
  category: string;
  minPrice?: number;
  maxPrice?: number;
  page: number;
}

interface Chip {
  key: 'search' | 'category' | 'price';
  label: string;
}

@Component({
  selector: 'app-product-list',
  imports: [
    Alert,
    Dialog,
    Pagination,
    ProductCard,
    ProductFilters,
    Skeleton
  ],
  templateUrl: './product-list.html',
  styleUrl: './product-list.css'
})
export class ProductList {

  private readonly route = inject(ActivatedRoute);

  private readonly router = inject(Router);

  private readonly productService = inject(ProductService);

  private readonly categoryService = inject(CategoryService);

  private readonly title = inject(Title);

  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  protected readonly cartActions = inject(CartActions);

  private readonly heading = viewChild<ElementRef<HTMLElement>>('heading');

  protected readonly placeholders = Array.from({ length: 8 }, (_, i) => i);

  protected readonly products = signal<Product[]>([]);

  protected readonly totalCount = signal(0);

  protected readonly totalPages = signal(1);

  protected readonly loading = signal(true);

  protected readonly errorMessage = signal('');

  protected readonly categories = signal<Category[]>([]);

  protected readonly categoriesLoading = signal(true);

  protected readonly filtersOpen = signal(false);

  private readonly queryParams = toSignal(this.route.queryParamMap, { initialValue: this.route.snapshot.queryParamMap });

  protected readonly state = computed<ListState>(() => this.parse(this.queryParams()));

  private readonly reload = new BehaviorSubject(0);

  private focusAfterLoad = false;

  protected readonly pageHeading = computed(() => {

    const { category, search } = this.state();

    if (category) {
      return category;
    }

    return search ? `Results for \u201C${search}\u201D` : 'All products';
  });

  protected readonly chips = computed<Chip[]>(() => {

    const { search, category, minPrice, maxPrice } = this.state();
    const chips: Chip[] = [];

    if (search) {
      chips.push({ key: 'search', label: `Search: ${search}` });
    }

    if (category) {
      chips.push({ key: 'category', label: category });
    }

    if (minPrice !== undefined || maxPrice !== undefined) {
      const label = minPrice !== undefined && maxPrice !== undefined
        ? `\u20B9${minPrice} \u2013 \u20B9${maxPrice}`
        : minPrice !== undefined ? `From \u20B9${minPrice}` : `Up to \u20B9${maxPrice}`;

      chips.push({ key: 'price', label });
    }

    return chips;
  });

  // Search is a query, not a filter, so it is not counted on the Filters button.
  protected readonly filterCount = computed(() => this.chips().filter(chip => chip.key !== 'search').length);

  protected readonly resultText = computed(() => {

    const count = this.totalCount();

    return `${count} ${count === 1 ? 'product' : 'products'}`;
  });

  constructor() {

    this.categoryService.getCategories().pipe(
      catchError(() => of<Category[]>([])),
      takeUntilDestroyed()
    ).subscribe(categories => {
      this.categories.set(categories);
      this.categoriesLoading.set(false);
    });

    // switchMap cancels any in-flight request when the URL changes.
    combineLatest([toObservable(this.state), this.reload]).pipe(
      tap(() => {
        this.loading.set(true);
        this.errorMessage.set('');
      }),
      switchMap(([state]) =>
        this.productService.searchProducts(this.toQuery(state)).pipe(
          map(result => ({ result, error: null as string | null })),
          catchError(error => of({
            result: null,
            error: (error.error?.message as string | undefined) ?? 'We could not load products. Please try again.'
          }))
        )
      ),
      takeUntilDestroyed()
    ).subscribe(({ result, error }) => {

      if (result) {
        const items = result.items ?? [];
        const total = result.totalItems ?? result.totalCount ?? items.length;
        const pages = Math.max(result.totalPages ?? 1, 1);

        this.products.set(items);
        this.totalCount.set(total);
        this.totalPages.set(pages);

        // A stale ?page beyond the last page falls back to the last one.
        if (this.state().page > pages) {
          this.update({ page: pages }, true);
        }
      } else {
        this.products.set([]);
        this.errorMessage.set(error ?? '');
      }

      this.loading.set(false);

      if (this.focusAfterLoad) {
        this.focusAfterLoad = false;
        this.heading()?.nativeElement.focus();
      }
    });

    effect(() => {
      this.title.setTitle(`${this.pageHeading()} | Verdant`);
    });
  }

  protected onFiltersChange(value: ProductFilterValue): void {
    this.update({ category: value.category, minPrice: value.minPrice, maxPrice: value.maxPrice });
  }

  protected removeChip(key: Chip['key']): void {

    if (key === 'search') {
      this.update({ search: '' });
    } else if (key === 'category') {
      this.update({ category: '' });
    } else {
      this.update({ minPrice: undefined, maxPrice: undefined });
    }
  }

  protected clearAll(): void {
    this.update({ search: '', category: '', minPrice: undefined, maxPrice: undefined });
  }

  protected goToPage(page: number): void {

    this.focusAfterLoad = true;
    this.update({ page });

    if (this.isBrowser) {
      window.scrollTo({ top: 0, behavior: 'smooth' });
    }
  }

  protected retry(): void {
    this.reload.next(this.reload.value + 1);
  }

  private update(patch: Partial<ListState>, replaceUrl = false): void {

    const next: ListState = { ...this.state(), page: 1, ...patch };

    const queryParams: Record<string, string | number> = {};

    if (next.search) { queryParams['search'] = next.search; }
    if (next.category) { queryParams['category'] = next.category; }
    if (next.minPrice !== undefined) { queryParams['minPrice'] = next.minPrice; }
    if (next.maxPrice !== undefined) { queryParams['maxPrice'] = next.maxPrice; }

    if (next.page > 1) { queryParams['page'] = next.page; }

    this.router.navigate([], { relativeTo: this.route, queryParams, replaceUrl });
  }

  private parse(params: ParamMap): ListState {

    const number = (key: string): number | undefined => {
      const raw = params.get(key);
      const value = raw === null || raw === '' ? NaN : Number(raw);

      return Number.isFinite(value) && value >= 0 ? value : undefined;
    };

    const page = Math.floor(number('page') ?? 1);

    return {
      search: (params.get('search') ?? '').trim(),
      category: (params.get('category') ?? '').trim(),
      minPrice: number('minPrice'),
      maxPrice: number('maxPrice'),
      page: page < 1 ? 1 : page
    };
  }

  private toQuery(state: ListState): ProductQuery {

    const query: ProductQuery = { pageNumber: state.page, pageSize: PAGE_SIZE };

    if (state.search) { query.search = state.search; }
    if (state.category) { query.category = state.category; }
    if (state.minPrice !== undefined) { query.minPrice = state.minPrice; }
    if (state.maxPrice !== undefined) { query.maxPrice = state.maxPrice; }

    return query;
  }
}

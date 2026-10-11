import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { catchError, forkJoin, map, of } from 'rxjs';

import { CartActions } from '../core/services/cart-actions';
import { CategoryService } from '../core/services/category';
import { ProductService } from '../core/services/product';
import { Category } from '../shared/models/category.model';
import { Product } from '../shared/models/product.model';
import { ProductCard } from '../shared/ui/product-card/product-card';
import { Button } from '../shared/ui/button/button';
import { Skeleton } from '../shared/ui/skeleton/skeleton';

const FEATURED_COUNT = 8;
const VALUE_COUNT = 4;
// Divisible by the grid's 1, 2 and 4 columns so the last row is never ragged.
const CATEGORY_PREVIEW_COUNT = 8;

@Component({
  selector: 'app-home',
  imports: [RouterLink, Button, ProductCard, Skeleton],
  templateUrl: './home.html',
  styleUrl: './home.css'
})
export class Home implements OnInit {

  private readonly productService = inject(ProductService);

  private readonly categoryService = inject(CategoryService);

  protected readonly cartActions = inject(CartActions);

  protected readonly categories = signal<Category[]>([]);

  protected readonly categoriesExpanded = signal(false);

  protected readonly visibleCategories = computed(() =>
    this.categoriesExpanded() ? this.categories() : this.categories().slice(0, CATEGORY_PREVIEW_COUNT)
  );

  protected readonly hiddenCategoryCount = computed(() => Math.max(0, this.categories().length - CATEGORY_PREVIEW_COUNT));

  protected readonly featured = signal<Product[]>([]);

  protected readonly lowestPriced = signal<Product[]>([]);

  protected readonly loading = signal(true);

  protected readonly errorMessage = signal('');

  // Hero collage uses real product photos only.
  protected readonly heroImages = computed(() =>
    this.featured().filter(product => !!product.imageUrl).slice(0, 3)
  );

  protected readonly placeholders = [1, 2, 3, 4];

  ngOnInit(): void {
    this.load();
  }

  protected load(): void {

    this.loading.set(true);
    this.errorMessage.set('');

    forkJoin({
      categories: this.categoryService.getCategories().pipe(catchError(() => of<Category[]>([]))),
      featured: this.productService.searchProducts({ pageNumber: 1, pageSize: FEATURED_COUNT }).pipe(map(result => result.items)),
      lowestPriced: this.productService
        .searchProducts({ pageNumber: 1, pageSize: VALUE_COUNT, sortBy: 'price', sortOrder: 'asc' })
        .pipe(map(result => result.items), catchError(() => of<Product[]>([])))
    }).subscribe({
      next: ({ categories, featured, lowestPriced }) => {
        this.categories.set(categories);
        this.featured.set(featured);
        this.lowestPriced.set(lowestPriced);
        this.loading.set(false);
      },
      error: () => {
        this.errorMessage.set('We could not load products right now.');
        this.loading.set(false);
      }
    });
  }

  protected scrollTo(id: string): void {
    document.getElementById(id)?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }
}

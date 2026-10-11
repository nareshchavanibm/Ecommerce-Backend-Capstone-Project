import { CurrencyPipe } from '@angular/common';
import { Component, DestroyRef, ElementRef, HostListener, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterLink, RouterLinkActive } from '@angular/router';
import { Subject, catchError, debounceTime, distinctUntilChanged, filter, map, of, switchMap, tap } from 'rxjs';

import { AuthService } from '../../auth/auth';
import { CartService } from '../../core/services/cart';
import { CategoryService } from '../../core/services/category';
import { ProductService } from '../../core/services/product';
import { Category } from '../models/category.model';
import { Product } from '../models/product.model';
import { NotificationBell } from '../notification-bell/notification-bell';
import { Skeleton } from '../ui/skeleton/skeleton';

const MIN_SEARCH_LENGTH = 2;
const SUGGESTION_COUNT = 5;

@Component({
  selector: 'app-navbar',
  imports: [RouterLink, RouterLinkActive, CurrencyPipe, NotificationBell, Skeleton],
  templateUrl: './navbar.html',
  styleUrl: './navbar.css'
})
export class Navbar implements OnInit {

  protected readonly authService = inject(AuthService);

  protected readonly cartService = inject(CartService);

  private readonly categoryService = inject(CategoryService);

  private readonly productService = inject(ProductService);

  private readonly router = inject(Router);

  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);

  private readonly destroyRef = inject(DestroyRef);

  protected readonly menuOpen = signal(false);

  protected readonly megaOpen = signal(false);

  protected readonly categories = signal<Category[]>([]);

  protected readonly categoriesLoading = signal(true);

  protected readonly query = signal('');

  protected readonly suggestions = signal<Product[]>([]);

  protected readonly suggesting = signal(false);

  protected readonly searchOpen = signal(false);

  protected readonly activeIndex = signal(-1);

  private readonly queryInput = new Subject<string>();

  // Options are the product suggestions plus a trailing "see all results" row.
  protected readonly optionCount = computed(() =>
    this.suggestions().length > 0 ? this.suggestions().length + 1 : 0
  );

  protected readonly searchStatus = computed(() => {
    if (!this.searchOpen() || this.suggesting()) {
      return '';
    }

    const count = this.suggestions().length;

    return count === 0 ? 'No matching products.' : `${count} suggestions available.`;
  });

  protected get showsCart(): boolean {
    return this.authService.isLoggedIn() && this.authService.getRole() !== 'Admin';
  }

  constructor() {
    this.queryInput.pipe(
      debounceTime(250),
      distinctUntilChanged(),
      tap(term => {
        this.suggesting.set(term.length >= MIN_SEARCH_LENGTH);
        this.activeIndex.set(-1);
      }),
      switchMap(term =>
        term.length < MIN_SEARCH_LENGTH
          ? of<Product[]>([])
          : this.productService.searchProducts({ search: term, pageSize: SUGGESTION_COUNT }).pipe(
              map(result => result?.items ?? []),
              catchError(() => of<Product[]>([]))
            )
      ),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(items => {
      this.suggestions.set(items);
      this.suggesting.set(false);
    });

    // Close menus after any navigation.
    this.router.events.pipe(
      filter(event => event instanceof NavigationEnd),
      takeUntilDestroyed(this.destroyRef)
    ).subscribe(() => {
      this.menuOpen.set(false);
      this.megaOpen.set(false);
      this.searchOpen.set(false);
    });
  }

  ngOnInit(): void {

    this.categoryService.getCategories().subscribe({
      next: categories => {
        this.categories.set(categories);
        this.categoriesLoading.set(false);
      },
      error: () => this.categoriesLoading.set(false)
    });

    if (this.showsCart) {
      this.cartService.getCart().subscribe({ error: () => { } });
    }
  }

  @HostListener('document:click', ['$event'])
  protected onDocumentClick(event: MouseEvent): void {

    if (!this.host.nativeElement.contains(event.target as Node)) {
      this.megaOpen.set(false);
      this.searchOpen.set(false);
    }
  }

  @HostListener('keydown.escape')
  protected onEscape(): void {

    this.megaOpen.set(false);
    this.searchOpen.set(false);
    this.menuOpen.set(false);
  }

  protected toggleMenu(): void {
    this.menuOpen.update(open => !open);
  }

  protected toggleMega(): void {
    this.megaOpen.update(open => !open);
    this.searchOpen.set(false);
  }

  // Closes the mega menu when keyboard focus moves out of it.
  protected onMegaFocusOut(event: FocusEvent, container: HTMLElement): void {

    if (!container.contains(event.relatedTarget as Node | null)) {
      this.megaOpen.set(false);
    }
  }

  protected onSearchInput(event: Event): void {

    const value = (event.target as HTMLInputElement).value;

    this.query.set(value);
    this.searchOpen.set(value.trim().length >= MIN_SEARCH_LENGTH);
    this.queryInput.next(value.trim());
  }

  protected onSearchFocus(): void {

    if (this.query().trim().length >= MIN_SEARCH_LENGTH) {
      this.searchOpen.set(true);
    }

    this.megaOpen.set(false);
  }

  protected onSearchKeydown(event: KeyboardEvent): void {

    const count = this.optionCount();

    if (event.key === 'ArrowDown' && count > 0) {
      event.preventDefault();
      this.searchOpen.set(true);
      this.activeIndex.update(index => (index + 1) % count);
    } else if (event.key === 'ArrowUp' && count > 0) {
      event.preventDefault();
      this.activeIndex.update(index => (index <= 0 ? count - 1 : index - 1));
    } else if (event.key === 'Enter' && this.activeIndex() >= 0) {
      event.preventDefault();
      this.chooseOption(this.activeIndex());
    }
  }

  protected onSearchFocusOut(event: FocusEvent, container: HTMLElement): void {

    if (!container.contains(event.relatedTarget as Node | null)) {
      this.searchOpen.set(false);
    }
  }

  protected submitSearch(event: Event): void {

    event.preventDefault();

    const term = this.query().trim();

    if (!term) {
      return;
    }

    this.searchOpen.set(false);
    this.router.navigate(['/products'], { queryParams: { search: term } });
  }

  protected chooseOption(index: number): void {

    const product = this.suggestions()[index];

    if (product) {
      this.searchOpen.set(false);
      this.router.navigate(['/products', product.id]);
    } else {
      this.submitSearch(new Event('submit'));
    }
  }

  protected optionId(index: number): string {
    return `search-option-${index}`;
  }

  protected logout(): void {

    this.authService.logout();
    this.cartService.count.set(0);
    this.router.navigate(['/login']);
  }
}

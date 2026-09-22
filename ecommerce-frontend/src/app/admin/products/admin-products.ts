import {
  Component,
  OnInit,
  computed,
  signal
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';

import { ProductService } from '../../core/services/product';
import { CategoryService } from '../../core/services/category';

import { Product } from '../../shared/models/product.model';
import { Category } from '../../shared/models/category.model';

@Component({
  selector: 'app-admin-products',
  imports: [FormsModule, RouterLink],
  templateUrl: './admin-products.html',
  styleUrl: './admin-products.css'
})
export class AdminProducts implements OnInit {

  products = signal<Product[]>([]);
  categories = signal<Category[]>([]);

  readonly pageSize = 10;
  currentPage = signal(1);

  totalPages = computed(() => {
    const count = this.products().length;
    return count === 0
      ? 1
      : Math.ceil(count / this.pageSize);
  });

  pagedProducts = computed(() => {
    const start =
      (this.currentPage() - 1) * this.pageSize;

    return this.products().slice(
      start,
      start + this.pageSize
    );
  });

  loading = signal(true);
  errorMessage = signal('');
  successMessage = signal('');

  constructor(
    private productService: ProductService,
    private categoryService: CategoryService
  ) { }

  ngOnInit(): void {
    this.loadProducts();
    this.loadCategories();
  }

  loadProducts(): void {
    this.loading.set(true);
    this.errorMessage.set('');

    this.productService.getProducts().subscribe({
      next: (data) => {
        this.products.set(data);
        this.currentPage.set(1);
        this.loading.set(false);
      },
      error: (error) => {
        console.error('Error loading products:', error);

        this.errorMessage.set(
          error.error?.message ?? 'Unable to load products.'
        );

        this.loading.set(false);
      }
    });
  }

  loadCategories(): void {
    this.categoryService.getCategories().subscribe({
      next: (data) => {
        this.categories.set(data);
      },
      error: (error) => {
        console.error('Error loading categories:', error);
      }
    });
  }

  getCategoryName(categoryId: number): string {
    const category = this.categories().find(
      category => category.id === categoryId
    );

    return category?.name ?? 'Unknown Category';
  }

  deleteProduct(id: number): void {

    const confirmed = confirm(
      'Are you sure you want to delete this product?'
    );

    if (!confirmed) {
      return;
    }

    this.errorMessage.set('');
    this.successMessage.set('');

    this.productService.deleteProduct(id).subscribe({
      next: () => {
        this.successMessage.set(
          'Product deleted successfully.'
        );

        this.loadProducts();
      },
      error: (error) => {
        console.error('Error deleting product:', error);

        this.errorMessage.set(
          error.error?.message ?? 'Unable to delete product.'
        );
      }
    });
  }

  goToPreviousPage(): void {
    if (this.currentPage() > 1) {
      this.currentPage.update(page => page - 1);
    }
  }

  goToNextPage(): void {
    if (this.currentPage() < this.totalPages()) {
      this.currentPage.update(page => page + 1);
    }
  }
}

import { Component, OnInit, signal } from '@angular/core';
import { Router } from '@angular/router';

import { ProductService } from '../../core/services/product';
import { CategoryService } from '../../core/services/category';

import { Product } from '../../shared/models/product.model';
import { Category } from '../../shared/models/category.model';

@Component({
  selector: 'app-products',
  templateUrl: './products.html',
  styleUrl: './products.css'
})
export class Products implements OnInit {

  products = signal<Product[]>([]);
  categories = signal<Category[]>([]);

  loading = signal(true);
  errorMessage = signal('');

  constructor(
    private productService: ProductService,
    private categoryService: CategoryService,
    private router: Router
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
        this.loading.set(false);
      },

      error: (error) => {
        console.error('Error loading products:', error);

        this.products.set([]);
        this.errorMessage.set(
          error.error?.message ||
          'Unable to load products.'
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

  addProduct(): void {
    this.router.navigate(['/admin/products/add']);
  }

  editProduct(id: number): void {
    this.router.navigate(['/admin/products/edit', id]);
  }

  deleteProduct(product: Product): void {

    const confirmed = confirm(
      `Are you sure you want to delete "${product.name}"?`
    );

    if (!confirmed) {
      return;
    }

    this.productService.deleteProduct(product.id).subscribe({

      next: () => {
        this.products.update(products =>
          products.filter(p => p.id !== product.id)
        );
      },

      error: (error) => {
        console.error('Error deleting product:', error);

        this.errorMessage =
          error.error?.message ||
          'Unable to delete product.';
      }
    });
  }
}

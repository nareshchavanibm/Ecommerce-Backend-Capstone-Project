import { Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';

import { ProductService } from '../../../core/services/product';
import { CategoryService } from '../../../core/services/category';
import { Category } from '../../../shared/models/category.model';
import { CreateProduct } from '../../../shared/models/product.model';

@Component({
  selector: 'app-add-product',
  imports: [FormsModule],
  templateUrl: './add-product.html',
  styleUrl: './add-product.css'
})
export class AddProduct implements OnInit {

  product: CreateProduct = {
    name: '',
    description: '',
    price: 0,
    categoryId: 0,
    initialStock: 0
  };

  categories: Category[] = [];

  errorMessage = '';
  successMessage = '';
  loading = false;

  constructor(
    private productService: ProductService,
    private categoryService: CategoryService,
    private router: Router
  ) { }

  ngOnInit(): void {
    this.loadCategories();
  }

  loadCategories(): void {
    this.categoryService.getCategories().subscribe({
      next: (data) => {
        this.categories = data;
      },
      error: (error) => {
        console.error('Error loading categories:', error);
        this.errorMessage = 'Unable to load categories.';
      }
    });
  }

  addProduct(): void {
    this.errorMessage = '';
    this.successMessage = '';

    if (!this.product.name.trim()) {
      this.errorMessage = 'Product name is required.';
      return;
    }

    if (!this.product.description.trim()) {
      this.errorMessage = 'Product description is required.';
      return;
    }

    if (this.product.price <= 0) {
      this.errorMessage = 'Price must be greater than 0.';
      return;
    }

    if (this.product.categoryId <= 0) {
      this.errorMessage = 'Please select a category.';
      return;
    }

    if (this.product.initialStock < 0) {
      this.errorMessage = 'Initial stock cannot be negative.';
      return;
    }

    this.loading = true;

    this.productService.createProduct(this.product).subscribe({
      next: () => {
        this.loading = false;
        this.successMessage = 'Product created successfully.';

        setTimeout(() => {
          this.router.navigate(['/admin/products']);
        }, 1000);
      },

      error: (error) => {
        console.error('Error creating product:', error);

        this.loading = false;

        this.errorMessage =
          error.error?.message ||
          'Unable to create product.';
      }
    });
  }

  cancel(): void {
    this.router.navigate(['/admin/products']);
  }
}

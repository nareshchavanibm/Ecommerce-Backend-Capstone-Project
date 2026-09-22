import {ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';

import { ProductService } from '../../../core/services/product';
import { CategoryService } from '../../../core/services/category';

import { Category } from '../../../shared/models/category.model';
import {
  Product,
  UpdateProduct
} from '../../../shared/models/product.model';

@Component({
  selector: 'app-edit-product',
  imports: [FormsModule],
  templateUrl: './edit-product.html',
  styleUrl: './edit-product.css'
})
export class EditProduct implements OnInit {

  productId = 0;

  product: UpdateProduct = {
    name: '',
    description: '',
    price: 0,
    categoryId: 0
  };

  stockQuantity = 0;

  categories: Category[] = [];

  loading = false;
  loadingProduct = true;

  errorMessage = '';
  successMessage = '';

  constructor(
    private productService: ProductService,
    private categoryService: CategoryService,
    private route: ActivatedRoute,
    private router: Router,
    private cdr: ChangeDetectorRef
  ) { }

  ngOnInit(): void {
    const id = Number(this.route.snapshot.paramMap.get('id'));

    if (!id || id <= 0) {
      this.errorMessage = 'Invalid product ID.';
      this.loadingProduct = false;
      return;
    }

    this.productId = id;

    this.loadCategories();
    this.loadProduct();
  }

  loadProduct(): void {

    console.log('Loading product ID:', this.productId);

    this.productService.getProductById(this.productId).subscribe({

      next: (data: Product) => {

        console.log('Product received from API:', data);

        this.product = {
          name: data.name,
          description: data.description,
          price: data.price,
          categoryId: data.categoryId
        };

        this.stockQuantity = data.stockQuantity;

        this.loadingProduct = false;

        this.cdr.detectChanges();

        console.log('Product assigned to form:', this.product);
      },

      error: (error) => {

        console.error('Error loading product:', error);

        this.errorMessage =
          error.error?.message ||
          'Unable to load product.';

        this.loadingProduct = false;

        this.cdr.detectChanges();
      }
    });
  }

  loadCategories(): void {
    this.categoryService.getCategories().subscribe({

      next: (data) => {
        this.categories = data;
      },

      error: (error) => {
        console.error('Error loading categories:', error);
      }
    });
  }

  updateProduct(): void {

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

    this.loading = true;

    this.productService
      .updateProduct(this.productId, this.product)
      .subscribe({

        next: () => {

          this.loading = false;

          this.successMessage =
            'Product updated successfully.';

          setTimeout(() => {
            this.router.navigate(['/admin/products']);
          }, 1000);
        },

        error: (error) => {

          console.error('Error updating product:', error);

          this.loading = false;

          this.errorMessage =
            error.error?.message ||
            'Unable to update product.';
        }
      });
  }

  cancel(): void {
    this.router.navigate(['/admin/products']);
  }
}

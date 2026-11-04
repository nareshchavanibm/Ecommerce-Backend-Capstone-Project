import { Component, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { Observable, catchError, map, of, switchMap } from 'rxjs';

import { ProductService } from '../../../core/services/product';
import { CategoryService } from '../../../core/services/category';
import { InventoryService } from '../../../core/services/inventory';

import { Category } from '../../../shared/models/category.model';
import { Inventory } from '../../../shared/models/inventory.model';
import {
  ProductSpecification,
  UpdateProduct
} from '../../../shared/models/product.model';
import { cleanSpecifications, validateSpecifications } from '../../../shared/utils/specifications';
import { SpecEditor } from '../spec-editor/spec-editor';

@Component({
  selector: 'app-edit-product',
  imports: [FormsModule, SpecEditor],
  templateUrl: './edit-product.html',
  styleUrl: './edit-product.css'
})
export class EditProduct implements OnInit {

  productId = 0;

  product: UpdateProduct = {
    name: '',
    description: '',
    price: 0,
    categoryId: 0,
    imageUrl: ''
  };

  imageFailed = false;

  specs = signal<ProductSpecification[]>([]);

  stock = 0;

  // Null when the product has no stock record yet.
  private inventory: Inventory | null = null;

  categories = signal<Category[]>([]);

  loading = signal(false);
  loadingProduct = signal(true);

  errorMessage = signal('');
  successMessage = signal('');

  constructor(
    private productService: ProductService,
    private categoryService: CategoryService,
    private inventoryService: InventoryService,
    private route: ActivatedRoute,
    private router: Router
  ) { }

  ngOnInit(): void {

    this.loadCategories();

    // The same component instance is reused when another product is opened from the admin search.
    this.route.paramMap.subscribe(params => this.open(Number(params.get('id'))));
  }

  private open(id: number): void {

    this.errorMessage.set('');
    this.successMessage.set('');
    this.imageFailed = false;
    this.loadingProduct.set(true);

    if (!id || id <= 0) {
      this.errorMessage.set('Invalid product ID.');
      this.loadingProduct.set(false);
      return;
    }

    this.productId = id;

    this.loadProduct();
  }

  loadProduct(): void {

    const id = this.productId;

    this.productService.getProductById(id).pipe(
      switchMap(data => this.inventoryService.getInventory(id).pipe(
        catchError(() => of(null)),
        map(inventory => ({ data, inventory }))
      ))
    ).subscribe({

      next: ({ data, inventory }) => {

        // A slower response for a product that is no longer open is ignored.
        if (id !== this.productId) {
          return;
        }

        this.product = {
          name: data.name,
          description: data.description,
          price: data.price,
          categoryId: data.categoryId,
          imageUrl: data.imageUrl ?? ''
        };

        this.inventory = inventory;
        this.stock = inventory?.stockQuantity ?? data.stockQuantity ?? 0;

        this.specs.set((data.specifications ?? []).map(spec => ({ ...spec })));

        this.loadingProduct.set(false);
      },

      error: (error) => {

        if (id !== this.productId) {
          return;
        }

        console.error('Error loading product:', error);

        this.errorMessage.set(
          error.error?.message ||
          'Unable to load product.'
        );

        this.loadingProduct.set(false);
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

  updateProduct(): void {

    this.errorMessage.set('');
    this.successMessage.set('');

    if (!this.product.name.trim()) {
      this.errorMessage.set('Product name is required.');
      return;
    }

    if (!this.product.description.trim()) {
      this.errorMessage.set('Product description is required.');
      return;
    }

    if (this.product.price <= 0) {
      this.errorMessage.set('Price must be greater than 0.');
      return;
    }

    if (this.product.categoryId <= 0) {
      this.errorMessage.set('Please select a category.');
      return;
    }

    const specificationError = validateSpecifications(this.specs());

    if (specificationError) {
      this.errorMessage.set(specificationError);
      return;
    }

    if (!Number.isInteger(this.stock) || this.stock < 0) {
      this.errorMessage.set('Stock must be a whole number, 0 or more.');
      return;
    }

    this.loading.set(true);

    this.productService
      .updateProduct(this.productId, {
        ...this.product,
        imageUrl: this.product.imageUrl?.trim() || null,
        specifications: cleanSpecifications(this.specs())
      })
      .pipe(switchMap(() => this.saveStock()))
      .subscribe({

        next: () => {

          this.loading.set(false);

          this.successMessage.set('Product updated successfully.');

          setTimeout(() => {
            this.router.navigate(['/admin/products']);
          }, 1000);
        },

        error: (error) => {

          console.error('Error updating product:', error);

          this.loading.set(false);

          this.errorMessage.set(
            error.error?.message ||
            'Unable to update product.'
          );
        }
      });
  }

  // Only touches inventory when the stock was changed (or had no record and a value was entered).
  private saveStock(): Observable<unknown> {

    const current = this.inventory;

    if (current ? current.stockQuantity === this.stock : this.stock === 0) {
      return of(null);
    }

    const request = { quantity: this.stock, reorderLevel: current?.reorderLevel ?? 5 };

    return current
      ? this.inventoryService.updateInventory(this.productId, request)
      : this.inventoryService.createInventory(this.productId, request);
  }

  cancel(): void {
    this.router.navigate(['/admin/products']);
  }
}

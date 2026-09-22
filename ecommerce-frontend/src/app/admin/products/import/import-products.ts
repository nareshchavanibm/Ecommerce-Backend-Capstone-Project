import { Component } from '@angular/core';
import { Router } from '@angular/router';

import { ProductService } from '../../../core/services/product';

@Component({
  selector: 'app-import-products',
  imports: [],
  templateUrl: './import-products.html',
  styleUrl: './import-products.css'
})
export class ImportProducts {

  selectedFile: File | null = null;

  loading = false;
  errorMessage = '';
  successMessage = '';

  constructor(
    private productService: ProductService,
    private router: Router
  ) { }

  onFileSelected(event: Event): void {

    this.errorMessage = '';
    this.successMessage = '';

    const input =
      event.target as HTMLInputElement;

    if (!input.files ||
      input.files.length === 0) {

      this.selectedFile = null;
      return;
    }

    const file = input.files[0];

    // Check empty file
    if (file.size === 0) {

      this.errorMessage =
        'Selected file is empty.';

      this.selectedFile = null;
      input.value = '';

      return;
    }

    // Get file extension
    const extension =
      file.name
        .substring(file.name.lastIndexOf('.'))
        .toLowerCase();

    // Validate file type
    if (
      extension !== '.csv' &&
      extension !== '.xlsx'
    ) {

      this.errorMessage =
        'Please select a CSV or XLSX file.';

      this.selectedFile = null;
      input.value = '';

      return;
    }

    // Store selected file
    this.selectedFile = file;
  }

  importProducts(): void {

    this.errorMessage = '';
    this.successMessage = '';

    if (!this.selectedFile) {

      this.errorMessage =
        'Please select a CSV or XLSX file.';

      return;
    }

    this.loading = true;

    this.productService
      .importProducts(this.selectedFile)
      .subscribe({

        next: (response) => {

          console.log(
            'Import API response:',
            response
          );

          this.loading = false;

          this.successMessage =
            response.message ||
            'Products imported successfully.';

          // Clear selected file
          this.selectedFile = null;
        },

        error: (error) => {

          console.error(
            'Error importing products:',
            error
          );

          this.loading = false;

          this.errorMessage =
            error.error?.message ||
            error.message ||
            error.error ||
            'Unable to import products.';
        }
      });
  }

  backToProducts(): void {

    this.router.navigate([
      '/admin/products'
    ]);
  }
}

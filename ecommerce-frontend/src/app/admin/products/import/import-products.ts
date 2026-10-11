import { Component, ElementRef, inject, signal, viewChild } from '@angular/core';
import { Router } from '@angular/router';

import { ProductService } from '../../../core/services/product';
import { ProductImportResponse } from '../../../shared/models/product.model';
import { Alert } from '../../../shared/ui/alert/alert';
import { Button } from '../../../shared/ui/button/button';

const MAX_FILE_BYTES = 10 * 1024 * 1024;
const ACCEPTED_EXTENSIONS = ['.csv', '.xlsx'];

const TEMPLATE_CSV =
  'Name,Description,Price,CategoryName,ImageUrl,StockQuantity,ReorderLevel\r\n' +
  'Sample Product,Short description,499,Accessories,,10,2\r\n';

@Component({
  selector: 'app-import-products',
  imports: [Alert, Button],
  templateUrl: './import-products.html',
  styleUrl: './import-products.css'
})
export class ImportProducts {

  private readonly productService = inject(ProductService);
  private readonly router = inject(Router);

  private readonly fileInput =
    viewChild<ElementRef<HTMLInputElement>>('fileInput');

  protected readonly file = signal<File | null>(null);
  protected readonly fileError = signal('');
  protected readonly loading = signal(false);
  protected readonly errorMessage = signal('');
  protected readonly result = signal<ProductImportResponse | null>(null);

  protected onFileSelected(event: Event): void {

    this.result.set(null);
    this.errorMessage.set('');
    this.fileError.set('');
    this.file.set(null);

    const input = event.target as HTMLInputElement;
    const selected = input.files?.[0];

    if (!selected) {
      return;
    }

    const extension = selected.name
      .substring(selected.name.lastIndexOf('.'))
      .toLowerCase();

    if (selected.size === 0) {
      this.rejectFile('The selected file is empty.');
    } else if (!ACCEPTED_EXTENSIONS.includes(extension)) {
      this.rejectFile('Please select a CSV or XLSX file.');
    } else if (selected.size > MAX_FILE_BYTES) {
      this.rejectFile('The file is larger than 10 MB.');
    } else {
      this.file.set(selected);
    }
  }

  protected importProducts(): void {

    const selected = this.file();

    if (!selected || this.loading()) {
      return;
    }

    this.errorMessage.set('');
    this.result.set(null);
    this.loading.set(true);

    this.productService.importProducts(selected).subscribe({

      next: (response) => {
        this.loading.set(false);
        this.result.set(response);
        this.clearFile();
      },

      error: (error) => {
        this.loading.set(false);
        this.errorMessage.set(
          error.error?.message ||
          (typeof error.error === 'string' ? error.error : '') ||
          error.message ||
          'Unable to import products.'
        );
      }
    });
  }

  protected downloadTemplate(): void {

    const url = URL.createObjectURL(
      new Blob([TEMPLATE_CSV], { type: 'text/csv;charset=utf-8' })
    );

    const link = document.createElement('a');
    link.href = url;
    link.download = 'product-import-template.csv';
    link.click();

    URL.revokeObjectURL(url);
  }

  protected formatSize(bytes: number): string {

    return bytes < 1024 * 1024
      ? `${Math.max(1, Math.round(bytes / 1024))} KB`
      : `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  }

  protected backToProducts(): void {
    this.router.navigate(['/admin/products']);
  }

  private rejectFile(message: string): void {
    this.fileError.set(message);
    this.clearFile();
  }

  private clearFile(): void {
    this.file.set(null);

    const input = this.fileInput()?.nativeElement;

    if (input) {
      input.value = '';
    }
  }
}

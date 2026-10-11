import { Component, signal } from '@angular/core';
import { Router } from '@angular/router';
import { concatMap, from, toArray } from 'rxjs';

import { ProductService } from '../../../core/services/product';
import { BulkUpdateProduct } from '../../../shared/models/product.model';

// The API accepts at most 100 products per request.
const BATCH_SIZE = 100;
const MAX_ROWS = 1000;

const FIELDS = ['id', 'name', 'description', 'price', 'categoryid', 'categoryname', 'imageurl'];

@Component({
  selector: 'app-bulk-update-products',
  imports: [],
  templateUrl: './bulk-update-products.html',
  styleUrl: './bulk-update-products.css'
})
export class BulkUpdateProducts {

  readonly fileName = signal('');
  readonly items = signal<BulkUpdateProduct[]>([]);
  readonly rowErrors = signal<string[]>([]);

  readonly loading = signal(false);
  readonly errorMessage = signal('');
  readonly successMessage = signal('');
  readonly missingIds = signal<number[]>([]);

  constructor(
    private productService: ProductService,
    private router: Router
  ) { }

  async onFileSelected(event: Event): Promise<void> {

    this.reset();

    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];

    if (!file) {
      return;
    }

    const extension = file.name
      .substring(file.name.lastIndexOf('.'))
      .toLowerCase();

    if (extension !== '.csv' && extension !== '.json') {
      this.errorMessage.set('Please select a CSV or JSON file.');
      input.value = '';
      return;
    }

    try {
      const records = extension === '.json'
        ? this.readJson(await file.text())
        : this.readCsv(await file.text());

      if (records.length === 0) {
        this.errorMessage.set('The file has no rows to update.');
        return;
      }

      if (records.length > MAX_ROWS) {
        this.errorMessage.set(`A maximum of ${MAX_ROWS} rows can be updated at once.`);
        return;
      }

      const items: BulkUpdateProduct[] = [];
      const errors: string[] = [];

      records.forEach((record, index) => {

        // Row 1 is the header in a CSV file.
        const rowNumber = extension === '.json' ? index + 1 : index + 2;
        const result = this.toItem(record);

        if (typeof result === 'string') {
          errors.push(`Row ${rowNumber}: ${result}`);
        } else {
          items.push(result);
        }
      });

      this.fileName.set(file.name);
      this.items.set(items);
      this.rowErrors.set(errors);

    } catch (error) {
      this.errorMessage.set(
        error instanceof Error
          ? error.message
          : 'The file could not be read.'
      );
    }
  }

  updateProducts(): void {

    const items = this.items();

    if (items.length === 0 || this.loading()) {
      return;
    }

    this.errorMessage.set('');
    this.successMessage.set('');
    this.missingIds.set([]);
    this.loading.set(true);

    const batches: BulkUpdateProduct[][] = [];

    for (let i = 0; i < items.length; i += BATCH_SIZE) {
      batches.push(items.slice(i, i + BATCH_SIZE));
    }

    from(batches)
      .pipe(
        concatMap(batch => this.productService.bulkUpdateProducts(batch)),
        toArray()
      )
      .subscribe({

        next: (responses) => {

          const updated = responses.flat();
          const updatedIds = new Set(updated.map(product => product.id));

          this.loading.set(false);

          this.successMessage.set(
            `Updated ${updated.length} of ${items.length} products.`
          );

          // The API silently skips ids that do not exist.
          this.missingIds.set(
            items
              .map(item => item.id)
              .filter(id => !updatedIds.has(id))
          );

          this.items.set([]);
          this.fileName.set('');
        },

        error: (error) => {

          this.loading.set(false);

          this.errorMessage.set(
            error.error?.message ||
            this.firstValidationError(error.error) ||
            error.error?.title ||
            error.message ||
            'Unable to update products.'
          );
        }
      });
  }

  backToProducts(): void {
    this.router.navigate(['/admin/products']);
  }

  private reset(): void {
    this.fileName.set('');
    this.items.set([]);
    this.rowErrors.set([]);
    this.errorMessage.set('');
    this.successMessage.set('');
    this.missingIds.set([]);
  }

  private readJson(text: string): Record<string, unknown>[] {

    let data: unknown;

    try {
      data = JSON.parse(text);
    } catch {
      throw new Error('The JSON file is not valid.');
    }

    if (!Array.isArray(data)) {
      throw new Error('The JSON file must contain an array of products.');
    }

    return data.map(entry => this.lowerCaseKeys(entry));
  }

  private readCsv(text: string): Record<string, unknown>[] {

    const table = this.parseCsv(text.replace(/^\uFEFF/, ''))
      .filter(row => row.some(cell => cell.trim() !== ''));

    if (table.length === 0) {
      return [];
    }

    const headers = table[0].map(header => header.trim().toLowerCase());

    if (!headers.includes('id')) {
      throw new Error('The first row must be a header row with an Id column.');
    }

    return table.slice(1).map(cells => {
      const record: Record<string, unknown> = {};

      headers.forEach((header, index) => {
        if (FIELDS.includes(header)) {
          record[header] = cells[index] ?? '';
        }
      });

      return record;
    });
  }

  private lowerCaseKeys(entry: unknown): Record<string, unknown> {

    const record: Record<string, unknown> = {};

    if (entry && typeof entry === 'object') {
      for (const [key, value] of Object.entries(entry)) {
        record[key.toLowerCase()] = value;
      }
    }

    return record;
  }

  // Empty cells are omitted so the existing value is kept.
  private toItem(record: Record<string, unknown>): BulkUpdateProduct | string {

    const text = (key: string) => {
      const value = record[key];
      return value === null || value === undefined ? '' : String(value).trim();
    };

    const id = Number(text('id'));

    if (!Number.isInteger(id) || id <= 0) {
      return 'Id must be a whole number greater than 0.';
    }

    const item: BulkUpdateProduct = { id };

    const name = text('name');
    if (name) {
      if (name.length > 100) return 'Name cannot exceed 100 characters.';
      item.name = name;
    }

    const description = text('description');
    if (description) {
      if (description.length > 1000) return 'Description cannot exceed 1000 characters.';
      item.description = description;
    }

    const price = text('price');
    if (price) {
      const value = Number(price);
      if (!Number.isFinite(value) || value <= 0) return 'Price must be a number greater than 0.';
      item.price = value;
    }

    const categoryId = text('categoryid');
    if (categoryId) {
      const value = Number(categoryId);
      if (!Number.isInteger(value) || value <= 0) return 'CategoryId must be a whole number greater than 0.';
      item.categoryId = value;
    }

    const categoryName = text('categoryname');
    if (categoryName) {
      if (categoryName.length > 100) return 'CategoryName cannot exceed 100 characters.';
      item.categoryName = categoryName;
    }

    const imageUrl = text('imageurl');
    if (imageUrl) {
      if (imageUrl.length > 500) return 'ImageUrl cannot exceed 500 characters.';
      item.imageUrl = imageUrl;
    }

    if (Object.keys(item).length === 1) {
      return 'No fields to update.';
    }

    return item;
  }

  private firstValidationError(body: unknown): string {

    const errors = (body as { errors?: Record<string, string[]> } | null)?.errors;

    return errors ? Object.values(errors).flat()[0] ?? '' : '';
  }

  private parseCsv(text: string): string[][] {

    const rows: string[][] = [];
    let row: string[] = [];
    let cell = '';
    let quoted = false;

    for (let i = 0; i < text.length; i++) {

      const char = text[i];

      if (quoted) {
        if (char === '"') {
          if (text[i + 1] === '"') {
            cell += '"';
            i++;
          } else {
            quoted = false;
          }
        } else {
          cell += char;
        }
      } else if (char === '"') {
        quoted = true;
      } else if (char === ',') {
        row.push(cell);
        cell = '';
      } else if (char === '\n' || char === '\r') {
        if (char === '\r' && text[i + 1] === '\n') {
          i++;
        }
        row.push(cell);
        rows.push(row);
        row = [];
        cell = '';
      } else {
        cell += char;
      }
    }

    if (cell !== '' || row.length > 0) {
      row.push(cell);
      rows.push(row);
    }

    return rows;
  }
}

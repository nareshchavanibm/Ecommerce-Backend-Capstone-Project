import { Component, computed, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { forkJoin } from 'rxjs';

import { InventoryService } from '../../core/services/inventory';
import { ProductService } from '../../core/services/product';
import { Inventory } from '../../shared/models/inventory.model';
import { Alert } from '../../shared/ui/alert/alert';
import { Badge } from '../../shared/ui/badge/badge';
import { Button } from '../../shared/ui/button/button';
import { Dialog } from '../../shared/ui/dialog/dialog';
import { Pagination } from '../../shared/ui/pagination/pagination';
import { TextInput } from '../../shared/ui/text-input/text-input';
import { ToastService } from '../../shared/ui/toast/toast.service';

interface StockRow {
  productId: number;
  name: string;
  // null while the product has no stock record yet.
  stock: number | null;
  reorderLevel: number | null;
  low: boolean;
}

const WHOLE_NUMBER = /^\d{1,7}$/;

@Component({
  selector: 'app-inventory',
  imports: [ReactiveFormsModule, Alert, Badge, Button, Dialog, Pagination, TextInput],
  templateUrl: './inventory.html',
  styleUrl: './inventory.css'
})
export class InventoryComponent {

  private readonly pageSize = 10;

  protected readonly rows = signal<StockRow[]>([]);

  protected readonly page = signal(1);

  protected readonly loading = signal(true);

  protected readonly errorMessage = signal('');

  protected readonly totalPages = computed(() => Math.max(1, Math.ceil(this.rows().length / this.pageSize)));

  protected readonly pagedRows = computed(() => {
    const start = (this.page() - 1) * this.pageSize;

    return this.rows().slice(start, start + this.pageSize);
  });

  // Update dialog
  protected readonly dialogOpen = signal(false);

  protected readonly editing = signal<StockRow | null>(null);

  protected readonly saving = signal(false);

  protected readonly saveError = signal('');

  protected readonly form = new FormGroup({
    quantity: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.pattern(WHOLE_NUMBER)] }),
    reorderLevel: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.pattern(WHOLE_NUMBER)] })
  });

  protected readonly messages = {
    quantity: { required: 'Enter the stock quantity.', pattern: 'Use a whole number, 0 or more.' },
    reorderLevel: { required: 'Enter the reorder level.', pattern: 'Use a whole number, 0 or more.' }
  };

  constructor(
    private productService: ProductService,
    private inventoryService: InventoryService,
    private toast: ToastService
  ) {
    this.load();
  }

  protected load(): void {

    this.loading.set(true);
    this.errorMessage.set('');

    forkJoin({
      products: this.productService.getProducts(),
      inventory: this.inventoryService.getAllInventory()
    }).subscribe({
      next: ({ products, inventory }) => {

        const records = new Map<number, Inventory>(inventory.map(record => [record.productId, record]));

        this.rows.set(products.map(product => {
          const record = records.get(product.id);

          return {
            productId: product.id,
            name: product.name,
            stock: record ? record.stockQuantity : null,
            reorderLevel: record ? record.reorderLevel : null,
            low: record ? record.isLowStock : false
          };
        }));

        this.page.set(1);
        this.loading.set(false);
      },
      error: (error) => {
        this.errorMessage.set(error.error?.message || 'Unable to load inventory.');
        this.loading.set(false);
      }
    });
  }

  protected openDialog(row: StockRow): void {

    this.editing.set(row);
    this.saveError.set('');

    this.form.reset({
      quantity: row.stock === null ? '0' : String(row.stock),
      reorderLevel: row.reorderLevel === null ? '5' : String(row.reorderLevel)
    });

    this.dialogOpen.set(true);
  }

  protected save(): void {

    const row = this.editing();

    if (!row) {
      return;
    }

    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    const request = { quantity: Number(value.quantity), reorderLevel: Number(value.reorderLevel) };

    this.saveError.set('');
    this.saving.set(true);

    const call = row.stock === null
      ? this.inventoryService.createInventory(row.productId, request)
      : this.inventoryService.updateInventory(row.productId, request);

    call.subscribe({
      next: (record) => {

        this.rows.update(rows => rows.map(item => item.productId === row.productId
          ? { ...item, stock: record.stockQuantity, reorderLevel: record.reorderLevel, low: record.isLowStock }
          : item));

        this.saving.set(false);
        this.dialogOpen.set(false);
        this.toast.success(`Stock updated for ${row.name}.`);
      },
      error: (error) => {
        this.saving.set(false);
        this.saveError.set(error.error?.message || 'We could not update the stock.');
      }
    });
  }
}

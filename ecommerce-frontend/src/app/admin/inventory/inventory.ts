import {
  Component,
  OnInit,
  computed,
  signal
} from '@angular/core';

import { ProductService } from '../../core/services/product';
import { InventoryService } from '../../core/services/inventory';

import { Inventory } from '../../shared/models/inventory.model';

@Component({
  selector: 'app-inventory',
  imports: [],
  templateUrl: './inventory.html',
  styleUrl: './inventory.css'
})
export class InventoryComponent implements OnInit {

  inventories = signal<Inventory[]>([]);

  readonly pageSize = 10;
  currentPage = signal(1);

  totalPages = computed(() => {
    const count = this.inventories().length;
    return count === 0
      ? 1
      : Math.ceil(count / this.pageSize);
  });

  pagedInventories = computed(() => {
    const start =
      (this.currentPage() - 1) * this.pageSize;

    return this.inventories().slice(
      start,
      start + this.pageSize
    );
  });

  loading = signal(true);

  errorMessage = signal('');

  actionProductId = signal<number | null>(null);

  constructor(
    private productService: ProductService,
    private inventoryService: InventoryService
  ) { }

  ngOnInit(): void {
    this.loadInventory();
  }

  loadInventory(): void {

    this.loading.set(true);
    this.errorMessage.set('');

    this.productService.getProducts().subscribe({

      next: (products) => {

        if (products.length === 0) {

          this.inventories.set([]);
          this.currentPage.set(1);

          this.loading.set(false);

          return;
        }

        let completed = 0;

        const results: Inventory[] = [];

        products.forEach(product => {

          this.inventoryService
            .getInventory(product.id)
            .subscribe({

              next: (inventory) => {

                results.push(inventory);

                completed++;

                if (completed === products.length) {

                  this.inventories.set(results);
                  this.currentPage.set(1);

                  this.loading.set(false);
                }
              },

              error: (error) => {

                console.error(
                  `Error loading inventory for product ${product.id}:`,
                  error
                );

                completed++;

                if (completed === products.length) {

                  this.inventories.set(results);
                  this.currentPage.set(1);

                  this.loading.set(false);
                }
              }

            });

        });

      },

      error: (error) => {

        console.error(
          'Error loading products:',
          error
        );

        this.errorMessage.set(
          error.error?.message ||
          'Unable to load inventory.'
        );

        this.currentPage.set(1);
        this.loading.set(false);
      }

    });
  }

  addStock(productId: number): void {

    const quantityText =
      prompt('Enter quantity to add:');

    if (quantityText === null) {
      return;
    }

    const quantity = Number(quantityText);

    if (
      !Number.isInteger(quantity) ||
      quantity <= 0
    ) {

      this.errorMessage.set(
        'Please enter a valid positive quantity.'
      );

      return;
    }

    this.actionProductId.set(productId);
    this.errorMessage.set('');

    this.inventoryService
      .addStock(
        productId,
        { quantity }
      )
      .subscribe({

        next: () => {

          this.actionProductId.set(null);

          this.loadInventory();
        },

        error: (error) => {

          console.error(
            'Error adding stock:',
            error
          );

          this.actionProductId.set(null);

          this.errorMessage.set(
            error.error?.message ||
            error.error ||
            'Unable to add stock.'
          );
        }

      });
  }

  removeStock(productId: number): void {

    const quantityText =
      prompt('Enter quantity to remove:');

    if (quantityText === null) {
      return;
    }

    const quantity = Number(quantityText);

    if (
      !Number.isInteger(quantity) ||
      quantity <= 0
    ) {

      this.errorMessage.set(
        'Please enter a valid positive quantity.'
      );

      return;
    }

    this.actionProductId.set(productId);
    this.errorMessage.set('');

    this.inventoryService
      .removeStock(
        productId,
        { quantity }
      )
      .subscribe({

        next: () => {

          this.actionProductId.set(null);

          this.loadInventory();
        },

        error: (error) => {

          console.error(
            'Error removing stock:',
            error
          );

          this.actionProductId.set(null);

          this.errorMessage.set(
            error.error?.message ||
            error.error ||
            'Unable to remove stock.'
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
``

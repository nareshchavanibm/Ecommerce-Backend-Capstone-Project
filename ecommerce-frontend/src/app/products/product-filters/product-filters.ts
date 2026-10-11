import { Component, computed, input, linkedSignal, output, signal } from '@angular/core';

import { Category } from '../../shared/models/category.model';

export interface ProductFilterValue {
  category: string;
  minPrice?: number;
  maxPrice?: number;
}

let nextId = 0;

@Component({
  selector: 'app-product-filters',
  templateUrl: './product-filters.html',
  styleUrl: './product-filters.css'
})
export class ProductFilters {

  readonly categories = input<Category[]>([]);

  readonly loadingCategories = input(false);

  readonly category = input('');

  readonly minPrice = input<number | undefined>(undefined);

  readonly maxPrice = input<number | undefined>(undefined);

  readonly filtersChange = output<ProductFilterValue>();

  protected readonly uid = `product-filters-${nextId++}`;

  protected readonly minText = linkedSignal(() => this.minPrice()?.toString() ?? '');

  protected readonly maxText = linkedSignal(() => this.maxPrice()?.toString() ?? '');

  protected readonly touched = signal(false);

  protected readonly priceError = computed(() => {

    const min = this.parse(this.minText());
    const max = this.parse(this.maxText());

    if (Number.isNaN(min) || Number.isNaN(max)) {
      return 'Enter prices as positive numbers.';
    }

    if (min !== undefined && max !== undefined && min > max) {
      return 'Minimum price cannot be higher than maximum price.';
    }

    return '';
  });

  protected selectCategory(name: string): void {

    this.filtersChange.emit({
      category: name,
      minPrice: this.minPrice(),
      maxPrice: this.maxPrice()
    });
  }

  protected applyPrice(event?: Event): void {

    event?.preventDefault();
    this.touched.set(true);

    if (this.priceError()) {
      return;
    }

    this.filtersChange.emit({
      category: this.category(),
      minPrice: this.parse(this.minText()),
      maxPrice: this.parse(this.maxText())
    });
  }

  protected onPriceInput(target: 'min' | 'max', event: Event): void {

    const value = (event.target as HTMLInputElement).value;

    (target === 'min' ? this.minText : this.maxText).set(value);
  }

  // Empty -> undefined; invalid or negative -> NaN.
  private parse(text: string): number | undefined {

    const trimmed = text.trim();

    if (!trimmed) {
      return undefined;
    }

    const value = Number(trimmed);

    return Number.isFinite(value) && value >= 0 ? value : NaN;
  }
}

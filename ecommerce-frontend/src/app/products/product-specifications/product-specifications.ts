import { Component, computed, input, signal } from '@angular/core';

import { ProductSpecification } from '../../shared/models/product.model';
import { Button } from '../../shared/ui/button/button';
import { groupSpecifications } from '../../shared/utils/specifications';

// Rows shown before "Show more", counted across all groups.
const COLLAPSED_ROWS = 4;

@Component({
  selector: 'app-product-specifications',
  imports: [Button],
  templateUrl: './product-specifications.html',
  styleUrl: './product-specifications.css'
})
export class ProductSpecifications {

  readonly specifications = input<ProductSpecification[] | undefined>([]);

  protected readonly expanded = signal(false);

  protected readonly groups = computed(() => groupSpecifications(this.specifications()));

  protected readonly total = computed(() => this.groups().reduce((sum, group) => sum + group.items.length, 0));

  protected readonly collapsible = computed(() => this.total() > COLLAPSED_ROWS);

  protected readonly visibleGroups = computed(() => {

    if (this.expanded() || !this.collapsible()) {
      return this.groups();
    }

    let remaining = COLLAPSED_ROWS;

    return this.groups()
      .map(group => {
        const items = group.items.slice(0, Math.max(remaining, 0));
        remaining -= items.length;

        return { ...group, items };
      })
      .filter(group => group.items.length > 0);
  });
}

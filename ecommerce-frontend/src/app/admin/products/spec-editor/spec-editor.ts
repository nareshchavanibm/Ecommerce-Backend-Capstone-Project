import { Component, computed, input, model, signal } from '@angular/core';

import { ProductSpecification } from '../../../shared/models/product.model';
import {
  COMMON_SPEC_GROUPS,
  MAX_SPECIFICATIONS,
  MAX_SPEC_GROUP,
  MAX_SPEC_NAME,
  MAX_SPEC_VALUE,
  specificationError
} from '../../../shared/utils/specifications';

let nextId = 0;

// Admin editor for a product's specification sheet: one row per Group / Name / Value.
@Component({
  selector: 'app-spec-editor',
  templateUrl: './spec-editor.html',
  styleUrl: './spec-editor.css'
})
export class SpecEditor {

  readonly specifications = model<ProductSpecification[]>([]);

  readonly disabled = input(false);

  protected readonly id = `spec-editor-${nextId++}`;

  protected readonly groups = COMMON_SPEC_GROUPS;

  protected readonly maxRows = MAX_SPECIFICATIONS;

  protected readonly maxGroup = MAX_SPEC_GROUP;

  protected readonly maxName = MAX_SPEC_NAME;

  protected readonly maxValue = MAX_SPEC_VALUE;

  // Rows whose fields have been left once; errors show only for these.
  private readonly touched = signal<ReadonlySet<number>>(new Set());

  protected readonly canAdd = computed(() => this.specifications().length < MAX_SPECIFICATIONS);

  protected add(): void {

    const rows = this.specifications();
    const lastGroup = rows.length > 0 ? rows[rows.length - 1].group : '';

    // New rows start in the previous row's group so a whole section is quick to enter.
    this.specifications.set([...rows, { group: lastGroup ?? '', name: '', value: '' }]);
  }

  protected remove(index: number): void {
    this.specifications.update(rows => rows.filter((_, i) => i !== index));
    this.touched.set(new Set());
  }

  protected update(index: number, field: keyof ProductSpecification, event: Event): void {

    const value = (event.target as HTMLInputElement).value;

    this.specifications.update(rows =>
      rows.map((row, i) => (i === index ? { ...row, [field]: value } : row))
    );
  }

  protected markTouched(index: number): void {
    this.touched.update(set => new Set(set).add(index));
  }

  protected error(index: number): string {
    return this.touched().has(index) ? specificationError(this.specifications()[index]) : '';
  }
}

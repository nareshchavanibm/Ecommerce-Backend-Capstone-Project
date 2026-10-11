import { Component, computed, input, output } from '@angular/core';

@Component({
  selector: 'app-pagination',
  templateUrl: './pagination.html',
  styleUrl: './pagination.css'
})
export class Pagination {

  readonly page = input.required<number>();

  readonly totalPages = input.required<number>();

  readonly pageChange = output<number>();

  // Window of page numbers with `null` marking an ellipsis.
  protected readonly pages = computed<(number | null)[]>(() => {

    const total = this.totalPages();
    const current = this.page();

    if (total <= 7) {
      return Array.from({ length: total }, (_, i) => i + 1);
    }

    const result: (number | null)[] = [1];
    const start = Math.max(2, current - 1);
    const end = Math.min(total - 1, current + 1);

    if (start > 2) {
      result.push(null);
    }

    for (let p = start; p <= end; p++) {
      result.push(p);
    }

    if (end < total - 1) {
      result.push(null);
    }

    result.push(total);

    return result;
  });

  protected go(page: number): void {

    if (page >= 1 && page <= this.totalPages() && page !== this.page()) {
      this.pageChange.emit(page);
    }
  }
}

import { DecimalPipe } from '@angular/common';
import { Component, booleanAttribute, computed, input } from '@angular/core';

import { ratingBand, ratingLabel } from '../../utils/rating';

// Read-only stars; color follows the rating band (poor to excellent).
@Component({
  selector: 'app-star-rating',
  imports: [DecimalPipe],
  templateUrl: './star-rating.html',
  styleUrl: './star-rating.css',
  host: {
    role: 'img',
    '[attr.aria-label]': 'description()',
    '[attr.data-band]': 'band()',
    '[attr.data-size]': 'size()'
  }
})
export class StarRating {

  readonly value = input.required<number>();

  readonly count = input<number | null>(null);

  readonly size = input<'sm' | 'md' | 'lg'>('md');

  readonly showValue = input(false, { transform: booleanAttribute });

  readonly showLabel = input(false, { transform: booleanAttribute });

  protected readonly stars = [1, 2, 3, 4, 5];

  protected readonly band = computed(() => ratingBand(this.value()));

  protected readonly label = computed(() => ratingLabel(this.value()));

  protected readonly percent = computed(() => Math.round(Math.min(5, Math.max(0, this.value())) * 2000) / 100);

  protected readonly description = computed(() => {

    const count = this.count();
    const reviews = count === null ? '' : `, ${count} ${count === 1 ? 'review' : 'reviews'}`;

    return `Rated ${this.value().toFixed(1)} out of 5, ${this.label()}${reviews}`;
  });
}

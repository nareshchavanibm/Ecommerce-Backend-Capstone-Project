import { Component, computed, input, signal } from '@angular/core';

import { Promotion } from '../../models/promotion.model';
import { PromoLink } from '../promo-link/promo-link';
import { Slider } from '../slider/slider';

// Banners slide in from the right on a timer; they can also be swiped, dragged, stepped with
// the buttons or arrow keys, and paused (hover, focus, or the Pause button).
@Component({
  selector: 'app-promo-carousel',
  imports: [PromoLink, Slider],
  templateUrl: './promo-carousel.html',
  styleUrl: './promo-carousel.css'
})
export class PromoCarousel {

  readonly items = input.required<Promotion[]>();

  // Promotions whose picture failed to load fall back to the plain, picture-less banner.
  protected readonly broken = signal<ReadonlySet<number>>(new Set());

  protected markBroken(id: number): void {
    this.broken.update(ids => new Set(ids).add(id));
  }

  // A trailing copy of the first banner lets the last one slide into the first, still moving left.
  protected readonly slides = computed(() => {

    const items = this.items();

    return items.length > 1 ? [...items, items[0]] : items;
  });
}

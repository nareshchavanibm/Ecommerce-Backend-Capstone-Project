import { DatePipe } from '@angular/common';
import { Component, computed, input, signal } from '@angular/core';

import { Promotion } from '../../models/promotion.model';
import { PromoLink } from '../promo-link/promo-link';
import { Slider } from '../slider/slider';

// Enough copies to cover the widest layout (five cards on screen) so the row can always keep sliding.
const COPIES = 5;

// Offers (discount, code, expiry) and ads (image led, labelled "Ad") in a row that slides right to left.
@Component({
  selector: 'app-promo-cards',
  imports: [DatePipe, PromoLink, Slider],
  templateUrl: './promo-cards.html',
  styleUrl: './promo-cards.css'
})
export class PromoCards {

  readonly items = input.required<Promotion[]>();

  readonly kind = input<'offer' | 'ad'>('offer');

  protected readonly copiedId = signal<number | null>(null);

  // Promotions whose picture failed to load (e.g. the saved link is not an image); they show the plain panel instead.
  protected readonly broken = signal<ReadonlySet<number>>(new Set());

  protected markBroken(id: number): void {
    this.broken.update(ids => new Set(ids).add(id));
  }

  // The real cards followed by visual copies of the first ones; the copies are hidden from assistive tech.
  protected readonly slides = computed(() => {

    const items = this.items();

    return items.length > 1
      ? [...items, ...Array.from({ length: COPIES }, (_, i) => items[i % items.length])]
      : items;
  });

  protected readonly copies = computed(() => (this.items().length > 1 ? COPIES : 0));

  protected async copy(promo: Promotion): Promise<void> {

    try {
      await navigator.clipboard.writeText(promo.promoCode);
      this.copiedId.set(promo.id);
      setTimeout(() => this.copiedId.update(id => (id === promo.id ? null : id)), 2000);
    } catch {
      // Clipboard can be blocked; the code is still visible to copy by hand.
    }
  }
}

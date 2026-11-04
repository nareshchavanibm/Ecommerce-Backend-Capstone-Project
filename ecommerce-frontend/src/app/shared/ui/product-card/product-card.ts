import { CurrencyPipe } from '@angular/common';
import { Component, booleanAttribute, inject, input, output, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { AuthService } from '../../../auth/auth';
import { StockAlerts } from '../../../core/services/stock-alerts';
import { Product } from '../../models/product.model';
import { RatingSummary } from '../../models/review.model';
import { productImageUrl } from '../../utils/product-image';
import { Badge } from '../badge/badge';
import { Button } from '../button/button';
import { StarRating } from '../star-rating/star-rating';

@Component({
  selector: 'app-product-card',
  imports: [RouterLink, CurrencyPipe, Badge, Button, StarRating],
  templateUrl: './product-card.html',
  styleUrl: './product-card.css'
})
export class ProductCard {

  readonly product = input.required<Product>();

  readonly imageUrl = input('');

  // Real customer ratings only; nothing is shown until the product has reviews.
  readonly rating = input<RatingSummary | null>(null);

  readonly adding = input(false, { transform: booleanAttribute });

  readonly addToCart = output<Product>();

  // Admins manage products; they do not shop.
  protected readonly canBuy = inject(AuthService).getRole() !== 'Admin';

  protected readonly alerts = inject(StockAlerts);

  // Stock comes from the product API; an unknown value is treated as available.
  protected get inStock(): boolean {
    const stock = this.product().stockQuantity;

    return typeof stock !== 'number' || stock > 0;
  }

  protected readonly imageFailed = signal(false);

  protected get image(): string {
    return this.imageFailed() ? '' : this.imageUrl() || productImageUrl(this.product());
  }
}

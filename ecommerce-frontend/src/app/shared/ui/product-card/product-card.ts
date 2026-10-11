import { CurrencyPipe } from '@angular/common';
import { Component, booleanAttribute, inject, input, output, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { AuthService } from '../../../auth/auth';
import { Product } from '../../models/product.model';
import { productImageUrl } from '../../utils/product-image';
import { Badge } from '../badge/badge';
import { Button } from '../button/button';

@Component({
  selector: 'app-product-card',
  imports: [RouterLink, CurrencyPipe, Badge, Button],
  templateUrl: './product-card.html',
  styleUrl: './product-card.css'
})
export class ProductCard {

  readonly product = input.required<Product>();

  readonly imageUrl = input('');

  readonly adding = input(false, { transform: booleanAttribute });

  readonly addToCart = output<Product>();

  // Admins manage products; they do not shop.
  protected readonly canBuy = inject(AuthService).getRole() !== 'Admin';

  // The product API does not return stock; only an explicit 0 means unavailable.
  protected get inStock(): boolean {
    const stock = this.product().stockQuantity;

    return typeof stock !== 'number' || stock > 0;
  }

  protected readonly imageFailed = signal(false);

  protected get image(): string {
    return this.imageFailed() ? '' : this.imageUrl() || productImageUrl(this.product());
  }
}

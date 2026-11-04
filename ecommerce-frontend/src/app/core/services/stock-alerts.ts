import { HttpClient } from '@angular/common/http';
import { isPlatformBrowser } from '@angular/common';
import { Injectable, PLATFORM_ID, effect, inject, signal, untracked } from '@angular/core';
import { Router } from '@angular/router';

import { environment } from '../../../environments/environment';
import { AuthService } from '../../auth/auth';
import { Product } from '../../shared/models/product.model';
import { ToastService } from '../../shared/ui/toast/toast.service';

// "Notify me" requests are stored on the server; the user gets a notification when the product is restocked.
@Injectable({
  providedIn: 'root'
})
export class StockAlerts {

  private readonly http = inject(HttpClient);

  private readonly authService = inject(AuthService);

  private readonly toast = inject(ToastService);

  private readonly router = inject(Router);

  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  private readonly apiUrl = `${environment.apiUrl}/api/notifications/stock-alerts`;

  private readonly ids = signal<number[]>([]);

  constructor() {
    effect(() => {

      const loggedIn = this.authService.isLoggedIn();

      untracked(() => {
        this.ids.set([]);

        if (loggedIn && this.isBrowser) {
          this.http.get<number[]>(this.apiUrl).subscribe({
            next: ids => this.ids.set(ids),
            error: () => this.ids.set([])
          });
        }
      });
    });
  }

  isSubscribed(productId: number): boolean {
    return this.ids().includes(productId);
  }

  toggle(product: Product): void {

    if (!this.authService.isLoggedIn()) {
      this.router.navigate(['/login']);
      return;
    }

    const subscribed = this.isSubscribed(product.id);
    const url = `${this.apiUrl}/${product.id}`;

    this.ids.update(ids => subscribed ? ids.filter(id => id !== product.id) : [...ids, product.id]);

    const request = subscribed
      ? this.http.delete(url)
      : this.http.put(url, { productName: product.name });

    request.subscribe({
      next: () => subscribed
        ? this.toast.show(`You will no longer be notified about ${product.name}.`)
        : this.toast.success(`We will notify you when ${product.name} is back in stock.`),
      error: () => {
        this.ids.update(ids => subscribed ? [...ids, product.id] : ids.filter(id => id !== product.id));
        this.toast.error('Unable to update your notification. Please try again.');
      }
    });
  }
}

import { Injectable, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, concatMap, from, of, toArray } from 'rxjs';

import { Order } from '../../shared/models/order.model';
import { Payment } from '../../shared/models/payment.model';
import { ToastService } from '../../shared/ui/toast/toast.service';
import { printInvoice } from '../../shared/utils/invoice';
import { CartService } from './cart';
import { PaymentService } from './payment';

// Customer actions on a past order, shared by the order list and order details.
@Injectable({
  providedIn: 'root'
})
export class OrderActions {

  private readonly cartService = inject(CartService);

  private readonly paymentService = inject(PaymentService);

  private readonly toast = inject(ToastService);

  private readonly router = inject(Router);

  readonly reorderingNumber = signal<string | null>(null);

  readonly invoiceNumber = signal<string | null>(null);

  // Items are added one at a time so concurrent cart updates cannot overwrite each other.
  reorder(order: Order): void {

    if (this.reorderingNumber()) {
      return;
    }

    this.reorderingNumber.set(order.orderNumber);

    let failed = 0;

    from(order.items).pipe(
      concatMap(item => this.cartService.addToCart(item.productId, item.quantity).pipe(
        catchError(() => {
          failed++;
          return of(null);
        })
      )),
      toArray()
    ).subscribe(() => {
      this.reorderingNumber.set(null);

      if (failed === order.items.length) {
        this.toast.error('None of these items could be added to your cart. They may be unavailable.');
        return;
      }

      if (failed > 0) {
        this.toast.error(`${failed} item(s) could not be added because they are unavailable.`);
      } else {
        this.toast.success('Items from your order were added to the cart.');
      }

      this.router.navigate(['/cart']);
    });
  }

  downloadInvoice(order: Order, payment: Payment | null = null): void {

    if (payment) {
      printInvoice(order, payment);
      return;
    }

    if (this.invoiceNumber()) {
      return;
    }

    this.invoiceNumber.set(order.orderNumber);

    // Payment details are optional on the invoice.
    this.paymentService.getPaymentByOrderId(order.orderNumber).pipe(
      catchError(() => of(null))
    ).subscribe(result => {
      this.invoiceNumber.set(null);
      printInvoice(order, (Array.isArray(result) ? result[result.length - 1] : result) ?? null);
    });
  }
}

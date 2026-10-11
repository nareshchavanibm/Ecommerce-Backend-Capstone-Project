import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable, toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { catchError, map, of, switchMap, tap } from 'rxjs';

import { OrderService } from '../../core/services/order';
import { PaymentService } from '../../core/services/payment';
import { Order, OrderItem } from '../../shared/models/order.model';
import { Payment } from '../../shared/models/payment.model';
import { Alert } from '../../shared/ui/alert/alert';
import { Badge } from '../../shared/ui/badge/badge';
import { Skeleton } from '../../shared/ui/skeleton/skeleton';

const PAID_ORDER_STATUSES = ['Confirmed', 'Processing', 'Shipped', 'Delivered'];
const SUCCESS_PAYMENT_STATUSES = ['completed', 'successful', 'succeeded', 'paid'];

@Component({
  selector: 'app-order-confirmation',
  imports: [RouterLink, CurrencyPipe, DatePipe, Alert, Badge, Skeleton],
  templateUrl: './order-confirmation.html',
  styleUrl: './order-confirmation.css'
})
export class OrderConfirmation {

  private readonly route = inject(ActivatedRoute);

  private readonly orderService = inject(OrderService);

  private readonly paymentService = inject(PaymentService);

  protected readonly order = signal<Order | null>(null);

  protected readonly payment = signal<Payment | null>(null);

  protected readonly loading = signal(true);

  protected readonly errorMessage = signal('');

  private readonly orderNumber = toSignal(
    this.route.paramMap.pipe(map(params => params.get('id') ?? '')),
    { initialValue: this.route.snapshot.paramMap.get('id') ?? '' }
  );

  // "Paid" comes from the backend's order or payment status, never from the client.
  protected readonly paid = computed(() => {

    const status = this.payment()?.status?.toLowerCase() ?? '';

    return SUCCESS_PAYMENT_STATUSES.includes(status) || PAID_ORDER_STATUSES.includes(this.order()?.status ?? '');
  });

  protected readonly cancelled = computed(() => this.order()?.status === 'Cancelled');

  constructor() {

    toObservable(this.orderNumber).pipe(
      tap(() => {
        this.loading.set(true);
        this.errorMessage.set('');
      }),
      switchMap(id => this.orderService.getOrder(id).pipe(
        tap(order => this.order.set(order)),
        switchMap(order => this.paymentService.getPaymentByOrderId(order.orderNumber).pipe(
          map(result => (Array.isArray(result) ? result[result.length - 1] : result) ?? null),
          catchError(() => of(null))
        )),
        catchError(error => {
          this.errorMessage.set(error.status === 404 ? 'We could not find this order.' : 'Unable to load order details.');
          return of(null);
        })
      )),
      takeUntilDestroyed()
    ).subscribe(payment => {
      this.payment.set(payment);
      this.loading.set(false);
    });
  }

  protected unitPrice(item: OrderItem): number {
    return item.unitPrice ?? item.price;
  }

  protected lineTotal(item: OrderItem): number {
    return item.totalPrice ?? this.unitPrice(item) * item.quantity;
  }
}

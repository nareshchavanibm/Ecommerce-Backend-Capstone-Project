import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toObservable, toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { catchError, debounceTime, map, of } from 'rxjs';

import { NotificationService } from '../../core/services/notifications';
import { OrderService } from '../../core/services/order';
import { PaymentService } from '../../core/services/payment';
import { Order, OrderItem } from '../../shared/models/order.model';
import { Payment } from '../../shared/models/payment.model';
import { Alert } from '../../shared/ui/alert/alert';
import { Badge } from '../../shared/ui/badge/badge';
import { Breadcrumbs, Crumb } from '../../shared/ui/breadcrumbs/breadcrumbs';
import { Button } from '../../shared/ui/button/button';
import { Dialog } from '../../shared/ui/dialog/dialog';
import { Skeleton } from '../../shared/ui/skeleton/skeleton';
import { ToastService } from '../../shared/ui/toast/toast.service';

// Order of a normal fulfilment; Cancelled is handled separately.
const PROGRESS_STEPS = ['Pending', 'Confirmed', 'Processing', 'Shipped', 'Delivered'];

export interface ProgressStep {
  label: string;
  state: 'done' | 'current' | 'upcoming';
}

@Component({
  selector: 'app-order-details',
  imports: [RouterLink, CurrencyPipe, DatePipe, Alert, Badge, Breadcrumbs, Button, Dialog, Skeleton],
  templateUrl: './order-details.html',
  styleUrl: './order-details.css'
})
export class OrderDetails {

  private readonly route = inject(ActivatedRoute);

  private readonly orderService = inject(OrderService);

  private readonly paymentService = inject(PaymentService);

  private readonly toast = inject(ToastService);

  protected readonly order = signal<Order | null>(null);

  protected readonly payment = signal<Payment | null>(null);

  protected readonly loading = signal(true);

  protected readonly errorMessage = signal('');

  protected readonly cancelOpen = signal(false);

  protected readonly cancelling = signal(false);

  protected readonly placeholders = [1, 2, 3];

  private readonly orderNumber = toSignal(
    this.route.paramMap.pipe(map(params => params.get('id') ?? '')),
    { initialValue: this.route.snapshot.paramMap.get('id') ?? '' }
  );

  protected readonly crumbs = computed<Crumb[]>(() => [
    { label: 'My orders', link: ['/orders'] },
    { label: `Order ${this.orderNumber()}` }
  ]);

  protected readonly cancelled = computed(() => this.order()?.status === 'Cancelled');

  // Only pending orders can be cancelled by the customer.
  protected readonly canCancel = computed(() => this.order()?.status === 'Pending');

  protected readonly badgeVariant = computed(() => {

    const status = this.order()?.status;

    return status === 'Cancelled' ? 'danger' : status === 'Pending' ? 'warning' : 'success';
  });

  protected readonly steps = computed<ProgressStep[]>(() => {

    const current = PROGRESS_STEPS.indexOf(this.order()?.status ?? '');

    return PROGRESS_STEPS.map((label, index) => ({
      label,
      state: index < current ? 'done' : index === current ? 'current' : 'upcoming'
    }));
  });

  constructor() {

    toObservable(this.orderNumber).pipe(takeUntilDestroyed())
      .subscribe(number => this.load(number, false));

    // Status changes pushed by the server refresh the page without a loading flash.
    inject(NotificationService).ordersChanged
      .pipe(debounceTime(500), takeUntilDestroyed())
      .subscribe(() => this.load(this.orderNumber(), true));
  }

  protected load(orderNumber = this.orderNumber(), silent = false): void {

    if (!orderNumber) {
      this.errorMessage.set('Invalid order number.');
      this.loading.set(false);
      return;
    }

    if (!silent) {
      this.loading.set(true);
      this.errorMessage.set('');
    }

    this.orderService.getOrder(orderNumber).subscribe({
      next: order => {
        this.order.set(order);
        this.loading.set(false);
        this.loadPayment(order.orderNumber);
      },
      error: error => {
        if (!silent) {
          this.errorMessage.set(error.status === 404 ? 'We could not find this order.' : error.error?.message ?? 'Unable to load order.');
          this.loading.set(false);
        }
      }
    });
  }

  protected confirmCancel(): void {

    const order = this.order();

    if (!order || this.cancelling()) {
      return;
    }

    this.cancelling.set(true);

    this.orderService.cancelOrder(order.orderNumber).subscribe({
      next: updated => {
        this.order.set(updated);
        this.cancelling.set(false);
        this.cancelOpen.set(false);
        this.toast.success(`Order ${updated.orderNumber} was cancelled.`);
      },
      error: error => {
        this.cancelling.set(false);
        this.cancelOpen.set(false);
        this.toast.error(error.error?.message ?? 'We could not cancel this order.');
      }
    });
  }

  protected unitPrice(item: OrderItem): number {
    return item.unitPrice ?? item.price;
  }

  protected lineTotal(item: OrderItem): number {
    return item.totalPrice ?? this.unitPrice(item) * item.quantity;
  }

  // The payment is extra information; the page works without it.
  private loadPayment(orderNumber: string): void {

    this.paymentService.getPaymentByOrderId(orderNumber).pipe(
      map(result => (Array.isArray(result) ? result[result.length - 1] : result) ?? null),
      catchError(() => of(null))
    ).subscribe(payment => this.payment.set(payment));
  }
}

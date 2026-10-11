import { CurrencyPipe, isPlatformBrowser } from '@angular/common';
import { Component, OnInit, PLATFORM_ID, computed, inject, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { finalize, forkJoin } from 'rxjs';

import { AuthService } from '../auth/auth';
import { AccountService } from '../core/services/account';
import { CartService } from '../core/services/cart';
import { OrderService } from '../core/services/order';
import { PaymentService } from '../core/services/payment';
import { Address } from '../shared/models/account.model';
import { Cart, CartItem } from '../shared/models/cart.model';
import { Order } from '../shared/models/order.model';
import { Payment } from '../shared/models/payment.model';
import { Alert } from '../shared/ui/alert/alert';
import { Button } from '../shared/ui/button/button';
import { Skeleton } from '../shared/ui/skeleton/skeleton';

export type CheckoutStage = 'review' | 'processing' | 'pending' | 'failed';

// Remembers an unpaid order so a reload never creates a second one.
const PENDING_ORDER_KEY = 'checkout.pendingOrderNumber';

const SUCCESS_STATUSES = ['completed', 'successful', 'succeeded', 'paid'];
const PENDING_STATUSES = ['pending', 'processing', 'initiated'];

export const PAYMENT_METHODS = [
  { value: 'Card', label: 'Card' },
  { value: 'UPI', label: 'UPI' }
];

@Component({
  selector: 'app-checkout',
  imports: [ReactiveFormsModule, RouterLink, CurrencyPipe, Alert, Button, Skeleton],
  templateUrl: './checkout.html',
  styleUrl: './checkout.css'
})
export class Checkout implements OnInit {

  private readonly authService = inject(AuthService);

  private readonly accountService = inject(AccountService);

  private readonly cartService = inject(CartService);

  private readonly orderService = inject(OrderService);

  private readonly paymentService = inject(PaymentService);

  private readonly router = inject(Router);

  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  protected readonly methods = PAYMENT_METHODS;

  protected readonly method = new FormControl<string>('', { nonNullable: true, validators: [Validators.required] });

  protected readonly email = this.authService.isLoggedIn() ? this.authService.getEmail() : null;

  protected readonly cart = signal<Cart | null>(null);

  protected readonly addresses = signal<Address[]>([]);

  // The user's choice, else the default address, else the first saved one.
  protected readonly selectedAddressId = signal<number | null>(null);

  // Shows the full list only while the user is choosing another address.
  protected readonly changingAddress = signal(false);

  protected chooseAddress(id: number): void {
    this.selectedAddressId.set(id);
    this.changingAddress.set(false);
  }

  protected readonly address = computed(() =>
    this.addresses().find(item => item.id === this.selectedAddressId())
    ?? this.addresses().find(item => item.isDefault)
    ?? this.addresses()[0]
    ?? null
  );

  // Set once an order exists on the server; retries reuse it instead of creating another.
  protected readonly order = signal<Order | null>(null);

  protected readonly loading = signal(true);

  protected readonly loadError = signal('');

  protected readonly stage = signal<CheckoutStage>('review');

  protected readonly errorMessage = signal('');

  protected readonly infoMessage = signal('');

  protected readonly cancelling = signal(false);

  protected readonly placeholders = [1, 2];

  protected readonly items = computed<CartItem[]>(() => this.cart()?.items ?? []);

  // Prefer the server's order total once an order exists.
  protected readonly total = computed(() => this.order()?.totalAmount ?? this.cart()?.totalAmount ?? 0);

  ngOnInit(): void {

    if (!this.authService.isLoggedIn()) {
      this.router.navigate(['/login']);
      return;
    }

    this.load();
  }

  protected load(): void {

    this.loading.set(true);
    this.loadError.set('');

    forkJoin({ cart: this.cartService.getCart(), addresses: this.accountService.getAddresses() }).subscribe({
      next: ({ cart, addresses }) => {
        this.cart.set(cart);
        this.addresses.set(addresses);
        this.resumePendingOrder();
      },
      error: error => {
        this.loadError.set(error.error?.message ?? 'Unable to load your cart.');
        this.loading.set(false);
      }
    });
  }

  protected lineTotal(item: CartItem): number {
    return item.totalPrice ?? item.price * item.quantity;
  }

  protected submit(event: Event): void {

    event.preventDefault();
    this.placeOrder();
  }

  protected placeOrder(): void {

    // Guards against double clicks and repeated submits.
    if (this.stage() === 'processing') {
      return;
    }

    this.method.markAsTouched();

    if (this.method.invalid) {
      return;
    }

    this.errorMessage.set('');
    this.infoMessage.set('');

    const existing = this.order();

    this.stage.set('processing');

    if (existing) {
      this.pay(existing.orderNumber);
      return;
    }

    const cart = this.cart();

    if (!cart || cart.items.length === 0) {
      this.stage.set('review');
      this.router.navigate(['/cart']);
      return;
    }

    if (!this.address()) {
      this.stage.set('review');
      this.errorMessage.set('Add a delivery address before placing your order.');
      return;
    }

    // Only ids and quantities are sent; the server prices the order.
    this.orderService.createOrder({
      items: cart.items.map(item => ({ productId: item.productId, quantity: item.quantity }))
    }).subscribe({
      next: order => {
        this.order.set(order);
        this.storePending(order.orderNumber);
        this.pay(order.orderNumber);
      },
      error: error => {
        this.stage.set('review');
        this.errorMessage.set(error.error?.message ?? 'We could not place your order. Please try again.');
        this.refreshCart();
      }
    });
  }

  protected retryPayment(): void {

    const order = this.order();

    if (!order || this.stage() === 'processing') {
      return;
    }

    this.method.markAsTouched();

    if (this.method.invalid) {
      this.stage.set('review');
      return;
    }

    this.errorMessage.set('');
    this.stage.set('processing');
    this.pay(order.orderNumber);
  }

  protected checkPaymentStatus(): void {

    const order = this.order();

    if (!order || this.stage() === 'processing') {
      return;
    }

    this.stage.set('processing');
    this.reconcile(order.orderNumber);
  }

  protected cancelOrder(): void {

    const order = this.order();

    if (!order || this.cancelling()) {
      return;
    }

    this.cancelling.set(true);

    this.orderService.cancelOrder(order.orderNumber).pipe(
      finalize(() => this.cancelling.set(false))
    ).subscribe({
      next: () => {
        this.clearPending();
        this.order.set(null);
        this.errorMessage.set('');
        this.stage.set('review');
        this.infoMessage.set('Your order was cancelled. Your cart has not been changed.');
        this.refreshCart();
      },
      error: error => {
        this.errorMessage.set(error.error?.message ?? 'We could not cancel the order.');
      }
    });
  }

  private pay(orderNumber: string): void {

    this.paymentService.processPayment(orderNumber, this.method.value).subscribe({
      next: payment => this.handlePayment(orderNumber, payment),
      error: error => {
        const message: string | undefined = error.error?.message;

        // The payment may already have been recorded (e.g. after a dropped response).
        if (message && /already exists/i.test(message)) {
          this.reconcile(orderNumber);
        } else {
          this.fail(message ?? 'Your payment could not be processed.');
        }
      }
    });
  }

  private reconcile(orderNumber: string): void {

    this.paymentService.getPaymentByOrderId(orderNumber).subscribe({
      next: result => {
        const payments: Payment[] = Array.isArray(result) ? result : [result];
        const payment = payments[payments.length - 1];

        if (payment) {
          this.handlePayment(orderNumber, payment);
        } else {
          this.stage.set('pending');
        }
      },
      error: () => {
        this.errorMessage.set('We could not confirm your payment status yet. Please check again shortly.');
        this.stage.set('pending');
      }
    });
  }

  // Success is only declared when the backend reports a completed payment.
  private handlePayment(orderNumber: string, payment: Payment): void {

    const status = (payment?.status ?? '').toLowerCase();

    if (SUCCESS_STATUSES.includes(status)) {
      this.complete(orderNumber);
    } else if (PENDING_STATUSES.includes(status)) {
      this.errorMessage.set('');
      this.stage.set('pending');
    } else {
      this.fail(`Your payment was not completed (status: ${payment?.status ?? 'unknown'}).`);
    }
  }

  private complete(orderNumber: string): void {

    this.cartService.clearCart().pipe(
      finalize(() => {
        this.clearPending();
        this.cartService.count.set(0);
        this.router.navigate(['/order-confirmation', orderNumber]);
      })
    ).subscribe({ error: () => { } });
  }

  private fail(message: string): void {
    this.errorMessage.set(message);
    this.stage.set('failed');
  }

  private resumePendingOrder(): void {

    const pendingNumber = this.readPending();

    if (pendingNumber === null) {
      this.finishLoading();
      return;
    }

    this.orderService.getOrder(pendingNumber).subscribe({
      next: order => {

        if (order.status === 'Pending') {
          this.order.set(order);
          this.infoMessage.set(`You have an unpaid order ${order.orderNumber}. Choose a payment method to complete it, or cancel it.`);
          this.finishLoading();
        } else if (order.status === 'Cancelled') {
          this.clearPending();
          this.finishLoading();
        } else {
          // Already paid: continue to the confirmation page.
          this.clearPending();
          this.router.navigate(['/order-confirmation', order.orderNumber]);
        }
      },
      error: () => {
        this.clearPending();
        this.finishLoading();
      }
    });
  }

  private finishLoading(): void {

    this.loading.set(false);

    if (!this.order() && this.items().length === 0) {
      this.router.navigate(['/cart']);
    }
  }

  private refreshCart(): void {
    this.cartService.getCart().subscribe({ next: cart => this.cart.set(cart), error: () => { } });
  }

  private readPending(): string | null {

    if (!this.isBrowser) {
      return null;
    }

    return sessionStorage.getItem(PENDING_ORDER_KEY) || null;
  }

  private storePending(orderNumber: string): void {

    if (this.isBrowser) {
      sessionStorage.setItem(PENDING_ORDER_KEY, orderNumber);
    }
  }

  private clearPending(): void {

    if (this.isBrowser) {
      sessionStorage.removeItem(PENDING_ORDER_KEY);
    }
  }
}

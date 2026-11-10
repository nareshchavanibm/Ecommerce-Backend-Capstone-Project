import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { debounceTime } from 'rxjs';
import { NotificationService } from '../../core/services/notifications';
import { OrderActions } from '../../core/services/order-actions';
import { OrderService } from '../../core/services/order';
import { Order } from '../../shared/models/order.model';

type OrderFilter = 'all' | 'active' | 'delivered' | 'cancelled';

const PROGRESS_STEPS = ['Pending', 'Confirmed', 'Processing', 'Shipped', 'Delivered'];

function matchesFilter(order: Order, filter: OrderFilter): boolean {
  switch (filter) {
    case 'active':
      return order.status !== 'Delivered' && order.status !== 'Cancelled';
    case 'delivered':
      return order.status === 'Delivered';
    case 'cancelled':
      return order.status === 'Cancelled';
    default:
      return true;
  }
}

@Component({
  selector: 'app-my-orders',
  imports: [RouterLink, DatePipe, CurrencyPipe],
  templateUrl: './my-orders.html',
  styleUrl: './my-orders.css'
})
export class MyOrders implements OnInit {

  protected readonly actions = inject(OrderActions);

  protected readonly filters: { key: OrderFilter; label: string }[] = [
    { key: 'all', label: 'All' },
    { key: 'active', label: 'In progress' },
    { key: 'delivered', label: 'Delivered' },
    { key: 'cancelled', label: 'Cancelled' }
  ];

  protected readonly steps = PROGRESS_STEPS;

  orders = signal<Order[]>([]);

  protected readonly filter = signal<OrderFilter>('all');

  protected readonly search = signal('');

  protected readonly sort = signal<'newest' | 'oldest'>('newest');

  protected readonly counts = computed(() => {
    const orders = this.orders();
    return {
      all: orders.length,
      active: orders.filter((o) => matchesFilter(o, 'active')).length,
      delivered: orders.filter((o) => matchesFilter(o, 'delivered')).length,
      cancelled: orders.filter((o) => matchesFilter(o, 'cancelled')).length
    } as Record<OrderFilter, number>;
  });

  protected readonly visibleOrders = computed(() => {
    const term = this.search().trim().toLowerCase();
    const direction = this.sort() === 'newest' ? -1 : 1;

    return this.orders()
      .filter((o) => matchesFilter(o, this.filter()))
      .filter(
        (o) =>
          !term ||
          o.orderNumber.toLowerCase().includes(term) ||
          o.items.some((i) => i.productName?.toLowerCase().includes(term))
      )
      .sort((a, b) => direction * (new Date(a.orderDate).getTime() - new Date(b.orderDate).getTime()));
  });

  protected stepIndex(order: Order): number {
    return PROGRESS_STEPS.indexOf(order.status);
  }

  protected itemSummary(order: Order): string {
    const names = order.items.map((i) => i.productName).filter(Boolean);
    if (names.length <= 2) {
      return names.join(', ');
    }
    return `${names.slice(0, 2).join(', ')} +${names.length - 2} more`;
  }

  protected totalQuantity(order: Order): number {
    return order.items.reduce((sum, i) => sum + i.quantity, 0);
  }

  protected clearFilters(): void {
    this.filter.set('all');
    this.search.set('');
  }

  loading = signal(true);

  errorMessage = signal('');

  constructor(
    private orderService: OrderService
  ) { }

  ngOnInit(): void {

    this.loadOrders();

  }

  loadOrders(): void {

    this.loading.set(true);

    this.errorMessage.set('');

    this.fetchOrders();
  }

  private readonly liveUpdates = inject(NotificationService).ordersChanged
    .pipe(debounceTime(500), takeUntilDestroyed())
    .subscribe(() => this.fetchOrders());

  // Used by live updates: replaces the list without showing the loading state.
  private fetchOrders(): void {

    this.orderService
      .getMyOrders()
      .subscribe({

        next: (data) => {

          this.orders.set(data);

          this.loading.set(false);

        },

        error: (error) => {

          console.error(
            'Error loading orders:',
            error
          );

          this.errorMessage.set(
            error.error?.message ||
            'Unable to load orders.'
          );

          this.loading.set(false);

        }

      });
  }
}

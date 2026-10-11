import { Component, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { debounceTime } from 'rxjs';
import { NotificationService } from '../../core/services/notifications';
import { OrderService } from '../../core/services/order';
import { Order } from '../../shared/models/order.model';

@Component({
  selector: 'app-my-orders',
  imports: [RouterLink, DatePipe, CurrencyPipe],
  templateUrl: './my-orders.html',
  styleUrl: './my-orders.css'
})
export class MyOrders implements OnInit {

  orders = signal<Order[]>([]);

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

          console.log(
            'My Orders:',
            data
          );

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

import {
  Component,
  OnInit,
  computed,
  signal
} from '@angular/core';

import {
  Router
} from '@angular/router';

import { DatePipe } from '@angular/common';

import { OrderService } from '../../core/services/order';
import { Order } from '../../shared/models/order.model';

@Component({
  selector: 'app-admin-orders',
  imports: [
    DatePipe
  ],
  templateUrl: './orders.html',
  styleUrl: './orders.css'
})
export class AdminOrders implements OnInit {

  orders = signal<Order[]>([]);

  readonly pageSize = 10;
  currentPage = signal(1);

  totalPages = computed(() => {
    const count = this.orders().length;
    return count === 0
      ? 1
      : Math.ceil(count / this.pageSize);
  });

  pagedOrders = computed(() => {
    const start =
      (this.currentPage() - 1) * this.pageSize;

    return this.orders().slice(
      start,
      start + this.pageSize
    );
  });

  loading = signal(true);

  errorMessage = signal('');

  constructor(
    private orderService: OrderService,
    private router: Router
  ) { }

  ngOnInit(): void {
    this.loadOrders();
  }

  loadOrders(): void {

    this.loading.set(true);

    this.errorMessage.set('');

    this.orderService
      .getAllOrders()
      .subscribe({

        next: (data) => {

          this.orders.set(data);
          this.currentPage.set(1);

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

  viewOrder(id: number): void {

    this.router.navigate([
      '/admin/orders/details',
      id
    ]);
  }

  goToPreviousPage(): void {
    if (this.currentPage() > 1) {
      this.currentPage.update(page => page - 1);
    }
  }

  goToNextPage(): void {
    if (this.currentPage() < this.totalPages()) {
      this.currentPage.update(page => page + 1);
    }
  }
}

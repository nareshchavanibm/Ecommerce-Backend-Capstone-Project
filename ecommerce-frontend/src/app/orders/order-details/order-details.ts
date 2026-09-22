import { Component, OnInit, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { DatePipe } from '@angular/common';

import { OrderService } from '../../core/services/order';
import { Order } from '../../shared/models/order.model';

@Component({
  selector: 'app-order-details',
  imports: [
    RouterLink,
    DatePipe
  ],
  templateUrl: './order-details.html',
  styleUrl: './order-details.css'
})
export class OrderDetails implements OnInit {

  order = signal<Order | null>(null);

  loading = signal(true);

  errorMessage = signal('');

  cancelMessage = signal('');

  constructor(
    private route: ActivatedRoute,
    private orderService: OrderService
  ) { }

  ngOnInit(): void {

    const orderId = Number(
      this.route.snapshot.paramMap.get('id')
    );

    if (!orderId || orderId <= 0) {

      this.errorMessage.set(
        'Invalid order ID.'
      );

      this.loading.set(false);

      return;
    }

    this.loadOrder(orderId);
  }

  loadOrder(orderId: number): void {

    this.loading.set(true);
    this.errorMessage.set('');

    this.orderService
      .getOrder(orderId)
      .subscribe({

        next: (data: Order) => {

          console.log('Order:', data);

          this.order.set(data);

          this.loading.set(false);
        },

        error: (error) => {

          console.error(
            'Error loading order:',
            error
          );

          this.errorMessage.set(
            error.error?.message ||
            'Unable to load order.'
          );

          this.loading.set(false);
        }

      });
  }

  cancelOrder(): void {

    const currentOrder = this.order();

    if (!currentOrder) {
      return;
    }

    if (currentOrder.status === 'Cancelled') {
      return;
    }

    this.cancelMessage.set('');

    this.orderService
      .cancelOrder(currentOrder.id)
      .subscribe({

        next: (data: Order) => {

          console.log(
            'Order cancelled:',
            data
          );

          this.order.set(data);

          this.cancelMessage.set(
            'Order cancelled successfully.'
          );
        },

        error: (error) => {

          console.error(
            'Error cancelling order:',
            error
          );

          this.cancelMessage.set(
            error.error?.message ||
            'Unable to cancel order.'
          );
        }

      });
  }
}

import {
  Component,
  OnInit,
  signal
} from '@angular/core';

import {
  ActivatedRoute,
  RouterLink
} from '@angular/router';

import { DatePipe } from '@angular/common';

import { OrderService } from '../../../core/services/order';

import { Order } from '../../../shared/models/order.model';

@Component({
  selector: 'app-admin-order-details',
  imports: [
    RouterLink,
    DatePipe
  ],
  templateUrl: './order-details.html',
  styleUrl: './order-details.css'
})
export class AdminOrderDetails implements OnInit {

  order = signal<Order | null>(null);

  loading = signal(true);

  errorMessage = signal('');

  updateMessage = signal('');

  updating = signal(false);

  selectedStatus = signal('');

  statuses = [
    'Pending',
    'Confirmed',
    'Shipped',
    'Delivered',
    'Cancelled'
  ];

  constructor(
    private route: ActivatedRoute,
    private orderService: OrderService
  ) { }

  ngOnInit(): void {

    const orderId = Number(
      this.route.snapshot.paramMap.get('id')
    );

    if (!orderId) {

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

        next: (data) => {

          this.order.set(data);

          this.selectedStatus.set(
            data.status
          );

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

  onStatusChange(event: Event): void {

    const select =
      event.target as HTMLSelectElement;

    this.selectedStatus.set(
      select.value
    );
  }

  updateStatus(): void {

    const currentOrder = this.order();

    if (!currentOrder) {
      return;
    }

    const status =
      this.selectedStatus();

    if (!status) {

      this.updateMessage.set(
        'Please select a status.'
      );

      return;
    }

    if (status === currentOrder.status) {

      this.updateMessage.set(
        'Please select a different status.'
      );

      return;
    }

    this.updating.set(true);

    this.updateMessage.set('');

    this.orderService
      .updateOrderStatus(
        currentOrder.id,
        { status }
      )
      .subscribe({

        next: (updatedOrder) => {

          this.order.set(updatedOrder);

          this.selectedStatus.set(
            updatedOrder.status
          );

          this.updateMessage.set(
            'Order status updated successfully.'
          );

          this.updating.set(false);
        },

        error: (error) => {

          console.error(
            'Error updating order status:',
            error
          );

          this.updateMessage.set(
            error.error?.message ||
            error.error ||
            'Unable to update order status.'
          );

          this.updating.set(false);
        }

      });
  }
}

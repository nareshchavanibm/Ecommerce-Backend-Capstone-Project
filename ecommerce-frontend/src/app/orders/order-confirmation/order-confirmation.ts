import { Component, OnInit, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { DatePipe } from '@angular/common';

import { OrderService } from '../../core/services/order';
import { Order } from '../../shared/models/order.model';

@Component({
  selector: 'app-order-confirmation',
  imports: [RouterLink,DatePipe],
  templateUrl: './order-confirmation.html',
  styleUrl: './order-confirmation.css'
})
export class OrderConfirmation implements OnInit {

  order = signal<Order | null>(null);

  loading = signal(true);

  errorMessage = signal('');

  constructor(
    private route: ActivatedRoute,
    private orderService: OrderService
  ) { }

  ngOnInit(): void {

    const orderId = Number(
      this.route.snapshot.paramMap.get('id')
    );

    this.orderService
      .getOrder(orderId)
      .subscribe({

        next: (data) => {

          console.log(
            'Order:',
            data
          );

          this.order.set(data);

          this.loading.set(false);

        },

        error: (error) => {

          console.error(
            'Error loading order:',
            error
          );

          this.errorMessage.set(
            'Unable to load order details.'
          );

          this.loading.set(false);

        }

      });
  }
}

import { Component, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { OrderService } from '../core/services/order';
import { CartService } from '../core/services/cart';
import { PaymentService } from '../core/services/payment';
import { Router} from '@angular/router';

import {
  Cart
} from '../shared/models/cart.model';

@Component({
  selector: 'app-cart',
  imports: [RouterLink],
  templateUrl: './cart.html',
  styleUrl: './cart.css'
})
export class CartComponent implements OnInit {

  cart = signal<Cart | null>(null);

  loading = signal(true);

  errorMessage = signal('');

  constructor(
    private cartService: CartService,
    private orderService: OrderService,
    private paymentService: PaymentService,
    private router: Router
  ) { }

  ngOnInit(): void {

    this.loadCart();

  }

  loadCart(): void {

    this.loading.set(true);

    this.errorMessage.set('');

    this.cartService.getCart().subscribe({

      next: (data) => {

        console.log('Cart:', data);

        this.cart.set(data);

        this.loading.set(false);

      },

      error: (error) => {

        console.error(
          'Error loading cart:',
          error
        );

        this.errorMessage.set(
          error.error?.message ||
          'Unable to load cart.'
        );

        this.loading.set(false);

      }

    });
  }

  updateQuantity(
    productId: number,
    quantity: number
  ): void {

    if (quantity < 1) {
      return;
    }

    this.cartService
      .updateCartItem(
        productId,
        quantity
      )
      .subscribe({

        next: (data) => {

          this.cart.set(data);

        },

        error: (error) => {

          console.error(
            'Error updating cart:',
            error
          );

        }

      });
  }

  removeItem(
    productId: number
  ): void {

    this.cartService
      .removeCartItem(productId)
      .subscribe({

        next: (data) => {

          this.cart.set(data);

        },

        error: (error) => {

          console.error(
            'Error removing item:',
            error
          );

        }

      });
  }


  clearCart(): void {

    this.cartService
      .clearCart()
      .subscribe({

        next: () => {

          this.loadCart();

        },

        error: (error) => {

          console.error(
            'Error clearing cart:',
            error
          );

        }

      });
  }

  checkout(): void {

    const currentCart = this.cart();

    if (
      !currentCart ||
      currentCart.items.length === 0
    ) {
      return;
    }

    const orderRequest = {

      items: currentCart.items.map(item => ({

        productId: item.productId,

        quantity: item.quantity

      }))

    };

    console.log(
      'Creating order:',
      orderRequest
    );

    this.orderService
      .createOrder(orderRequest)
      .subscribe({

        next: (order) => {

          console.log(
            'Order created:',
            order
          );

          this.processPayment(order.id);

        },

        error: (error) => {

          console.error(
            'Error creating order:',
            error
          );

        }

      });
  }
  processPayment(orderId: number): void {

    const paymentMethod = 'Card';

    this.paymentService
      .processPayment(
        orderId,
        paymentMethod
      )
      .subscribe({

        next: (payment) => {

          console.log(
            'Payment successful:',
            payment
          );

          // Payment completed
          this.cartService
            .clearCart()
            .subscribe({

              next: () => {

                // Go to confirmation page
                this.router.navigate([
                  '/order-confirmation',
                  orderId
                ]);

              },

              error: (error) => {

                console.error(
                  'Error clearing cart:',
                  error
                );

                // Even if clearing the cart fails,
                // payment was successful.
                this.router.navigate([
                  '/order-confirmation',
                  orderId
                ]);

              }

            });

        },

        error: (error) => {

          console.error(
            'Payment failed:',
            error
          );

        }

      });
  }
}

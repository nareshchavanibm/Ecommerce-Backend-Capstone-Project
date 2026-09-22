import {
  Component,
  OnInit,
  signal
} from '@angular/core';

import {
  ActivatedRoute,
  Router,
  RouterLink
} from '@angular/router';

import { ProductService } from '../../core/services/product';
import { CartService } from '../../core/services/cart';
import { AuthService } from '../../auth/auth';

import { Product } from '../../shared/models/product.model';

@Component({
  selector: 'app-product-details',

  imports: [
    RouterLink
  ],

  templateUrl: './product-details.html',

  styleUrl: './product-details.css'
})
export class ProductDetails implements OnInit {

  product = signal<Product | null>(null);

  loading = signal(true);

  errorMessage = signal('');

  cartMessage = signal('');


  constructor(
    private route: ActivatedRoute,

    private router: Router,

    private productService: ProductService,

    private cartService: CartService,

    private authService: AuthService
  ) { }


  ngOnInit(): void {

    const id = Number(
      this.route.snapshot.paramMap.get('id')
    );


    this.productService
      .getProductById(id)
      .subscribe({

        next: (data) => {

          console.log(
            'Product:',
            data
          );

          this.product.set(data);

          this.loading.set(false);

        },

        error: (error) => {

          console.error(
            'Error loading product:',
            error
          );

          this.errorMessage.set(
            'Unable to load product details.'
          );

          this.loading.set(false);

        }

      });
  }


  addToCart(): void {

    const currentProduct =
      this.product();


    if (!currentProduct) {
      return;
    }


    // Check whether user is logged in
    if (!this.authService.isLoggedIn()) {

      this.router.navigate(['/login']);

      return;
    }


    this.cartMessage.set('');


    this.cartService
      .addToCart(
        currentProduct.id,
        1
      )
      .subscribe({

        next: (cart) => {

          console.log(
            'Cart:',
            cart
          );

          this.cartMessage.set(
            'Product added to cart successfully!'
          );

        },

        error: (error) => {

          console.error(
            'Error adding product to cart:',
            error
          );

          this.cartMessage.set(
            error.error?.message ??
            'Unable to add product to cart.'
          );

        }

      });
  }
}

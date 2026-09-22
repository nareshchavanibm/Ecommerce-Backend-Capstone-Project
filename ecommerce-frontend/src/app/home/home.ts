import {
  Component,
  OnInit,
  signal
} from '@angular/core';

import {
  RouterLink
} from '@angular/router';

import {
  ProductService
} from '../core/services/product';

import {
  Product
} from '../shared/models/product.model';

@Component({
  selector: 'app-home',

  imports: [
    RouterLink
  ],

  templateUrl: './home.html',

  styleUrl: './home.css'
})
export class Home implements OnInit {

  products = signal<Product[]>([]);

  loading = signal(true);

  errorMessage = signal('');


  constructor(
    private productService: ProductService
  ) { }


  ngOnInit(): void {

    this.loadProducts();

  }


  loadProducts(): void {

    this.loading.set(true);

    this.errorMessage.set('');


    this.productService
      .getProducts()
      .subscribe({

        next: (data) => {

          console.log(
            'Products:',
            data
          );

          this.products.set(data);

          this.loading.set(false);

        },

        error: (error) => {

          console.error(
            'Error loading products:',
            error
          );

          this.errorMessage.set(
            'Unable to load products.'
          );

          this.loading.set(false);

        }

      });
  }
}

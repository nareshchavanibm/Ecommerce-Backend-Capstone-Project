import {
  Component,
  OnInit,
  signal
} from '@angular/core';

import {
  FormsModule
} from '@angular/forms';

import {
  RouterLink
} from '@angular/router';

import {
  ProductService
} from '../../core/services/product';

import {
  CategoryService
} from '../../core/services/category';

import {
  Product,
  ProductQuery
} from '../../shared/models/product.model';

import {
  Category
} from '../../shared/models/category.model';

@Component({
  selector: 'app-product-list',

  imports: [
    RouterLink,
    FormsModule
  ],

  templateUrl: './product-list.html',

  styleUrl: './product-list.css'
})
export class ProductList implements OnInit {

  products = signal<Product[]>([]);

  loading = signal(true);

  errorMessage = signal('');

  search = '';

  categories = signal<Category[]>([]);

  categoryId: number | undefined;

  minPrice: number | undefined;

  maxPrice: number | undefined;

  sortBy = '';

  sortOrder = '';

  currentPage = signal(1);

  pageSize = signal(10);

  totalPages = signal(1);

  totalCount = signal(0);


  constructor(
    private productService: ProductService,

    private categoryService: CategoryService
  ) { }


  ngOnInit(): void {

    // Categories are also public
    this.loadCategories();

    // Load products
    this.searchProducts();
  }


  searchProducts(): void {

    this.loading.set(true);

    this.errorMessage.set('');

    const query: ProductQuery = {

      pageNumber: this.currentPage(),

      pageSize: this.pageSize()

    };


    if (this.search.trim()) {

      query.search =
        this.search.trim();
    }


    if (this.categoryId !== undefined) {

      query.categoryId =
        this.categoryId;
    }


    if (this.minPrice !== undefined) {

      query.minPrice =
        this.minPrice;
    }


    if (this.maxPrice !== undefined) {

      query.maxPrice =
        this.maxPrice;
    }


    if (this.sortBy) {

      query.sortBy =
        this.sortBy;
    }


    if (this.sortOrder) {

      query.sortOrder =
        this.sortOrder;
    }


    console.log(
      'Product query:',
      query
    );


    this.productService
      .searchProducts(query)
      .subscribe({

        next: (data) => {

          console.log(
            'Product search response:',
            data
          );


          const response =
            data as any;

          let productList: Product[] = [];


          // Handle array response
          if (Array.isArray(response)) {

            productList =
              response;
          }

          // Handle items response
          else if (
            Array.isArray(response.items)
          ) {

            productList =
              response.items;
          }

          // Handle products response
          else if (
            Array.isArray(response.products)
          ) {

            productList =
              response.products;
          }

          // Handle PascalCase
          else if (
            Array.isArray(response.Items)
          ) {

            productList =
              response.Items;
          }

          else if (
            Array.isArray(response.Products)
          ) {

            productList =
              response.Products;
          }

          else {

            console.error(
              'Unexpected product response format:',
              response
            );
          }


          this.products.set(
            productList
          );


          this.totalCount.set(

            response.totalCount ??
            response.TotalCount ??
            productList.length

          );


          this.totalPages.set(

            response.totalPages ??
            response.TotalPages ??
            1

          );


          this.currentPage.set(

            response.pageNumber ??
            response.PageNumber ??
            this.currentPage()

          );


          this.pageSize.set(

            response.pageSize ??
            response.PageSize ??
            this.pageSize()

          );


          this.loading.set(false);
        },


        error: (error) => {

          console.error(
            'Error loading products:',
            error
          );

          this.products.set([]);

          this.errorMessage.set(

            error.error?.message ??
            'Unable to load products.'

          );

          this.loading.set(false);
        }

      });
  }


  loadCategories(): void {

    this.categoryService
      .getCategories()
      .subscribe({

        next: (data) => {

          console.log(
            'Categories:',
            data
          );

          this.categories.set(data);
        },

        error: (error) => {

          console.error(
            'Error loading categories:',
            error
          );
        }

      });
  }


  getCategoryName(
    categoryId: number
  ): string {

    const category =
      this.categories().find(

        category =>
          category.id === categoryId

      );

    return category?.name ??
      'Unknown Category';
  }


  clearFilters(): void {

    this.search = '';

    this.categoryId =
      undefined;

    this.minPrice =
      undefined;

    this.maxPrice =
      undefined;

    this.sortBy = '';

    this.sortOrder = '';

    this.currentPage.set(1);

    this.searchProducts();
  }


  nextPage(): void {

    if (
      this.currentPage() <
      this.totalPages()
    ) {

      this.currentPage.update(
        page => page + 1
      );

      this.searchProducts();
    }
  }


  previousPage(): void {

    if (
      this.currentPage() > 1
    ) {

      this.currentPage.update(
        page => page - 1
      );

      this.searchProducts();
    }
  }


  goToPage(
    page: number
  ): void {

    if (
      page >= 1 &&
      page <= this.totalPages()
    ) {

      this.currentPage.set(page);

      this.searchProducts();
    }
  }
}

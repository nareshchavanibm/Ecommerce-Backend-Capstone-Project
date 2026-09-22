import { Injectable } from '@angular/core';

import {
  HttpClient,
  HttpParams
} from '@angular/common/http';

import {
  Observable
} from 'rxjs';

import { environment } from '../../../environments/environment';

import {
  Product,
  ProductQuery,
  ProductPagedResult,
  CreateProduct,
  UpdateProduct
} from '../../shared/models/product.model';

@Injectable({
  providedIn: 'root'
})
export class ProductService {

  private apiUrl =
    `${environment.apiUrl}/api/products`;

  constructor(
    private http: HttpClient
  ) { }

  // Get all products
  getProducts(): Observable<Product[]> {

    return this.http.get<Product[]>(
      this.apiUrl
    );
  }

  // Get one product
  getProductById(
    id: number
  ): Observable<Product> {

    return this.http.get<Product>(
      `${this.apiUrl}/${id}`
    );
  }

  // Search, filter, sort and paginate products
  searchProducts(
    query: ProductQuery
  ): Observable<ProductPagedResult> {

    let params = new HttpParams();

    if (query.search) {

      params = params.set(
        'search',
        query.search
      );
    }

    if (query.categoryId !== undefined) {

      params = params.set(
        'categoryId',
        query.categoryId
      );
    }

    if (query.minPrice !== undefined) {

      params = params.set(
        'minPrice',
        query.minPrice
      );
    }

    if (query.maxPrice !== undefined) {

      params = params.set(
        'maxPrice',
        query.maxPrice
      );
    }

    if (query.sortBy) {

      params = params.set(
        'sortBy',
        query.sortBy
      );
    }

    if (query.sortOrder) {

      params = params.set(
        'sortOrder',
        query.sortOrder
      );
    }

    if (query.pageNumber !== undefined) {

      params = params.set(
        'pageNumber',
        query.pageNumber
      );
    }

    if (query.pageSize !== undefined) {

      params = params.set(
        'pageSize',
        query.pageSize
      );
    }

    return this.http.get<ProductPagedResult>(
      `${this.apiUrl}/search`,
      {
        params
      }
    );
  }

  // Create product
  createProduct(
    product: CreateProduct
  ): Observable<Product> {

    return this.http.post<Product>(
      this.apiUrl,
      product
    );
  }

  // Update product
  updateProduct(
    id: number,
    product: UpdateProduct
  ): Observable<void> {

    return this.http.put<void>(
      `${this.apiUrl}/${id}`,
      product
    );
  }

  // Delete product
  deleteProduct(
    id: number
  ): Observable<void> {

    return this.http.delete<void>(
      `${this.apiUrl}/${id}`
    );
  }

  // Import products synchronously
  importProducts(
    file: File
  ): Observable<{
    jobId: string;
    message: string;
  }> {

    const formData = new FormData();

    formData.append(
      'file',
      file
    );

    return this.http.post<{
      jobId: string;
      message: string;
    }>(
      `${this.apiUrl}/import`,
      formData
    );
  }
}

import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

import { environment } from '../../../environments/environment';

import {
  Cart,
  AddCartItem,
  UpdateCartItem
} from '../../shared/models/cart.model';

@Injectable({
  providedIn: 'root'
})
export class CartService {

  private apiUrl = `${environment.apiUrl}/api/cart`;

  constructor(
    private http: HttpClient
  ) { }

  getCart(): Observable<Cart> {

    return this.http.get<Cart>(
      this.apiUrl
    );
  }

  addToCart(
    productId: number,
    quantity: number
  ): Observable<Cart> {

    const request: AddCartItem = {
      productId,
      quantity
    };

    return this.http.post<Cart>(
      `${this.apiUrl}/items`,
      request
    );
  }

  updateCartItem(
    productId: number,
    quantity: number
  ): Observable<Cart> {

    const request: UpdateCartItem = {
      quantity
    };

    return this.http.put<Cart>(
      `${this.apiUrl}/items/${productId}`,
      request
    );
  }

  removeCartItem(
    productId: number
  ): Observable<Cart> {

    return this.http.delete<Cart>(
      `${this.apiUrl}/items/${productId}`
    );
  }

  clearCart(): Observable<void> {

    return this.http.delete<void>(
      this.apiUrl
    );
  }
}

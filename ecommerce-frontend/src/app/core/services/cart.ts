import { Injectable, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';

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

  // Total units in the cart; updated by every cart call for the header badge.
  readonly count = signal(0);

  constructor(
    private http: HttpClient
  ) { }

  private syncCount = (cart: Cart): void => {

    // Some responses (e.g. after a delete) carry no cart body.
    if (!cart || !Array.isArray(cart.items)) {
      return;
    }

    this.count.set(
      cart.items.reduce((sum, item) => sum + item.quantity, 0)
    );
  };

  getCart(): Observable<Cart> {

    return this.http.get<Cart>(
      this.apiUrl
    ).pipe(tap(this.syncCount));
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
    ).pipe(tap(this.syncCount));
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
    ).pipe(tap(this.syncCount));
  }

  removeCartItem(
    productId: number
  ): Observable<Cart> {

    return this.http.delete<Cart>(
      `${this.apiUrl}/items/${productId}`
    ).pipe(tap(this.syncCount));
  }

  clearCart(): Observable<void> {

    return this.http.delete<void>(
      this.apiUrl
    ).pipe(tap(() => this.count.set(0)));
  }
}

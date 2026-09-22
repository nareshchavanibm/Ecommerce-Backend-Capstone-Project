import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

import { environment } from '../../../environments/environment';

import {
  CreateOrder,
  Order,
  UpdateOrderStatus
} from '../../shared/models/order.model';

@Injectable({
  providedIn: 'root'
})
export class OrderService {

  private apiUrl = `${environment.apiUrl}/api/orders`;

  constructor(
    private http: HttpClient
  ) { }

  createOrder(
    request: CreateOrder
  ): Observable<Order> {

    return this.http.post<Order>(
      this.apiUrl,
      request
    );
  }

  getOrder(
    orderId: number
  ): Observable<Order> {

    return this.http.get<Order>(
      `${this.apiUrl}/${orderId}`
    );
  }

  getMyOrders(): Observable<Order[]> {

    return this.http.get<Order[]>(
      `${this.apiUrl}/my-orders`
    );
  }

  cancelOrder(
    orderId: number
  ): Observable<Order> {

    return this.http.put<Order>(
      `${this.apiUrl}/${orderId}/cancel`,
      {}
    );
  }

  getAllOrders(): Observable<Order[]> {

    return this.http.get<Order[]>(
      this.apiUrl
    );
  }

  updateOrderStatus(
    orderId: number,
    request: UpdateOrderStatus
  ): Observable<Order> {

    return this.http.put<Order>(
      `${this.apiUrl}/${orderId}/status`,
      request
    );
  }
}

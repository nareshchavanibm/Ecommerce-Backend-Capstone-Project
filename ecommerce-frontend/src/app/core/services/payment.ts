import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

import { environment } from '../../../environments/environment';

import {
  CreatePayment,
  Payment
} from '../../shared/models/payment.model';

@Injectable({
  providedIn: 'root'
})
export class PaymentService {

  private apiUrl = `${environment.apiUrl}/api/payments`;

  constructor(
    private http: HttpClient
  ) { }

  processPayment(
    orderNumber: string,
    paymentMethod: string
  ): Observable<Payment> {

    const request: CreatePayment = {
      paymentMethod
    };

    return this.http.post<Payment>(
      `${this.apiUrl}/orders/${orderNumber}`,
      request
    );
  }

  getPayment(
    paymentId: number
  ): Observable<Payment> {

    return this.http.get<Payment>(
      `${this.apiUrl}/${paymentId}`
    );
  }

  getPaymentByOrderId(
    orderNumber: string
  ): Observable<Payment> {

    return this.http.get<Payment>(
      `${this.apiUrl}/order/${orderNumber}`
    );
  }
}

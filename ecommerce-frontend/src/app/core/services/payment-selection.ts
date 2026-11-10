import { Injectable, signal } from '@angular/core';

export interface PaymentMethodOption {
  value: string;
  label: string;
}

export const PAYMENT_METHODS: PaymentMethodOption[] = [
  { value: 'Card', label: 'Card' },
  { value: 'UPI', label: 'UPI' },
  { value: 'CashOnDelivery', label: 'Pay on delivery' }
];

export const UPI_OPTIONS: PaymentMethodOption[] = [
  { value: 'GooglePay', label: 'Google Pay' },
  { value: 'PhonePe', label: 'PhonePe' },
  { value: 'Paytm', label: 'Paytm' },
  { value: 'BHIM', label: 'BHIM' },
  { value: 'UpiId', label: 'Other UPI ID' }
];

// Choice made on the payment page and read by checkout. Card numbers are never kept here.
@Injectable({ providedIn: 'root' })
export class PaymentSelection {

  readonly method = signal('');

  readonly upiApp = signal('');

  readonly summary = signal('');

  clear(): void {
    this.method.set('');
    this.upiApp.set('');
    this.summary.set('');
  }
}

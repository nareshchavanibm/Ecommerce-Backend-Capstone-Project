import { Payment } from '../models/payment.model';

export function isCashOnDelivery(payment: Pick<Payment, 'paymentMethod'> | null | undefined): boolean {
  return payment?.paymentMethod === 'CashOnDelivery';
}

// A cash-on-delivery payment stays Pending until the cash is collected.
export function paymentSummary(payment: Payment): string {

  if (isCashOnDelivery(payment)) {
    return `Pay on delivery \u00b7 ${payment.status === 'Pending' ? 'Due on delivery' : payment.status}`;
  }

  return `${payment.paymentMethod} \u00b7 ${payment.status}`;
}

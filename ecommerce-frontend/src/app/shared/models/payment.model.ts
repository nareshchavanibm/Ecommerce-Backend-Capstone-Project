export interface CreatePayment {
  paymentMethod: string;
}

export interface Payment {
  id: number;
  orderNumber: string;
  amount: number;
  paymentMethod: string;
  status: string;
  transactionId: string | null;
  paymentDate: string;
}

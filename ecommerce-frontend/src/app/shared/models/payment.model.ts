export interface CreatePayment {
  paymentMethod: string;
}

export interface Payment {
  id: number;
  orderId: number;
  amount: number;
  paymentMethod: string;
  status: string;
  transactionId: string | null;
  paymentDate: string;
}

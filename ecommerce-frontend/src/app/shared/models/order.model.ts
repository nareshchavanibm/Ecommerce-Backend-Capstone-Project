export interface CreateOrderItem {
  productId: number;
  quantity: number;
}

export interface CreateOrder {
  items: CreateOrderItem[];
}

export interface OrderItem {
  productId: number;
  productName: string;
  price: number;
  unitPrice?: number;
  totalPrice?: number;
  quantity: number;
}

export interface Order {
  orderNumber: string;
  userId: number;
  orderDate: string;
  totalAmount: number;
  status: string;
  items: OrderItem[];
}

export interface UpdateOrderStatus {
  status: string;
}

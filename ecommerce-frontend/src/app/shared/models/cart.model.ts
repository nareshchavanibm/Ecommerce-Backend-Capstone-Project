export interface AddCartItem {
  productId: number;
  quantity: number;
}

export interface UpdateCartItem {
  quantity: number;
}

export interface CartItem {
  productId: number;
  productName: string;
  price: number;
  quantity: number;
  totalPrice: number;
}

export interface Cart {
  id: number;
  userId: number;
  items: CartItem[];
  totalAmount: number;
}

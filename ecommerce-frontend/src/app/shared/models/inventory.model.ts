export interface Inventory {
  productId: number;
  productName: string;
  stockQuantity: number;
  reorderLevel: number;
  isLowStock: boolean;
}

export interface AddStock {
  quantity: number;
}

export interface RemoveStock {
  quantity: number;
}

export interface Inventory {
  productId: number;
  productName: string;
  stockQuantity: number;
  reorderLevel: number;
  isLowStock: boolean;
}

export interface InventoryUpdate {
  quantity: number;
  reorderLevel: number;
}

export interface Product {
  id: number;
  name: string;
  description: string;
  price: number;
  categoryId: number;
stockQuantity: number; // Add stockQuantity property
}

export interface CreateProduct {
  name: string;
  description: string;
  price: number;
  categoryId: number;
  initialStock: number;

}

export interface UpdateProduct {
  name: string;
  description: string;
  price: number;
  categoryId: number;
}

export interface ProductQuery {
  search?: string;
  categoryId?: number;
  minPrice?: number;
  maxPrice?: number;
  sortBy?: string;
  sortOrder?: string;
  pageNumber?: number;
  pageSize?: number;
}

export interface ProductPagedResult {
  items: Product[];
  totalCount: number;
  pageNumber: number;
  pageSize: number;
  totalPages: number;
}

export interface Product {
  id: number;
  name: string;
  description: string;
  price: number;
  categoryId: number;
stockQuantity: number; // Add stockQuantity property
  imageUrl?: string | null;
  categoryName?: string;
  // Only returned when a single product is fetched.
  specifications?: ProductSpecification[];
}

export interface ProductSpecification {
  // Section heading such as "Display"; the API treats blank as "General".
  group: string | null;
  name: string;
  value: string;
}

export interface CreateProduct {
  name: string;
  description: string;
  price: number;
  categoryId: number;
  initialStock: number;
  imageUrl?: string | null;
  specifications?: ProductSpecification[];

}

export interface UpdateProduct {
  name: string;
  description: string;
  price: number;
  categoryId: number;
  imageUrl?: string | null;
  // Omit to keep the current specifications; an empty list clears them.
  specifications?: ProductSpecification[];
}

// Mirrors PUT /api/products/bulk-update; omitted fields are left unchanged.
export interface BulkUpdateProduct {
  id: number;
  name?: string;
  description?: string;
  price?: number;
  categoryId?: number;
  categoryName?: string;
  imageUrl?: string;
}

// Response of POST /api/products/import
export interface ProductImportResponse {
  message: string;
  totalRows?: number;
  inserted?: number;
  errors?: number;
  errorDetails?: string[];
}

export interface ProductQuery {
  search?: string;
  categoryId?: number;
  // The product API filters by category name, not id.
  category?: string;
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
  totalItems?: number;
  pageNumber: number;
  pageSize: number;
  totalPages: number;
}

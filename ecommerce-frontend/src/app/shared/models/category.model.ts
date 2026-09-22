export interface Category {
  id: number;
  name: string;
  productCount: number;
}

export interface CreateCategory {
  name: string;
}

export interface UpdateCategory {
  name: string;
}

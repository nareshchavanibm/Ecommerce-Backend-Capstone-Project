import { Product } from '../models/product.model';

// Products without a stored image get a stock photo matched to their name/category; `lock` keeps it stable per product.
export function productImageUrl(product: Pick<Product, 'id' | 'name' | 'categoryName' | 'imageUrl'>, size = 400): string {
  if (product.imageUrl) {
    return product.imageUrl;
  }

  const words = `${product.categoryName ?? ''} ${product.name}`
    .toLowerCase()
    .match(/[a-z]{3,}/g) ?? [];
  const keywords = [...new Set(words)].slice(0, 3).join(',') || 'product';

  return `https://loremflickr.com/${size}/${size}/${keywords}?lock=${product.id}`;
}

export type PromotionType = 'Banner' | 'Ad' | 'Offer';

export interface Promotion {
  id: number;
  type: PromotionType;
  title: string;
  subtitle: string;
  imageUrl: string;
  linkUrl: string;
  discountText: string;
  promoCode: string;
  startsAt: string | null;
  endsAt: string | null;
  isActive: boolean;
  displayOrder: number;
  createdAt: string;
}

// Body of POST/PUT /api/promotions
export interface PromotionInput {
  type: PromotionType;
  title: string;
  subtitle: string;
  imageUrl: string;
  linkUrl: string;
  discountText: string;
  promoCode: string;
  startsAt: string | null;
  endsAt: string | null;
  isActive: boolean;
  displayOrder: number;
}

// Response of GET /api/trending
export interface TrendingEntry {
  productId: number;
  unitsSold: number;
}

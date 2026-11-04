export interface Review {
  id: number;
  productId: number;
  reviewerName: string;
  rating: number;
  title: string | null;
  body: string | null;
  createdAt: string;
  updatedAt: string | null;
  isMine: boolean;
}

export interface RatingSummary {
  productId: number;
  average: number;
  count: number;
  // Index 0 holds the number of 1-star reviews, index 4 the number of 5-star reviews.
  distribution: number[];
}

export interface ReviewPage {
  items: Review[];
  totalItems: number;
  page: number;
  pageSize: number;
  summary: RatingSummary;
}

export interface MyReviewStatus {
  canReview: boolean;
  review: Review | null;
}

export interface ReviewInput {
  rating: number;
  title: string | null;
  body: string | null;
}

export type ReviewSort = 'newest' | 'highest' | 'lowest';

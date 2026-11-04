import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, of } from 'rxjs';

import { environment } from '../../../environments/environment';
import {
  MyReviewStatus,
  RatingSummary,
  Review,
  ReviewInput,
  ReviewPage,
  ReviewSort
} from '../../shared/models/review.model';

@Injectable({
  providedIn: 'root'
})
export class ReviewService {

  private readonly http = inject(HttpClient);

  private readonly apiUrl = `${environment.apiUrl}/api/reviews`;

  getReviews(productId: number, page: number, sort: ReviewSort, pageSize = 10): Observable<ReviewPage> {

    const params = new HttpParams()
      .set('page', page)
      .set('pageSize', pageSize)
      .set('sort', sort);

    return this.http.get<ReviewPage>(`${this.apiUrl}/product/${productId}`, { params });
  }

  // Ratings for product cards; products without reviews are omitted from the result.
  getSummaries(productIds: number[]): Observable<Map<number, RatingSummary>> {

    if (productIds.length === 0) {
      return of(new Map());
    }

    const params = new HttpParams().set('productIds', productIds.join(','));

    return this.http.get<RatingSummary[]>(`${this.apiUrl}/summary`, { params }).pipe(
      map(list => new Map(list.map(summary => [summary.productId, summary] as const))),
      catchError(() => of(new Map<number, RatingSummary>()))
    );
  }

  getMine(productId: number): Observable<MyReviewStatus> {
    return this.http.get<MyReviewStatus>(`${this.apiUrl}/product/${productId}/me`);
  }

  create(productId: number, input: ReviewInput): Observable<Review> {
    return this.http.post<Review>(`${this.apiUrl}/product/${productId}`, input);
  }

  update(id: number, input: ReviewInput): Observable<Review> {
    return this.http.put<Review>(`${this.apiUrl}/${id}`, input);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/${id}`);
  }
}

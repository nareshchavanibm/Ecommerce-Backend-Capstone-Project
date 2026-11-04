import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, forkJoin, map, of, switchMap } from 'rxjs';

import { environment } from '../../../environments/environment';
import { Product } from '../../shared/models/product.model';
import { TrendingEntry } from '../../shared/models/promotion.model';
import { ProductService } from './product';

// Best sellers are calculated from recent orders; this resolves them to products.
@Injectable({
  providedIn: 'root'
})
export class TrendingService {

  private readonly http = inject(HttpClient);

  private readonly productService = inject(ProductService);

  private readonly apiUrl = `${environment.apiUrl}/api/trending`;

  getTrendingProducts(limit = 8, days = 30): Observable<Product[]> {

    // Ask for extra ids so products that are gone or unavailable can be skipped.
    const params = new HttpParams().set('days', days).set('take', limit + 4);

    return this.http.get<TrendingEntry[]>(this.apiUrl, { params }).pipe(
      switchMap(entries =>
        entries.length === 0
          ? of<Product[]>([])
          : forkJoin(
              entries.map(entry =>
                this.productService.getProductById(entry.productId).pipe(catchError(() => of(null)))
              )
            ).pipe(
              map(products => products.filter((product): product is Product => !!product).slice(0, limit))
            )
      )
    );
  }
}

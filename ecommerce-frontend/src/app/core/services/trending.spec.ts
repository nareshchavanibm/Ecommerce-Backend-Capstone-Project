import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { environment } from '../../../environments/environment';
import { Product } from '../../shared/models/product.model';
import { TrendingService } from './trending';

const product = (id: number): Product => ({ id, name: `P${id}`, description: '', price: 10, categoryId: 1, stockQuantity: 5 });

describe('TrendingService', () => {
  let service: TrendingService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(TrendingService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('returns products in sales order and skips the ones that no longer exist', () => {
    let result: Product[] = [];

    service.getTrendingProducts(2).subscribe(products => (result = products));

    const request = http.expectOne(r => r.url === `${environment.apiUrl}/api/trending`);
    expect(request.request.params.get('take')).toBe('6');
    request.flush([{ productId: 3, unitsSold: 9 }, { productId: 1, unitsSold: 5 }, { productId: 2, unitsSold: 1 }]);

    http.expectOne(`${environment.apiUrl}/api/products/3`).flush({ message: 'gone' }, { status: 404, statusText: 'Not Found' });
    http.expectOne(`${environment.apiUrl}/api/products/1`).flush(product(1));
    http.expectOne(`${environment.apiUrl}/api/products/2`).flush(product(2));

    expect(result.map(p => p.id)).toEqual([1, 2]);
  });

  it('returns an empty list without product lookups when nothing has sold', () => {
    let result: Product[] | null = null;

    service.getTrendingProducts().subscribe(products => (result = products));

    http.expectOne(r => r.url === `${environment.apiUrl}/api/trending`).flush([]);

    expect(result).toEqual([]);
  });
});

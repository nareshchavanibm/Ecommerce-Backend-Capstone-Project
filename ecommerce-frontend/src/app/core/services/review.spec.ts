import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { environment } from '../../../environments/environment';
import { ReviewService } from './review';

describe('ReviewService', () => {
  let service: ReviewService;
  let http: HttpTestingController;

  const api = `${environment.apiUrl}/api/reviews`;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    service = TestBed.inject(ReviewService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('requests a page of reviews with sort and paging', () => {
    service.getReviews(7, 2, 'highest', 5).subscribe();

    const request = http.expectOne(r => r.url === `${api}/product/7`);

    expect(request.request.params.get('page')).toBe('2');
    expect(request.request.params.get('pageSize')).toBe('5');
    expect(request.request.params.get('sort')).toBe('highest');
    request.flush({});
  });

  it('skips the request when there are no product ids', () => {
    let result: Map<number, unknown> | null = null;

    service.getSummaries([]).subscribe(map => (result = map));

    expect(result!.size).toBe(0);
  });

  it('keys summaries by product id', () => {
    let result = new Map<number, { average: number }>();

    service.getSummaries([1, 2]).subscribe(map => (result = map));

    const request = http.expectOne(r => r.url === `${api}/summary`);

    expect(request.request.params.get('productIds')).toBe('1,2');
    request.flush([{ productId: 2, average: 4.5, count: 3, distribution: [0, 0, 0, 1, 2] }]);

    expect(result.get(2)?.average).toBe(4.5);
    expect(result.has(1)).toBe(false);
  });

  it('falls back to no ratings when the summary call fails', () => {
    let result: Map<number, unknown> | null = null;

    service.getSummaries([1]).subscribe(map => (result = map));

    http.expectOne(r => r.url === `${api}/summary`).flush({}, { status: 500, statusText: 'Server Error' });

    expect(result!.size).toBe(0);
  });

  it('creates, updates and deletes reviews', () => {
    const input = { rating: 4, title: null, body: 'Nice' };

    service.create(3, input).subscribe();
    const created = http.expectOne(`${api}/product/3`);
    expect(created.request.method).toBe('POST');
    expect(created.request.body).toEqual(input);
    created.flush({});

    service.update(9, input).subscribe();
    expect(http.expectOne(`${api}/9`).request.method).toBe('PUT');

    service.delete(9).subscribe();
    const removed = http.expectOne(`${api}/9`);
    expect(removed.request.method).toBe('DELETE');
  });
});

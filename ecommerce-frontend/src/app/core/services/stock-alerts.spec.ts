import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { Product } from '../../shared/models/product.model';
import { StockAlerts } from './stock-alerts';

const product: Product = { id: 5, name: 'Trail Shoe', description: '', price: 99, categoryId: 1, stockQuantity: 0 };

describe('StockAlerts', () => {

  let http: HttpTestingController;
  let alerts: StockAlerts;

  beforeEach(() => {
    localStorage.setItem('token', 't');

    TestBed.configureTestingModule({
      providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()]
    });

    http = TestBed.inject(HttpTestingController);
    alerts = TestBed.inject(StockAlerts);
    TestBed.tick();
  });

  afterEach(() => {
    http.verify();
    localStorage.clear();
  });

  it('loads the requests saved on the server for a signed-in user', () => {

    http.expectOne(req => req.method === 'GET' && req.url.endsWith('/api/notifications/stock-alerts')).flush([5]);

    expect(alerts.isSubscribed(5)).toBe(true);
    expect(alerts.isSubscribed(6)).toBe(false);
  });

  it('saves a new request with the product name and removes it again', () => {

    http.expectOne(req => req.method === 'GET').flush([]);

    alerts.toggle(product);

    const put = http.expectOne(req => req.method === 'PUT' && req.url.endsWith('/stock-alerts/5'));

    expect(put.request.body).toEqual({ productName: 'Trail Shoe' });
    put.flush(null);
    expect(alerts.isSubscribed(5)).toBe(true);

    alerts.toggle(product);
    http.expectOne(req => req.method === 'DELETE' && req.url.endsWith('/stock-alerts/5')).flush(null);
    expect(alerts.isSubscribed(5)).toBe(false);
  });

  it('undoes the change when the server rejects it', () => {

    http.expectOne(req => req.method === 'GET').flush([]);

    alerts.toggle(product);
    http.expectOne(req => req.method === 'PUT').flush({}, { status: 500, statusText: 'Server Error' });

    expect(alerts.isSubscribed(5)).toBe(false);
  });
});

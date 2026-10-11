import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, TestRequest, provideHttpClientTesting } from '@angular/common/http/testing';
import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';

import { OrderDetails } from './order-details';

@Component({ template: '' })
class Blank { }

const order = (status = 'Pending') => ({
  orderNumber: 'ORD-7',
  userId: 1,
  orderDate: '2026-01-01T00:00:00Z',
  totalAmount: 200,
  status,
  items: [{ productId: 5, productName: 'Trail Shoe', price: 100, quantity: 2 }]
});

describe('OrderDetails', () => {

  let http: HttpTestingController;
  let harness: RouterTestingHarness;
  let component: OrderDetails;

  const match = (method: string, suffix: string): TestRequest[] =>
    http.match(req => req.method === method && req.url.endsWith(suffix));

  const settle = async (): Promise<void> => {
    TestBed.tick();
    await harness.fixture.whenStable();
    harness.detectChanges();
  };

  const el = (): HTMLElement => harness.routeNativeElement!;

  const open = async (status = 'Pending', url = '/orders/ORD-7'): Promise<void> => {
    localStorage.setItem('token', 't');

    TestBed.configureTestingModule({
      providers: [
        provideRouter([{ path: 'orders/:id', component: OrderDetails }, { path: '**', component: Blank }]),
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });

    http = TestBed.inject(HttpTestingController);
    harness = await RouterTestingHarness.create();
    component = await harness.navigateByUrl(url, OrderDetails);

    match('GET', '/api/orders/ORD-7')[0]?.flush(order(status));
    await settle();

    // Payment is optional extra information.
    match('GET', '/api/payments/order/ORD-7')[0]?.flush([]);
    await settle();
  };

  afterEach(() => {
    http.verify();
    localStorage.clear();
  });

  it('shows the order items, total and status', async () => {

    await open('Shipped');

    expect(el().textContent).toContain('Order ORD-7');
    expect(el().textContent).toContain('Trail Shoe');
    expect(el().textContent).toContain('Shipped');
    expect(el().querySelector('[aria-current="step"]')?.textContent).toContain('Shipped');
  });

  it('offers cancelling only while the order is pending', async () => {

    await open('Confirmed');

    expect(el().querySelector('.actions')?.textContent).not.toContain('Cancel order');
  });

  it('cancels a pending order and shows the cancelled state', async () => {

    await open('Pending');

    expect(el().textContent).toContain('You can cancel it until it is confirmed');

    component['confirmCancel']();

    match('PUT', '/api/orders/ORD-7/cancel')[0].flush(order('Cancelled'));
    await settle();

    expect(el().textContent).toContain('This order was cancelled.');
    expect(el().querySelector('.progress')).toBeNull();
  });

  it('shows an error when the order cannot be found', async () => {

    localStorage.setItem('token', 't');

    TestBed.configureTestingModule({
      providers: [
        provideRouter([{ path: 'orders/:id', component: OrderDetails }]),
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });

    http = TestBed.inject(HttpTestingController);
    harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/orders/ORD-7', OrderDetails);

    match('GET', '/api/orders/ORD-7')[0].flush({ message: 'Order not found.' }, { status: 404, statusText: 'Not Found' });
    await settle();

    expect(el().textContent).toContain('We could not find this order.');
  });
});

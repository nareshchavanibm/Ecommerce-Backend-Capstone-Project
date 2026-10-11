import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, TestRequest, provideHttpClientTesting } from '@angular/common/http/testing';
import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';

import { Checkout } from './checkout';

@Component({ template: '' })
class Blank { }

const cart = {
  id: 1,
  userId: 1,
  items: [{ productId: 5, productName: 'Trail Shoe', price: 100, quantity: 2, totalPrice: 200 }],
  totalAmount: 200
};

const order = (orderNumber = 'ORD-10', status = 'Pending') => ({
  orderNumber,
  userId: 1,
  orderDate: '2026-01-01T00:00:00Z',
  totalAmount: 200,
  status,
  items: [{ productId: 5, productName: 'Trail Shoe', price: 100, quantity: 2 }]
});

const address = {
  id: 1, fullName: 'Asha Rao', phone: '9999999999', addressLine1: '12 MG Road', city: 'Pune',
  state: 'MH', postalCode: '411001', country: 'India', isDefault: true
};

const payment = (status: string, orderNumber = 'ORD-10') => ({
  id: 1, orderNumber, amount: 200, paymentMethod: 'Card', status, transactionId: 'tx-1', paymentDate: '2026-01-01T00:00:00Z'
});

describe('Checkout', () => {

  let http: HttpTestingController;
  let harness: RouterTestingHarness;
  let component: Checkout;

  const match = (method: string, suffix: string): TestRequest[] =>
    http.match(req => req.method === method && req.url.endsWith(suffix));

  const settle = async (): Promise<void> => {
    TestBed.tick();
    await harness.fixture.whenStable();
    harness.detectChanges();
  };

  const el = (): HTMLElement => harness.routeNativeElement!;

  const open = async (addresses: object[] = [address]): Promise<void> => {
    localStorage.setItem('token', 't');
    localStorage.setItem('email', 'a@b.com');

    TestBed.configureTestingModule({
      providers: [
        provideRouter([{ path: 'checkout', component: Checkout }, { path: '**', component: Blank }]),
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });

    http = TestBed.inject(HttpTestingController);
    harness = await RouterTestingHarness.create();
    component = await harness.navigateByUrl('/checkout', Checkout);

    match('GET', '/api/cart')[0].flush(cart);
    match('GET', '/api/auth/addresses')[0].flush(addresses);
    await settle();
  };

  const radio = (root: HTMLElement, label: string): HTMLInputElement =>
    Array.from(root.querySelectorAll<HTMLLabelElement>('label.method'))
      .find(item => item.textContent?.trim() === label)!
      .querySelector<HTMLInputElement>('input')!;

  const choose = async (value: string): Promise<void> => {
    radio(el(), value).click();
    await settle();
  };

  const submit = async (): Promise<void> => {
    el().querySelector<HTMLFormElement>('form')!.dispatchEvent(new Event('submit', { cancelable: true }));
    await settle();
  };

  afterEach(() => {
    http.verify();
    localStorage.clear();
    sessionStorage.clear();
  });

  it('does not create an order without a saved delivery address', async () => {

    await open([]);
    await choose('Card');
    await submit();

    expect(match('POST', '/api/orders').length).toBe(0);
    expect(el().textContent).toContain('Add a delivery address before placing your order.');
  });

  it('lets the user choose any saved address, not only the default', async () => {

    await open([address, { ...address, id: 2, fullName: 'Ravi Kumar', isDefault: false }]);

    expect(component['address']()?.fullName).toBe('Asha Rao');

    // Only the selected address is shown until the user asks to change it.
    expect(el().querySelectorAll('input[name="address"]').length).toBe(0);

    el().querySelector<HTMLButtonElement>('.address-actions button')!.click();
    await settle();

    const radios = el().querySelectorAll<HTMLInputElement>('input[name="address"]');

    expect(radios.length).toBe(2);

    radios[1].click();
    await settle();

    expect(component['address']()?.fullName).toBe('Ravi Kumar');
    expect(el().querySelectorAll('input[name="address"]').length).toBe(0);
    expect(el().textContent).toContain('Ravi Kumar');
  });

  it('requires a payment method before creating an order', async () => {

    await open();
    await submit();

    expect(match('POST', '/api/orders').length).toBe(0);
    expect(el().textContent).toContain('Choose a payment method to continue.');
  });

  it('creates the order from ids and quantities only, then pays and confirms', async () => {

    await open();
    await choose('Card');
    await submit();

    const [create] = match('POST', '/api/orders');

    // No prices or totals are sent: the server prices the order.
    expect(create.request.body).toEqual({ items: [{ productId: 5, quantity: 2 }] });

    create.flush(order());
    await settle();

    expect(sessionStorage.getItem('checkout.pendingOrderNumber')).toBe('ORD-10');

    const [pay] = match('POST', '/api/payments/orders/ORD-10');

    expect(pay.request.body).toEqual({ paymentMethod: 'Card' });

    pay.flush(payment('Completed'));
    await settle();

    match('DELETE', '/api/cart')[0].flush(null);
    await settle();

    expect(TestBed.inject(Router).url).toBe('/order-confirmation/ORD-10');
    expect(sessionStorage.getItem('checkout.pendingOrderNumber')).toBeNull();
  });

  it('ignores a second submit while the first is processing', async () => {

    await open();
    await choose('Card');

    // Same event twice, before the first request returns.
    component['placeOrder']();
    component['placeOrder']();
    await settle();

    expect(match('POST', '/api/orders').length).toBe(1);
  });

  it('shows the server error when the order cannot be created and sends no payment', async () => {

    await open();
    await choose('Card');
    await submit();

    match('POST', '/api/orders')[0].flush({ message: 'Insufficient stock for Trail Shoe. Available: 1.' }, { status: 400, statusText: 'Bad Request' });
    await settle();

    // The cart is refreshed after the failure.
    match('GET', '/api/cart')[0].flush(cart);
    await settle();

    expect(el().textContent).toContain('Insufficient stock for Trail Shoe');
    expect(match('POST', '/api/payments/orders/ORD-10').length).toBe(0);
    expect(sessionStorage.getItem('checkout.pendingOrderNumber')).toBeNull();
  });

  it('does not claim success when the payment fails, and retries on the same order', async () => {

    await open();
    await choose('Card');
    await submit();

    match('POST', '/api/orders')[0].flush(order());
    await settle();

    match('POST', '/api/payments/orders/ORD-10')[0].flush(payment('Failed'));
    await settle();

    expect(el().textContent).toContain('Payment not completed');
    expect(match('DELETE', '/api/cart').length).toBe(0);
    expect(TestBed.inject(Router).url).toBe('/checkout');

    el().querySelectorAll<HTMLButtonElement>('button').forEach(button => {
      if (button.textContent?.includes('Try payment again')) {
        button.click();
      }
    });
    await settle();

    // No second order is created.
    expect(match('POST', '/api/orders').length).toBe(0);

    match('POST', '/api/payments/orders/ORD-10')[0].flush(payment('Completed'));
    await settle();

    match('DELETE', '/api/cart')[0].flush(null);
    await settle();

    expect(TestBed.inject(Router).url).toBe('/order-confirmation/ORD-10');
  });

  it('treats a pending payment as not yet paid', async () => {

    await open();
    await choose('UPI');
    await submit();

    match('POST', '/api/orders')[0].flush(order());
    await settle();

    match('POST', '/api/payments/orders/ORD-10')[0].flush(payment('Pending'));
    await settle();

    expect(el().textContent).toContain('Payment pending');
    expect(match('DELETE', '/api/cart').length).toBe(0);
    expect(TestBed.inject(Router).url).toBe('/checkout');
  });

  it('reconciles when the payment already exists on the server', async () => {

    await open();
    await choose('Card');
    await submit();

    match('POST', '/api/orders')[0].flush(order());
    await settle();

    match('POST', '/api/payments/orders/ORD-10')[0].flush({ message: 'Payment already exists for this order.' }, { status: 400, statusText: 'Bad Request' });
    await settle();

    match('GET', '/api/payments/order/ORD-10')[0].flush(payment('Completed'));
    await settle();

    match('DELETE', '/api/cart')[0].flush(null);
    await settle();

    expect(TestBed.inject(Router).url).toBe('/order-confirmation/ORD-10');
  });

  it('cancels an unpaid order and keeps the cart', async () => {

    await open();
    await choose('Card');
    await submit();

    match('POST', '/api/orders')[0].flush(order());
    await settle();

    match('POST', '/api/payments/orders/ORD-10')[0].flush({ message: 'Gateway unavailable.' }, { status: 502, statusText: 'Bad Gateway' });
    await settle();

    expect(el().textContent).toContain('Gateway unavailable.');

    el().querySelectorAll<HTMLButtonElement>('button').forEach(button => {
      if (button.textContent?.includes('Cancel order')) {
        button.click();
      }
    });
    await settle();

    match('PUT', '/api/orders/ORD-10/cancel')[0].flush(order('ORD-10', 'Cancelled'));
    await settle();

    match('GET', '/api/cart')[0].flush(cart);
    await settle();

    expect(el().textContent).toContain('Your order was cancelled');
    expect(sessionStorage.getItem('checkout.pendingOrderNumber')).toBeNull();
  });
});

describe('Checkout resuming an unpaid order', () => {

  it('pays the existing order instead of creating a new one after a reload', async () => {

    localStorage.setItem('token', 't');
    sessionStorage.setItem('checkout.pendingOrderNumber', 'ORD-7');

    TestBed.configureTestingModule({
      providers: [
        provideRouter([{ path: 'checkout', component: Checkout }, { path: '**', component: Blank }]),
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });

    const http = TestBed.inject(HttpTestingController);
    const harness = await RouterTestingHarness.create();

    await harness.navigateByUrl('/checkout', Checkout);

    http.expectOne(req => req.method === 'GET' && req.url.endsWith('/api/cart')).flush(cart);
    http.expectOne(req => req.url.endsWith('/api/auth/addresses')).flush([address]);
    TestBed.tick();
    await harness.fixture.whenStable();

    http.expectOne(req => req.url.endsWith('/api/orders/ORD-7')).flush(order('ORD-7'));
    TestBed.tick();
    await harness.fixture.whenStable();
    harness.detectChanges();

    expect(harness.routeNativeElement!.textContent).toContain('unpaid order ORD-7');

    Array.from(harness.routeNativeElement!.querySelectorAll<HTMLLabelElement>('label.method'))
      .find(item => item.textContent?.trim() === 'Card')!
      .querySelector<HTMLInputElement>('input')!
      .click();
    harness.routeNativeElement!.querySelector<HTMLFormElement>('form')!.dispatchEvent(new Event('submit', { cancelable: true }));
    TestBed.tick();
    await harness.fixture.whenStable();

    expect(http.match(req => req.method === 'POST' && req.url.endsWith('/api/orders')).length).toBe(0);
    expect(http.match(req => req.url.endsWith('/api/payments/orders/ORD-7')).length).toBe(1);

    http.verify();
    localStorage.clear();
    sessionStorage.clear();
  });
});

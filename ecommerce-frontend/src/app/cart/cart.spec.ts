import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, TestRequest, provideHttpClientTesting } from '@angular/common/http/testing';
import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';

import { CartComponent } from './cart';

@Component({ template: '' })
class Blank { }

const cart = (quantity = 2) => ({
  id: 1,
  userId: 1,
  items: [{ productId: 5, productName: 'Trail Shoe', price: 100, quantity, totalPrice: 100 * quantity }],
  totalAmount: 100 * quantity
});

describe('CartComponent', () => {

  let http: HttpTestingController;
  let harness: RouterTestingHarness;

  const match = (method: string, suffix: string): TestRequest[] =>
    http.match(req => req.method === method && req.url.endsWith(suffix));

  const settle = async (): Promise<void> => {
    TestBed.tick();
    await harness.fixture.whenStable();
    harness.detectChanges();
  };

  const el = (): HTMLElement => harness.routeNativeElement!;

  const open = async (initial: object): Promise<void> => {
    localStorage.setItem('token', 't');

    TestBed.configureTestingModule({
      providers: [
        provideRouter([{ path: 'cart', component: CartComponent }, { path: '**', component: Blank }]),
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });

    http = TestBed.inject(HttpTestingController);
    harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/cart', CartComponent);

    match('GET', '/api/cart')[0].flush(initial);
    await settle();
    match('GET', '/api/products/5').forEach(req => req.flush({ id: 5, imageUrl: 'http://img/5.jpg' }));
    await settle();
  };

  const button = (label: string): HTMLButtonElement =>
    el().querySelector<HTMLButtonElement>(`button[aria-label="${label}"]`)!;

  afterEach(() => {
    http.verify();
    localStorage.clear();
  });

  it('shows items, server total and product thumbnails', async () => {

    await open(cart(2));

    expect(el().textContent).toContain('Trail Shoe');
    expect(el().textContent).toContain('₹200');
    expect(el().querySelector<HTMLImageElement>('.item__media img')?.src).toContain('5.jpg');
  });

  it('updates quantity through the API and shows the server total', async () => {

    await open(cart(2));

    button('Increase quantity').click();
    await settle();

    const [request] = match('PUT', '/api/cart/items/5');

    expect(request.request.body).toEqual({ quantity: 3 });

    request.flush(cart(3));
    await settle();

    expect(el().textContent).toContain('₹300');
  });

  it('ignores further quantity changes while an update is in flight', async () => {

    await open(cart(2));

    button('Increase quantity').click();
    await settle();

    button('Increase quantity').click();
    await settle();

    const requests = match('PUT', '/api/cart/items/5');

    expect(requests.length).toBe(1);

    requests[0].flush(cart(3));
  });

  it('does not allow a quantity below 1', async () => {

    await open(cart(1));

    expect(button('Decrease quantity').disabled).toBe(true);
  });

  it('removes an item and falls back to the empty state', async () => {

    await open(cart(2));

    el().querySelector<HTMLButtonElement>('button.link-btn')!.click();
    await settle();

    match('DELETE', '/api/cart/items/5')[0].flush({ ...cart(0), items: [], totalAmount: 0 });
    await settle();

    // Empty cart loads suggestions.
    http.match(req => req.url.endsWith('/api/products/search')).forEach(req =>
      req.flush({ items: [], pageNumber: 1, pageSize: 4, totalItems: 0, totalPages: 1 })
    );
    await settle();

    expect(el().textContent).toContain('Your cart is empty');
  });

  it('refreshes from the server when a removal returns no cart body', async () => {

    await open(cart(2));

    el().querySelector<HTMLButtonElement>('button.link-btn')!.click();
    await settle();

    match('DELETE', '/api/cart/items/5')[0].flush(null);
    await settle();

    match('GET', '/api/cart')[0].flush({ ...cart(0), items: [], totalAmount: 0 });
    await settle();

    http.match(req => req.url.endsWith('/api/products/search')).forEach(req =>
      req.flush({ items: [], pageNumber: 1, pageSize: 4, totalItems: 0, totalPages: 1 })
    );
    await settle();

    expect(el().textContent).toContain('Your cart is empty');
  });

  it('reports a failed update and re-syncs the cart', async () => {

    await open(cart(2));

    button('Increase quantity').click();
    await settle();

    match('PUT', '/api/cart/items/5')[0].flush({ message: 'Insufficient stock.' }, { status: 400, statusText: 'Bad Request' });
    await settle();

    // The cart is reloaded so the displayed quantity matches the server.
    match('GET', '/api/cart')[0].flush(cart(2));
    await settle();

    expect(el().textContent).toContain('₹200');
  });

  it('links to checkout', async () => {

    await open(cart(1));

    const cta = el().querySelector<HTMLAnchorElement>('a.summary__cta')!;

    expect(cta.getAttribute('href')).toBe('/checkout');
    expect(TestBed.inject(Router)).toBeTruthy();
  });
});

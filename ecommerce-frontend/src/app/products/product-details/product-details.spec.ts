import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, TestRequest, provideHttpClientTesting } from '@angular/common/http/testing';
import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';

import { ProductDetails } from './product-details';

@Component({ template: '' })
class Blank { }

const product = { id: 5, name: 'Trail Shoe', description: 'Light', price: 99, categoryId: 2, categoryName: 'Shoes', imageUrl: null };

describe('ProductDetails', () => {

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

  const open = async (loggedIn: boolean, stock?: object): Promise<void> => {

    if (loggedIn) {
      localStorage.setItem('token', 't');
    }

    TestBed.configureTestingModule({
      providers: [
        provideRouter([{ path: 'products/:id', component: ProductDetails }, { path: '**', component: Blank }]),
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });

    http = TestBed.inject(HttpTestingController);
    harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/products/5', ProductDetails);

    match('GET', '/api/products/5')[0].flush(product);
    await settle();

    if (loggedIn) {
      match('GET', '/api/inventory/5')[0].flush(stock ?? { productId: 5, productName: 'Trail Shoe', stockQuantity: 10, reorderLevel: 2, isLowStock: false });
    }

    match('GET', '/api/products/search')[0].flush({
      items: [product, { id: 6, name: 'Road Shoe', description: '', price: 120, categoryId: 2 }],
      pageNumber: 1, pageSize: 5, totalItems: 2, totalPages: 1
    });
    await settle();
  };

  afterEach(() => {
    http.verify();
    localStorage.clear();
  });

  it('shows the product, a real price and related products without the current one', async () => {

    await open(false);

    expect(el().querySelector('h1')?.textContent).toContain('Trail Shoe');
    expect(el().textContent).toContain('₹99');
    expect(el().querySelectorAll('app-product-card').length).toBe(1);
    expect(el().textContent).toContain('Road Shoe');
  });

  it('does not invent stock information for signed-out visitors', async () => {

    await open(false);

    expect(el().textContent).not.toContain('In stock');
    expect(el().textContent).not.toContain('Out of stock');
  });

  it('adds the chosen quantity to the cart', async () => {

    await open(true);

    el().querySelector<HTMLButtonElement>('button[aria-label="Increase quantity"]')!.click();
    await settle();

    el().querySelector<HTMLButtonElement>('.buy__add')!.click();
    await settle();

    const [request] = match('POST', '/api/cart/items');

    expect(request.request.body).toEqual({ productId: 5, quantity: 2 });

    request.flush({ id: 1, userId: 1, items: [], totalAmount: 0 });
  });

  it('limits quantity to the stock reported by the inventory API', async () => {

    await open(true, { productId: 5, productName: 'Trail Shoe', stockQuantity: 1, reorderLevel: 2, isLowStock: true });

    expect(el().textContent).toContain('Only 1 left');
    expect(el().querySelector<HTMLButtonElement>('button[aria-label="Increase quantity"]')!.disabled).toBe(true);
  });

  it('disables add to cart when the inventory API reports no stock', async () => {

    await open(true, { productId: 5, productName: 'Trail Shoe', stockQuantity: 0, reorderLevel: 2, isLowStock: true });

    expect(el().querySelector<HTMLButtonElement>('.buy__add')!.disabled).toBe(true);
    expect(el().textContent).toContain('Out of stock');
  });
});

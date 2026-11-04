import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';

import { EditProduct } from './edit-product';

@Component({ template: '' })
class Blank { }

const product = (id: number, name: string) =>
  ({ id, name, description: 'A description', price: 10, categoryId: 1, imageUrl: null, stockQuantity: 0, specifications: [] });

describe('EditProduct', () => {

  let http: HttpTestingController;
  let harness: RouterTestingHarness;

  const settle = async (): Promise<void> => {
    TestBed.tick();
    await harness.fixture.whenStable();
    harness.detectChanges();
  };

  const loadProduct = async (id: number, name: string, stock: number | null): Promise<void> => {

    http.expectOne(req => req.method === 'GET' && req.url.endsWith(`/api/products/${id}`)).flush(product(id, name));

    const inventory = http.expectOne(req => req.method === 'GET' && req.url.endsWith(`/api/inventory/${id}`));

    if (stock === null) {
      inventory.flush({}, { status: 404, statusText: 'Not Found' });
    } else {
      inventory.flush({ productId: id, productName: name, stockQuantity: stock, reorderLevel: 3, isLowStock: false });
    }

    await settle();
  };

  const value = (selector: string): string => (harness.routeNativeElement!.querySelector(selector) as HTMLInputElement).value;

  beforeEach(async () => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([{ path: 'admin/products/edit/:id', component: EditProduct }, { path: '**', component: Blank }]),
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });

    http = TestBed.inject(HttpTestingController);
    harness = await RouterTestingHarness.create();

    await harness.navigateByUrl('/admin/products/edit/3002', EditProduct);

    http.expectOne(req => req.url.endsWith('/api/categories')).flush([{ id: 1, name: 'Shoes', productCount: 1 }]);
    await loadProduct(3002, 'First', 4);
  });

  afterEach(() => http.verify());

  it('shows an editable stock field', async () => {

    const stock = harness.routeNativeElement!.querySelector<HTMLInputElement>('#stock')!;

    expect(stock.disabled).toBe(false);
    expect(stock.value).toBe('4');
  });

  it('loads the newly opened product when the id in the url changes', async () => {

    expect(value('#name')).toBe('First');

    await harness.navigateByUrl('/admin/products/edit/3005', EditProduct);
    await loadProduct(3005, 'Second', 9);

    expect(value('#name')).toBe('Second');
    expect(value('#stock')).toBe('9');
  });
});

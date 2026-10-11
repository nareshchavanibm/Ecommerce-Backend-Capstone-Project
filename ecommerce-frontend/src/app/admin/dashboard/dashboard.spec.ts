import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { AdminDashboard } from './dashboard';

describe('AdminDashboard', () => {

  let http: HttpTestingController;

  const flush = (suffix: string, body: object, status = 200): void => {
    http.expectOne(req => req.method === 'GET' && req.url.endsWith(suffix))
      .flush(body, { status, statusText: status === 200 ? 'OK' : 'Error' });
  };

  const render = async (overrides: { inventoryStatus?: number } = {}) => {

    TestBed.configureTestingModule({ providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);

    const fixture = TestBed.createComponent(AdminDashboard);

    fixture.detectChanges();

    const today = new Date().toISOString();

    flush('/api/orders', [
      { orderNumber: 'ORD-11', userId: 4, orderDate: today, totalAmount: 1200, status: 'Delivered',
        items: [{ productId: 1, productName: 'Trail Shoe', price: 600, quantity: 2 }] },
      { orderNumber: 'ORD-12', userId: 5, orderDate: today, totalAmount: 300, status: 'Pending',
        items: [{ productId: 2, productName: 'Road Shoe', price: 300, quantity: 1 }] },
      { orderNumber: 'ORD-13', userId: 6, orderDate: today, totalAmount: 5000, status: 'Cancelled', items: [] }
    ]);
    flush('/api/categories', [{ id: 1, name: 'Shoes', productCount: 3 }, { id: 2, name: 'Bags', productCount: 1 }]);
    flush('/api/products/search', {
      items: [{ id: 1, name: 'Trail Shoe', description: '', price: 650, categoryId: 1, stockQuantity: 0 }],
      totalItems: 4, totalCount: 4, pageNumber: 1, pageSize: 100, totalPages: 1
    });

    if (overrides.inventoryStatus) {
      flush('/api/inventory', { message: 'nope' }, overrides.inventoryStatus);
    } else {
      flush('/api/inventory', [{ productId: 2, productName: 'Road Shoe', stockQuantity: 1, reorderLevel: 5, isLowStock: true }]);
    }

    fixture.detectChanges();

    return fixture.nativeElement as HTMLElement;
  };

  afterEach(() => http.verify());

  it('shows figures computed from real orders, excluding cancelled ones', async () => {

    const el = await render();
    const text = el.textContent!;

    expect(text).toContain('Total products');
    expect(text).toContain('4');
    expect(text).toContain('₹1,500');
    expect(text).not.toContain('₹6,500');
    expect(text).toContain('Pending orders');
  });

  it('lists recent orders, a low stock alert, top products and categories', async () => {

    const el = await render();
    const text = el.textContent!;

    expect(text).toContain('Order ORD-11');
    expect(text).toContain('Low stock alert');
    expect(text).toContain('Road Shoe - 1 left');
    expect(text).toContain('Trail Shoe');
    expect(text).toContain('Shoes');
    expect(text).toContain('75%');
  });

  it('keeps working and says so when one data source fails', async () => {

    const el = await render({ inventoryStatus: 500 });

    expect(el.textContent).toContain('Some data could not be loaded (inventory)');
    expect(el.textContent).toContain('Order ORD-11');
  });
});

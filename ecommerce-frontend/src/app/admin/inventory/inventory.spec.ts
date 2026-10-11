import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import { InventoryComponent } from './inventory';

describe('InventoryComponent', () => {

  let http: HttpTestingController;

  // The test DOM has no modal dialog support.
  beforeAll(() => {
    HTMLDialogElement.prototype.showModal ??= function (this: HTMLDialogElement) { this.setAttribute('open', ''); };
    HTMLDialogElement.prototype.close ??= function (this: HTMLDialogElement) { this.removeAttribute('open'); };
  });
  const render = () => {

    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);

    const fixture = TestBed.createComponent(InventoryComponent);

    fixture.detectChanges();

    http.expectOne(req => req.method === 'GET' && req.url.endsWith('/api/products')).flush([
      { id: 1, name: 'Trail Shoe', description: '', price: 10, categoryId: 1 },
      { id: 2, name: 'Road Shoe', description: '', price: 10, categoryId: 1 },
      { id: 3, name: 'Hiking Boot', description: '', price: 10, categoryId: 1 }
    ]);
    http.expectOne(req => req.method === 'GET' && req.url.endsWith('/api/inventory')).flush([
      { productId: 1, productName: 'Trail Shoe', stockQuantity: 20, reorderLevel: 5, isLowStock: false },
      { productId: 2, productName: 'Road Shoe', stockQuantity: 2, reorderLevel: 5, isLowStock: true }
    ]);

    fixture.detectChanges();

    return fixture.nativeElement as HTMLElement;
  };

  afterEach(() => http.verify());

  it('lists every product, including those without a stock record, with one inventory request', () => {

    const text = render().textContent!;

    expect(text).toContain('Trail Shoe');
    expect(text).toContain('Hiking Boot');
    expect(text).toContain('In stock');
    expect(text).toContain('Low stock');
    expect(text).toContain('No stock record');
    expect(text).toContain('Set stock');
  });

  it('creates a record for a product that has none, and updates an existing one', () => {

    const el = render();
    const buttons = Array.from(el.querySelectorAll('tbody button')) as HTMLButtonElement[];

    // Third row has no record: opening it and saving must POST.
    buttons[2].click();
    TestBed.tick();

    const form = el.querySelector<HTMLFormElement>('#stock-form')!;

    form.dispatchEvent(new Event('submit'));
    TestBed.tick();

    const create = http.expectOne(req => req.method === 'POST' && req.url.endsWith('/api/inventory/3'));

    expect(create.request.body).toEqual({ quantity: 0, reorderLevel: 5 });

    create.flush({ productId: 3, productName: 'Hiking Boot', stockQuantity: 0, reorderLevel: 5, isLowStock: true });
  });
});

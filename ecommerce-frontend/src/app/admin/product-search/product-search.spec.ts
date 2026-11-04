import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';

import { AdminProductSearch } from './product-search';

describe('AdminProductSearch', () => {

  let http: HttpTestingController;
  let el: HTMLElement;

  const type = async (value: string): Promise<void> => {

    const input = el.querySelector<HTMLInputElement>('input')!;

    input.focus();
    input.value = value;
    input.dispatchEvent(new Event('input'));

    await new Promise(resolve => setTimeout(resolve, 400));
  };

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);

    const fixture = TestBed.createComponent(AdminProductSearch);

    fixture.detectChanges();
    el = fixture.nativeElement;
  });

  afterEach(() => http.verify());

  it('finds a product by id and by text, showing the id match first', async () => {

    await type('7');

    const byText = http.expectOne(req => req.url.endsWith('/api/products/search') && req.params.get('search') === '7');

    http.expectOne(req => req.url.endsWith('/api/products/lookup') && req.params.get('id') === '7')
      .flush([{ id: 7, name: 'Spec Phone', description: '', price: 100, categoryId: 1, stockQuantity: 3 }]);
    byText.flush({
      items: [{ id: 9, name: 'Phone Case', description: '', price: 10, categoryId: 1, stockQuantity: 0 }],
      totalItems: 1, totalCount: 1, pageNumber: 1, pageSize: 8, totalPages: 1
    });

    TestBed.tick();

    const names = Array.from(el.querySelectorAll('.search__name')).map(link => link.textContent!.trim());

    expect(names).toEqual(['Spec Phone', 'Phone Case']);
    expect(el.textContent).toContain('ID 7');
    expect(el.textContent).toContain('Out of stock');
  });

  it('only searches the text when the term is not a number', async () => {

    await type('snapdragon');

    http.expectNone(req => req.url.endsWith('/api/products/lookup'));
    http.expectOne(req => req.url.endsWith('/api/products/search') && req.params.get('search') === 'snapdragon')
      .flush({ items: [], totalItems: 0, totalCount: 0, pageNumber: 1, pageSize: 8, totalPages: 0 });

    TestBed.tick();

    expect(el.textContent).toContain('No products found.');
  });

  it('closes the results on Escape', async () => {

    await type('shoe');

    http.expectOne(req => req.url.endsWith('/api/products/search'))
      .flush({ items: [], totalItems: 0, totalCount: 0, pageNumber: 1, pageSize: 8, totalPages: 0 });

    TestBed.tick();
    expect(el.querySelector('.search__panel')).not.toBeNull();

    el.querySelector('.search')!.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
    TestBed.tick();

    expect(el.querySelector('.search__panel')).toBeNull();
  });
});

import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, TestRequest, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';

import { ProductList } from './product-list';

const page = (items: object[], extra: object = {}) => ({
  items,
  pageNumber: 1,
  pageSize: 12,
  totalItems: items.length,
  totalPages: 1,
  ...extra
});

const product = { id: 1, name: 'Trail Shoe', description: 'Light', price: 99, categoryId: 1, categoryName: 'Shoes' };

describe('ProductList', () => {

  let http: HttpTestingController;
  let harness: RouterTestingHarness;

  const searches = (): TestRequest[] => http.match(req => req.url.endsWith('/api/products/search'));

  const flushCategories = (): void => {
    http.match(req => req.url.endsWith('/api/categories')).forEach(req =>
      req.flush([{ id: 1, name: 'Shoes', productCount: 3 }])
    );
  };

  const settle = async (): Promise<void> => {
    TestBed.tick();
    await harness.fixture.whenStable();
    harness.detectChanges();
  };

  const open = async (url: string): Promise<void> => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([{ path: 'products', component: ProductList }]),
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });

    http = TestBed.inject(HttpTestingController);
    harness = await RouterTestingHarness.create();
    await harness.navigateByUrl(url, ProductList);
    await settle();
  };

  afterEach(() => http.verify());

  it('sends URL filters and page to the API', async () => {

    await open('/products?category=Shoes&minPrice=10&maxPrice=200&sortBy=price&sortOrder=desc&page=2&search=trail');

    const [request] = searches();

    expect(request.request.params.get('category')).toBe('Shoes');
    expect(request.request.params.get('minPrice')).toBe('10');
    expect(request.request.params.get('maxPrice')).toBe('200');
    expect(request.request.params.has('sortBy')).toBe(false);
    expect(request.request.params.get('search')).toBe('trail');
    expect(request.request.params.get('pageNumber')).toBe('2');
    expect(request.request.params.get('pageSize')).toBe('12');

    request.flush(page([], { pageNumber: 2, totalItems: 13, totalPages: 2 }));
    flushCategories();
  });

  it('renders results with the server total and a category heading', async () => {

    await open('/products?category=Shoes');

    searches()[0].flush(page([product], { totalItems: 30, totalPages: 3 }));
    flushCategories();
    await settle();

    const element: HTMLElement = harness.routeNativeElement!;

    expect(element.querySelector('h1')?.textContent).toContain('Shoes');
    expect(element.textContent).toContain('30 products');
    expect(element.querySelectorAll('app-product-card').length).toBe(1);
  });

  it('removing a filter chip updates the URL and requests new results', async () => {

    await open('/products?category=Shoes');

    searches()[0].flush(page([product]));
    flushCategories();
    await settle();

    const chip = harness.routeNativeElement!.querySelector<HTMLButtonElement>('.chip')!;

    chip.click();
    await settle();

    expect(TestBed.inject(Router).url).toBe('/products');

    const [next] = searches();

    expect(next.request.params.has('category')).toBe(false);

    next.flush(page([product]));
  });

  it('has no search box of its own; search comes from the header', async () => {

    await open('/products?search=trail');

    searches()[0].flush(page([]));
    flushCategories();
    await settle();

    const element = harness.routeNativeElement!;

    expect(element.querySelector('input[type="search"]')).toBeNull();
    expect(element.textContent).toContain('Search: trail');
  });

  it('shows an error with a working retry', async () => {

    await open('/products');

    searches()[0].flush({ message: 'Boom' }, { status: 500, statusText: 'Server Error' });
    flushCategories();
    await settle();

    const element: HTMLElement = harness.routeNativeElement!;

    expect(element.textContent).toContain('Boom');

    const retry = Array.from(element.querySelectorAll('button')).find(b => b.textContent?.includes('Try again'))!;

    retry.click();
    await settle();

    const [again] = searches();

    expect(again).toBeTruthy();
    again.flush(page([product]));
  });
});

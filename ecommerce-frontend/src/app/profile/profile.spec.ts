import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, TestRequest, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';

import { Profile } from './profile';

describe('Profile', () => {

  let http: HttpTestingController;
  let harness: RouterTestingHarness;

  const match = (method: string, suffix: string): TestRequest[] =>
    http.match(req => req.method === method && req.url.endsWith(suffix));

  const settle = async (): Promise<void> => {
    TestBed.tick();
    await harness.fixture.whenStable();
    harness.detectChanges();
  };

  const open = async (addresses: object[]): Promise<void> => {

    localStorage.setItem('token', 't');
    localStorage.setItem('role', 'Customer');
    localStorage.setItem('email', 'jane@example.com');

    TestBed.configureTestingModule({
      providers: [provideRouter([{ path: 'profile', component: Profile }]), provideHttpClient(), provideHttpClientTesting()]
    });

    http = TestBed.inject(HttpTestingController);
    harness = await RouterTestingHarness.create();
    await harness.navigateByUrl('/profile', Profile);

    match('GET', '/api/auth/profile')[0].flush({ userId: 1, firstName: 'Jane', lastName: 'Doe', email: 'jane@example.com', role: 'Customer' });
    match('GET', '/api/auth/addresses')[0].flush(addresses);
    await settle();
  };

  afterEach(() => {
    http.verify();
    localStorage.clear();
  });

  it('shows the name, the update button and an add-address button', async () => {

    await open([]);

    const text = harness.routeNativeElement!.textContent!;

    expect(text).toContain('Jane Doe');
    expect(text).toContain('Update details');
    expect(text).toContain('Add address');
    expect(text).toContain('You have not saved a delivery address yet');
  });

  it('lists saved addresses with a default badge', async () => {

    await open([{
      id: 7, fullName: 'Jane Doe', phone: '9999999999', addressLine1: '12 Park Street', addressLine2: null,
      city: 'Pune', state: 'MH', postalCode: '411001', country: 'India', isDefault: true
    }]);

    const text = harness.routeNativeElement!.textContent!;

    expect(text).toContain('12 Park Street');
    expect(text).toContain('Default');
  });

  it('opens the details form from Update details', async () => {

    await open([]);

    const button = Array.from(harness.routeNativeElement!.querySelectorAll('button'))
      .find(b => b.textContent?.includes('Update details'))!;

    button.click();
    await settle();

    const first = harness.routeNativeElement!.querySelector<HTMLInputElement>('input[autocomplete="given-name"]')!;

    expect(first.value).toBe('Jane');
  });
});

import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';

import { AuthService } from '../../auth/auth';
import { sessionExpiredInterceptor } from './session-expired-interceptor';

function tokenExpiringIn(seconds: number): string {
  const payload = btoa(JSON.stringify({ exp: Math.floor(Date.now() / 1000) + seconds }));

  return `header.${payload}.signature`;
}

describe('session expiry', () => {

  let http: HttpClient;
  let backend: HttpTestingController;
  let router: Router;
  let auth: AuthService;

  beforeEach(() => {
    localStorage.clear();

    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideHttpClient(withInterceptors([sessionExpiredInterceptor])),
        provideHttpClientTesting()
      ]
    });

    http = TestBed.inject(HttpClient);
    backend = TestBed.inject(HttpTestingController);
    router = TestBed.inject(Router);
    auth = TestBed.inject(AuthService);

    vi.spyOn(router, 'navigateByUrl').mockResolvedValue(true);
  });

  afterEach(() => localStorage.clear());

  it('logs out and redirects to login with a message when the API returns 401 for a signed-in user', () => {
    localStorage.setItem('token', tokenExpiringIn(3600));
    localStorage.setItem('role', 'Customer');

    http.get('/api/orders/my-orders').subscribe({ error: () => undefined });
    backend.expectOne('/api/orders/my-orders').flush({}, { status: 401, statusText: 'Unauthorized' });

    expect(localStorage.getItem('token')).toBeNull();
    expect(auth.isLoggedIn()).toBe(false);

    const tree = (router.navigateByUrl as unknown as { mock: { calls: unknown[][] } }).mock.calls[0][0];

    expect(router.serializeUrl(tree as never)).toContain('/login?expired=1');
  });

  it('does not treat a failed login as an expired session', () => {
    localStorage.setItem('token', tokenExpiringIn(3600));

    http.post('/api/auth/login', {}).subscribe({ error: () => undefined });
    backend.expectOne('/api/auth/login').flush({}, { status: 401, statusText: 'Unauthorized' });

    expect(router.navigateByUrl).not.toHaveBeenCalled();
    expect(localStorage.getItem('token')).not.toBeNull();
  });

  it('ignores 401 when nobody is signed in', () => {
    http.get('/api/orders/my-orders').subscribe({ error: () => undefined });
    backend.expectOne('/api/orders/my-orders').flush({}, { status: 401, statusText: 'Unauthorized' });

    expect(router.navigateByUrl).not.toHaveBeenCalled();
  });

  it('detects an expired token from its exp claim', () => {
    localStorage.setItem('token', tokenExpiringIn(-60));
    expect(auth.isTokenExpired()).toBe(true);

    localStorage.setItem('token', tokenExpiringIn(3600));
    expect(auth.isTokenExpired()).toBe(false);
  });

  it('keeps the page to return to after re-login', () => {
    const tree = auth.expireSession('/checkout');

    expect(router.serializeUrl(tree)).toBe('/login?expired=1&returnUrl=%2Fcheckout');
  });

  describe('with a refresh token', () => {

    const isRefresh = (req: { url: string }) => req.url.endsWith('/api/auth/refresh');

    const renewed = { token: 'new-access', refreshToken: 'r2', userId: 1, email: 'a@b.c', role: 'Customer' };

    beforeEach(() => {
      localStorage.setItem('token', tokenExpiringIn(3600));
      localStorage.setItem('refreshToken', 'r1');
    });

    it('renews the session and repeats the request with the new token', () => {
      let result: unknown;

      http.get('/api/orders/my-orders').subscribe(value => result = value);
      backend.expectOne('/api/orders/my-orders').flush({}, { status: 401, statusText: 'Unauthorized' });

      const refresh = backend.expectOne(isRefresh);

      expect(refresh.request.body).toEqual({ refreshToken: 'r1' });
      refresh.flush(renewed);

      const retry = backend.expectOne('/api/orders/my-orders');

      expect(retry.request.headers.get('Authorization')).toBe('Bearer new-access');
      retry.flush({ ok: true });

      expect(result).toEqual({ ok: true });
      expect(localStorage.getItem('token')).toBe('new-access');
      expect(localStorage.getItem('refreshToken')).toBe('r2');
      expect(router.navigateByUrl).not.toHaveBeenCalled();
    });

    it('sends several failed requests through a single refresh', () => {
      http.get('/api/a').subscribe();
      http.get('/api/b').subscribe();

      backend.expectOne('/api/a').flush({}, { status: 401, statusText: 'Unauthorized' });
      backend.expectOne('/api/b').flush({}, { status: 401, statusText: 'Unauthorized' });

      const refreshes = backend.match(isRefresh);

      expect(refreshes.length).toBe(1);
      refreshes[0].flush(renewed);

      backend.expectOne('/api/a').flush({});
      backend.expectOne('/api/b').flush({});
    });

    it('ends the session when the refresh token is rejected', () => {
      let failure: unknown;

      http.get('/api/orders/my-orders').subscribe({ error: error => failure = error });
      backend.expectOne('/api/orders/my-orders').flush({}, { status: 401, statusText: 'Unauthorized' });
      backend.expectOne(isRefresh).flush({}, { status: 401, statusText: 'Unauthorized' });

      expect((failure as { status: number }).status).toBe(401);
      expect(localStorage.getItem('token')).toBeNull();
      expect(localStorage.getItem('refreshToken')).toBeNull();
      expect(router.navigateByUrl).toHaveBeenCalled();
    });

    it('keeps the session when the refresh fails for a server reason', () => {
      http.get('/api/orders/my-orders').subscribe({ error: () => undefined });
      backend.expectOne('/api/orders/my-orders').flush({}, { status: 401, statusText: 'Unauthorized' });
      backend.expectOne(isRefresh).flush({}, { status: 503, statusText: 'Unavailable' });

      expect(localStorage.getItem('token')).not.toBeNull();
      expect(router.navigateByUrl).not.toHaveBeenCalled();
    });

    it('ends the session when the repeated request is still refused', () => {
      http.get('/api/orders/my-orders').subscribe({ error: () => undefined });
      backend.expectOne('/api/orders/my-orders').flush({}, { status: 401, statusText: 'Unauthorized' });
      backend.expectOne(isRefresh).flush(renewed);
      backend.expectOne('/api/orders/my-orders').flush({}, { status: 401, statusText: 'Unauthorized' });

      expect(router.navigateByUrl).toHaveBeenCalled();
    });

    it('does not try to refresh when the refresh call itself returns 401', () => {
      http.post('/api/auth/refresh', {}).subscribe({ error: () => undefined });
      backend.expectOne(isRefresh).flush({}, { status: 401, statusText: 'Unauthorized' });

      expect(router.navigateByUrl).not.toHaveBeenCalled();
    });

    it('revokes the refresh token on the server when logging out', () => {
      auth.logout();

      const logout = backend.expectOne(req => req.url.endsWith('/api/auth/logout'));

      expect(logout.request.body).toEqual({ refreshToken: 'r1' });
      expect(localStorage.getItem('refreshToken')).toBeNull();
    });

    it('renews an expired access token for a route guard without logging out', () => {
      localStorage.setItem('token', tokenExpiringIn(-60));

      let outcome: unknown;

      (auth.renewIfExpired('/cart') as import('rxjs').Observable<unknown>).subscribe(value => outcome = value);
      backend.expectOne(isRefresh).flush(renewed);

      expect(outcome).toBe(true);
      expect(auth.isTokenExpired()).toBe(false);
    });

    it('treats an expired access token without a refresh token as an ended session', () => {
      localStorage.removeItem('refreshToken');
      localStorage.setItem('token', tokenExpiringIn(-60));

      const outcome = auth.renewIfExpired('/cart');

      expect(router.serializeUrl(outcome as never)).toBe('/login?expired=1&returnUrl=%2Fcart');
    });
  });
});

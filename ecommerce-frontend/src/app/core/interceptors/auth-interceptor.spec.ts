import { TestBed } from '@angular/core/testing';
import { HttpClient, HttpInterceptorFn, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { authInterceptor } from './auth-interceptor';

function tokenExpiringIn(seconds: number): string {
  const payload = btoa(JSON.stringify({ exp: Math.floor(Date.now() / 1000) + seconds }));

  return `header.${payload}.signature`;
}

describe('authInterceptor', () => {
  const interceptor: HttpInterceptorFn = (req, next) =>
    TestBed.runInInjectionContext(() => authInterceptor(req, next));

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({});
  });

  afterEach(() => localStorage.clear());

  it('should be created', () => {
    expect(interceptor).toBeTruthy();
  });

  describe('over HTTP', () => {
    let http: HttpClient;
    let backend: HttpTestingController;

    beforeEach(() => {
      TestBed.resetTestingModule();
      TestBed.configureTestingModule({
        providers: [
          provideRouter([]),
          provideHttpClient(withInterceptors([authInterceptor])),
          provideHttpClientTesting()
        ]
      });

      http = TestBed.inject(HttpClient);
      backend = TestBed.inject(HttpTestingController);
    });

    it('sends a valid token as it is', () => {
      const token = tokenExpiringIn(3600);
      localStorage.setItem('token', token);
      localStorage.setItem('refreshToken', 'r1');

      http.get('/api/chat/config').subscribe();

      expect(backend.expectOne('/api/chat/config').request.headers.get('Authorization')).toBe(`Bearer ${token}`);
    });

    it('renews an expired token before the request goes out', () => {
      localStorage.setItem('token', tokenExpiringIn(-60));
      localStorage.setItem('refreshToken', 'r1');

      http.post('/api/chat', {}).subscribe();

      backend.expectOne(req => req.url.endsWith('/api/auth/refresh'))
        .flush({ token: 'fresh', refreshToken: 'r2', userId: 1, email: 'a@b.c', role: 'Customer' });

      const chat = backend.expectOne('/api/chat');

      expect(chat.request.headers.get('Authorization')).toBe('Bearer fresh');
      expect(localStorage.getItem('refreshToken')).toBe('r2');
    });

    it('still sends the request when the renewal fails', () => {
      const stale = tokenExpiringIn(-60);
      localStorage.setItem('token', stale);
      localStorage.setItem('refreshToken', 'r1');

      http.post('/api/chat', {}).subscribe({ error: () => undefined });

      backend.expectOne(req => req.url.endsWith('/api/auth/refresh'))
        .flush({}, { status: 503, statusText: 'Unavailable' });

      expect(backend.expectOne('/api/chat').request.headers.get('Authorization')).toBe(`Bearer ${stale}`);
    });

    it('does not renew for the sign-in and refresh calls themselves', () => {
      localStorage.setItem('token', tokenExpiringIn(-60));
      localStorage.setItem('refreshToken', 'r1');

      http.post('/api/auth/login', {}).subscribe();

      backend.expectOne('/api/auth/login');
      backend.expectNone(req => req.url.endsWith('/api/auth/refresh'));
    });

    it('leaves anonymous requests alone', () => {
      http.get('/api/products').subscribe();

      expect(backend.expectOne('/api/products').request.headers.has('Authorization')).toBe(false);
    });
  });
});

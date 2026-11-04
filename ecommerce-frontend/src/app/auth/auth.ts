import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router, UrlTree } from '@angular/router';
import { Observable, catchError, finalize, map, of, shareReplay, tap, throwError } from 'rxjs';
import { environment } from '../../environments/environment';

interface LoginRequest {
  email: string;
  password: string;
}

export interface LoginResponse {
  userId: number;
  email: string;
  role: string;
  token: string;
  refreshToken?: string;
}

export interface SignupRequest {
  firstName: string;
  lastName: string;
  email: string;
  password: string;
}

@Injectable({
  providedIn: 'root'
})
export class AuthService {

  private apiUrl = `${environment.apiUrl}/api/auth`;

  private readonly router = inject(Router);

  // One refresh at a time; parallel 401s share its result.
  private refreshing$?: Observable<string>;

  // Used by Navbar to know whether user is logged in
  isLoggedIn = signal<boolean>(this.hasToken());

  constructor(private http: HttpClient) { }

  login(
    email: string,
    password: string
  ): Observable<LoginResponse> {

    const request: LoginRequest = {
      email,
      password
    };

    return this.http
      .post<LoginResponse>(
        `${this.apiUrl}/login`,
        request
      )
      .pipe(
        tap(response => {
          this.storeSession(response);
        })
      );
  }

  hasRefreshToken(): boolean {
    return typeof localStorage !== 'undefined' && !!localStorage.getItem('refreshToken');
  }

  // Trades the refresh token for a new access token (the refresh token rotates on every use).
  refresh(): Observable<string> {
    const sent = localStorage.getItem('refreshToken');

    if (!sent) {
      return throwError(() => new Error('No refresh token.'));
    }

    if (!this.refreshing$) {
      this.refreshing$ = this.http
        .post<LoginResponse>(`${this.apiUrl}/refresh`, { refreshToken: sent })
        .pipe(
          tap(response => this.storeSession(response)),
          map(response => response.token),
          catchError(error => {
            // Another tab may have rotated the token a moment ago.
            const current = localStorage.getItem('token');

            if (localStorage.getItem('refreshToken') !== sent && current) {
              return of(current);
            }

            return throwError(() => error);
          }),
          finalize(() => this.refreshing$ = undefined),
          shareReplay({ bufferSize: 1, refCount: false })
        );
    }

    return this.refreshing$;
  }

  // Renews an expired access token from the refresh token, or ends the session when that is not possible.
  renewIfExpired(returnUrl: string): true | UrlTree | Observable<true | UrlTree> {
    if (!this.isTokenExpired()) {
      return true;
    }

    if (!this.hasRefreshToken()) {
      return this.expireSession(returnUrl);
    }

    return this.refresh().pipe(
      map(() => true as const),
      catchError(() => of(this.expireSession(returnUrl)))
    );
  }

  private storeSession(response: LoginResponse): void {
    localStorage.setItem('token', response.token);
    localStorage.setItem('userId', response.userId.toString());
    localStorage.setItem('email', response.email);
    localStorage.setItem('role', response.role);

    if (response.refreshToken) {
      localStorage.setItem('refreshToken', response.refreshToken);
    }

    this.isLoggedIn.set(true);
  }

  signup(
    request: SignupRequest
  ): Observable<any> {

    return this.http.post(
      `${this.apiUrl}/register`,
      request
    );
  }

  forgotPassword(email: string): Observable<unknown> {
    return this.http.post(`${this.apiUrl}/forgot-password`, { email });
  }

  resendOtp(email: string): Observable<unknown> {
    return this.http.post(`${this.apiUrl}/resend-otp`, { email });
  }

  verifyOtp(email: string, code: string): Observable<unknown> {
    return this.http.post(`${this.apiUrl}/verify-otp`, { email, code });
  }

  resetPassword(email: string, code: string, newPassword: string): Observable<unknown> {
    return this.http.post(`${this.apiUrl}/reset-password`, { email, code, newPassword });
  }

  getToken(): string | null {
    return localStorage.getItem('token');
  }

  getRole(): string | null {
    return localStorage.getItem('role');
  }

  getEmail(): string | null {
    return localStorage.getItem('email');
  }

  // True when a stored token has passed its exp claim (30s early to avoid racing the server clock).
  isTokenExpired(): boolean {
    const token = typeof localStorage === 'undefined' ? null : localStorage.getItem('token');

    if (!token) {
      return false;
    }

    try {
      const payload = JSON.parse(atob(token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/')));

      return typeof payload.exp === 'number' && payload.exp * 1000 <= Date.now() + 30_000;
    } catch {
      // A token that cannot be read is left for the server to judge (a 401 ends the session).
      return false;
    }
  }

  // Clears the session and returns the login page to go to, with a message and the page to come back to.
  expireSession(returnUrl?: string): UrlTree {
    this.logout();

    const back = returnUrl && returnUrl.startsWith('/') && !returnUrl.startsWith('/login') ? returnUrl : undefined;

    return this.router.createUrlTree(['/login'], {
      queryParams: { expired: 1, returnUrl: back }
    });
  }

  logout(): void {

    const refreshToken = localStorage.getItem('refreshToken');

    localStorage.removeItem('token');
    localStorage.removeItem('refreshToken');
    localStorage.removeItem('userId');
    localStorage.removeItem('email');
    localStorage.removeItem('role');

    this.isLoggedIn.set(false);

    // Best effort: the server retires the token so a copy of it stops working.
    if (refreshToken) {
      this.http.post(`${this.apiUrl}/logout`, { refreshToken }).subscribe({ error: () => undefined });
    }
  }

  private hasToken(): boolean {
    return typeof localStorage !== 'undefined'
      && !!localStorage.getItem('token')
      && (!this.isTokenExpired() || this.hasRefreshToken());
  }
}

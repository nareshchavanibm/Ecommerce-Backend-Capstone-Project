import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { PLATFORM_ID, inject } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { Router } from '@angular/router';
import { catchError, switchMap, throwError } from 'rxjs';

import { AuthService } from '../../auth/auth';

// Sign-in and password flows return 401 for wrong credentials, not for an expired session.
const PUBLIC_AUTH_CALLS = /\/api\/auth\/(login|register|refresh|logout|forgot-password|resend-otp|verify-otp|reset-password)/;

// A 401 first tries to renew the session with the refresh token; only when that fails is the user sent to login.
export const sessionExpiredInterceptor: HttpInterceptorFn = (req, next) => {

  const auth = inject(AuthService);
  const router = inject(Router);
  const isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  const endSession = () => router.navigateByUrl(auth.expireSession(router.url));

  return next(req).pipe(
    catchError((error: unknown) => {

      if (
        !isBrowser ||
        !(error instanceof HttpErrorResponse) ||
        error.status !== 401 ||
        PUBLIC_AUTH_CALLS.test(req.url) ||
        !auth.getToken()
      ) {
        return throwError(() => error);
      }

      if (!auth.hasRefreshToken()) {
        endSession();

        return throwError(() => error);
      }

      return auth.refresh().pipe(
        catchError(refreshError => {
          // A network or server fault is not a verdict on the session.
          if (refreshError instanceof HttpErrorResponse && (refreshError.status === 401 || refreshError.status === 400)) {
            endSession();
          }

          return throwError(() => error);
        }),
        switchMap(token =>
          next(req.clone({ setHeaders: { Authorization: `Bearer ${token}` } })).pipe(
            catchError(retryError => {
              if (retryError instanceof HttpErrorResponse && retryError.status === 401) {
                endSession();
              }

              return throwError(() => retryError);
            })
          )
        )
      );
    })
  );
};

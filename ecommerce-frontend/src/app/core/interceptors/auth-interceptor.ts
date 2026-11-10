import {
  inject,
  PLATFORM_ID
} from '@angular/core';

import {
  isPlatformBrowser
} from '@angular/common';

import {
  HttpInterceptorFn
} from '@angular/common/http';

import { catchError, of, switchMap } from 'rxjs';

import { AuthService } from '../../auth/auth';
import { environment } from '../../../environments/environment';

const AUTH_CALLS = /\/api\/auth\/(login|register|refresh|logout)/;

// Relative URLs and the API origin only; the token must never reach a third-party host.
const isApiRequest = (url: string) =>
  !/^([a-z][a-z\d+.-]*:)?\/\//i.test(url) || url.startsWith(environment.apiUrl);

export const authInterceptor: HttpInterceptorFn = (req, next) => {

  const platformId = inject(PLATFORM_ID);

  // localStorage is available only in the browser
  if (isPlatformBrowser(platformId) && isApiRequest(req.url)) {

    const token = localStorage.getItem('token');

    // If user is logged in, attach JWT
    if (token) {

      const send = (jwt: string) => next(req.clone({
        setHeaders: {
          Authorization: `Bearer ${jwt}`
        }
      }));

      const auth = inject(AuthService);

      // Renew first: endpoints open to guests (like the chat) would otherwise treat an expired token as signed out.
      if (auth.isTokenExpired() && auth.hasRefreshToken() && !AUTH_CALLS.test(req.url)) {
        return auth.refresh().pipe(
          catchError(() => of(token)),
          switchMap(send)
        );
      }

      return send(token);
    }
  }

  // Anonymous request
  return next(req);
};

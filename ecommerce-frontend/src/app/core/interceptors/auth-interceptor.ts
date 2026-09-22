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

export const authInterceptor: HttpInterceptorFn = (req, next) => {

  const platformId = inject(PLATFORM_ID);

  // localStorage is available only in the browser
  if (isPlatformBrowser(platformId)) {

    const token = localStorage.getItem('token');

    // If user is logged in, attach JWT
    if (token) {

      const authReq = req.clone({
        setHeaders: {
          Authorization: `Bearer ${token}`
        }
      });

      return next(authReq);
    }
  }

  // Anonymous request
  return next(req);
};

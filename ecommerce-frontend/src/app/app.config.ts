import { ApplicationConfig } from '@angular/core';

import {
  provideRouter
} from '@angular/router';

import {
  provideHttpClient,
  withInterceptors
} from '@angular/common/http';

import { routes } from './app.routes';

import {
  authInterceptor
} from './core/interceptors/auth-interceptor';

import {
  devErrorInterceptor
} from './core/interceptors/dev-error-interceptor';

import {
  sessionExpiredInterceptor
} from './core/interceptors/session-expired-interceptor';

export const appConfig: ApplicationConfig = {

  providers: [

    provideRouter(routes),

    provideHttpClient(
      withInterceptors([
        authInterceptor,
        sessionExpiredInterceptor,
        devErrorInterceptor
      ])
    )

  ]

};

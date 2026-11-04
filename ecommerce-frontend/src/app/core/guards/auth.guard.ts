import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { map } from 'rxjs';

import { AuthService } from '../../auth/auth';

export const authGuard: CanActivateFn = (_route, state) => {

  const authService = inject(AuthService);
  const router = inject(Router);

  const signedIn = () => authService.isLoggedIn() || router.createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });

  const renewed = authService.renewIfExpired(state.url);

  if (renewed === true) {
    return signedIn();
  }

  if ('subscribe' in renewed) {
    return renewed.pipe(map(result => result === true ? signedIn() : result));
  }

  return renewed;
};

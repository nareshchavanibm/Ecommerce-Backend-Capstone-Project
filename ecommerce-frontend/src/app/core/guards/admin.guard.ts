import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { map } from 'rxjs';
import { AuthService } from '../../auth/auth';

export const adminGuard: CanActivateFn = (_route, state) => {

  const authService = inject(AuthService);
  const router = inject(Router);

  const check = () => {
    if (!authService.isLoggedIn()) {
      return router.createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
    }

    if (authService.getRole() !== 'Admin') {
      return router.createUrlTree(['/home']);
    }

    return true;
  };

  const renewed = authService.renewIfExpired(state.url);

  if (renewed === true) {
    return check();
  }

  if ('subscribe' in renewed) {
    return renewed.pipe(map(result => result === true ? check() : result));
  }

  return renewed;
};

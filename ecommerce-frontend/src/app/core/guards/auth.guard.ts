import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { AuthService } from '../../auth/auth';

export const authGuard: CanActivateFn = () => {

  const authService = inject(AuthService);

  return authService.isLoggedIn() || inject(Router).createUrlTree(['/login']);
};

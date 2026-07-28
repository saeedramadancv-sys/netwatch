import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AppRole } from '../models/auth.models';
import { AuthService } from '../services/auth.service';

/**
 * Blocks unauthenticated access and remembers where the user was heading, so a
 * deep link survives the login round trip instead of dumping them on the dashboard.
 */
export const authGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);

  if (auth.isAuthenticated()) {
    return true;
  }

  return router.createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
};

/**
 * Role gate for routes that only operators or admins should reach.
 *
 * This is a usability measure, not a security boundary: it hides a page the API
 * would reject anyway. Every protected operation is authorised server-side, where
 * a client cannot argue with it.
 */
export function roleGuard(...roles: AppRole[]): CanActivateFn {
  return () => {
    const auth = inject(AuthService);
    const router = inject(Router);

    return auth.hasAnyRole(...roles) ? true : router.createUrlTree(['/dashboard']);
  };
}

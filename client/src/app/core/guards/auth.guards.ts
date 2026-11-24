import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { UserRole } from '../../shared/models/api.models';
import { AuthService } from '../authentication/auth.service';

export const authGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  return auth.isAuthenticated()
    ? true
    : inject(Router).createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
};

export const guestGuard: CanActivateFn = () =>
  inject(AuthService).isAuthenticated() ? inject(Router).createUrlTree(['/dashboard']) : true;

/**
 * Hides screens the user cannot use. This is a convenience, not a security boundary:
 * the API enforces the same rules on every request.
 */
export function roleGuard(...roles: UserRole[]): CanActivateFn {
  return () => (inject(AuthService).hasRole(...roles) ? true : inject(Router).createUrlTree(['/dashboard']));
}

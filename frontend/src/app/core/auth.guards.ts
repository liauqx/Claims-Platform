import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from './auth.service';
import { UserRole } from './models';

export const authGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  return auth.accessToken ? true : inject(Router).createUrlTree(['/auth']);
};

export const guestGuard: CanActivateFn = () =>
  inject(AuthService).accessToken ? inject(Router).createUrlTree(['/claims']) : true;

export const roleGuard: CanActivateFn = (route) => {
  const role = inject(AuthService).user()?.role;
  const allowedRoles = route.data['roles'] as UserRole[] | undefined;
  return role && allowedRoles?.includes(role) ? true : inject(Router).createUrlTree(['/claims']);
};

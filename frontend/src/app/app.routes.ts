import { Routes } from '@angular/router';
import { authGuard, guestGuard, roleGuard } from './core/auth.guards';

export const routes: Routes = [
  {
    path: 'auth',
    canActivate: [guestGuard],
    loadComponent: () => import('./pages/auth-page.component').then((module) => module.AuthPageComponent),
  },
  {
    path: 'claims',
    canActivate: [authGuard],
    loadComponent: () => import('./pages/claims-page.component').then((module) => module.ClaimsPageComponent),
  },
  {
    path: 'claims/new',
    canActivate: [authGuard, roleGuard],
    data: { roles: ['CLAIMANT'] },
    loadComponent: () => import('./pages/claim-form-page.component').then((module) => module.ClaimFormPageComponent),
  },
  {
    path: 'claims/:id/edit',
    canActivate: [authGuard, roleGuard],
    data: { roles: ['CLAIMANT', 'OFFICER', 'MANAGER'] },
    loadComponent: () => import('./pages/claim-form-page.component').then((module) => module.ClaimFormPageComponent),
  },
  {
    path: 'claims/:id/request-info',
    canActivate: [authGuard, roleGuard],
    data: { roles: ['OFFICER', 'MANAGER'] },
    loadComponent: () =>
      import('./pages/information-request-page.component').then((module) => module.InformationRequestPageComponent),
  },
  {
    path: 'claims/:id',
    canActivate: [authGuard],
    loadComponent: () => import('./pages/claim-detail-page.component').then((module) => module.ClaimDetailPageComponent),
  },
  {
    path: 'notifications',
    canActivate: [authGuard],
    loadComponent: () =>
      import('./pages/notifications-page.component').then((module) => module.NotificationsPageComponent),
  },
  { path: '', pathMatch: 'full', redirectTo: 'claims' },
  { path: '**', redirectTo: 'claims' },
];

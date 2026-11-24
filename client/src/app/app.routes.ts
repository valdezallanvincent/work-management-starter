import { Routes } from '@angular/router';
import { authGuard, guestGuard, roleGuard } from './core/guards/auth.guards';

export const routes: Routes = [
  {
    path: 'login',
    canActivate: [guestGuard],
    title: 'Sign in',
    loadComponent: () => import('./features/authentication/login-page').then((m) => m.LoginPage),
  },
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () => import('./layout/shell').then((m) => m.Shell),
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
      {
        path: 'dashboard',
        title: 'Dashboard',
        loadComponent: () => import('./features/dashboard/dashboard-page').then((m) => m.DashboardPage),
      },
      { path: 'projects', loadChildren: () => import('./features/projects/projects.routes').then((m) => m.projectRoutes) },
      { path: 'work-items', loadChildren: () => import('./features/work-items/work-items.routes').then((m) => m.workItemRoutes) },
      {
        path: 'profile',
        title: 'Profile',
        loadComponent: () => import('./features/profile/profile-page').then((m) => m.ProfilePage),
      },
      {
        path: 'users',
        title: 'Users',
        canActivate: [roleGuard('Admin')],
        loadComponent: () => import('./features/users/user-list-page').then((m) => m.UserListPage),
      },
    ],
  },
  { path: '**', redirectTo: '' },
];

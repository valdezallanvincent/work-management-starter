import { Routes } from '@angular/router';

export const workItemRoutes: Routes = [
  { path: '', title: 'Work items', loadComponent: () => import('./work-item-list-page').then((m) => m.WorkItemListPage) },
  { path: ':id', title: 'Work item', loadComponent: () => import('./work-item-details-page').then((m) => m.WorkItemDetailsPage) },
];

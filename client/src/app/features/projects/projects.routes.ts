import { Routes } from '@angular/router';

export const projectRoutes: Routes = [
  { path: '', title: 'Projects', loadComponent: () => import('./project-list-page').then((m) => m.ProjectListPage) },
  { path: ':id', title: 'Project', loadComponent: () => import('./project-details-page').then((m) => m.ProjectDetailsPage) },
];

import { Routes } from '@angular/router';
import { authGuard, guestGuard } from './core/guards/auth.guard';

/** Feature areas are lazy-loaded so the login page stays small. */
export const routes: Routes = [
  {
    path: 'login',
    canActivate: [guestGuard],
    title: 'Sign in · Todo',
    loadComponent: () => import('./features/auth/login.component').then((m) => m.LoginComponent),
  },
  {
    path: 'register',
    canActivate: [guestGuard],
    title: 'Create account · Todo',
    loadComponent: () => import('./features/auth/register.component').then((m) => m.RegisterComponent),
  },
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () => import('./layout/shell.component').then((m) => m.ShellComponent),
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'all' },
      {
        path: 'all',
        loadComponent: () => import('./features/tasks/task-list.page').then((m) => m.TaskListPageComponent),
      },
      {
        path: 'sections/:id',
        loadComponent: () => import('./features/tasks/task-list.page').then((m) => m.TaskListPageComponent),
      },
      {
        path: 'settings',
        loadComponent: () => import('./features/settings/settings.page').then((m) => m.SettingsPageComponent),
      },
    ],
  },
  { path: '**', redirectTo: '' },
];

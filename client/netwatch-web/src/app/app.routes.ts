import { Routes } from '@angular/router';
import { authGuard, roleGuard } from './core/guards/auth.guard';

/**
 * Every feature is lazy-loaded.
 *
 * The login screen is the only thing a signed-out visitor needs, so shipping the
 * dashboard, the chart and the device forms in the initial bundle would slow the
 * first paint for no benefit.
 */
export const routes: Routes = [
  {
    path: 'login',
    loadComponent: () => import('./features/login/login.component').then((m) => m.LoginComponent),
  },
  {
    path: 'dashboard',
    canActivate: [authGuard],
    loadComponent: () => import('./features/dashboard/dashboard.component').then((m) => m.DashboardComponent),
  },
  {
    path: 'devices',
    canActivate: [authGuard],
    loadComponent: () => import('./features/devices/device-list.component').then((m) => m.DeviceListComponent),
  },
  {
    // Declared before ':id' so "new" is not swallowed as a device id.
    path: 'devices/new',
    canActivate: [authGuard, roleGuard('Admin', 'Operator')],
    loadComponent: () => import('./features/devices/device-form.component').then((m) => m.DeviceFormComponent),
  },
  {
    path: 'devices/:id',
    canActivate: [authGuard],
    loadComponent: () => import('./features/devices/device-detail.component').then((m) => m.DeviceDetailComponent),
  },
  {
    path: 'devices/:id/edit',
    canActivate: [authGuard, roleGuard('Admin', 'Operator')],
    loadComponent: () => import('./features/devices/device-form.component').then((m) => m.DeviceFormComponent),
  },
  {
    path: 'probes/:id',
    canActivate: [authGuard],
    loadComponent: () => import('./features/probes/probe-detail.component').then((m) => m.ProbeDetailComponent),
  },
  {
    path: 'incidents',
    canActivate: [authGuard],
    loadComponent: () => import('./features/incidents/incident-list.component').then((m) => m.IncidentListComponent),
  },
  { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
  { path: '**', redirectTo: 'dashboard' },
];

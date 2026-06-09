// src/app/app.routes.ts
// Definicion de rutas de la aplicacion con lazy loading por feature.
// El guard authGuard protege todas las rutas de negocio.
// PreloadAllModules (en app.config.ts) precarga los chunks para uso offline.

import { Routes } from '@angular/router';
import { authGuard } from './core/guards/auth.guard';

export const routes: Routes = [
  // Redireccion raiz al dashboard
  {
    path: '',
    redirectTo: 'dashboard',
    pathMatch: 'full'
  },

  // Dashboard de rentabilidad (ruta principal)
  {
    path: 'dashboard',
    loadComponent: () =>
      import('./features/dashboard/dashboard.component').then(m => m.DashboardComponent),
    canActivate: [authGuard]
  },

  // Feature: Aplicaciones de quimicos
  {
    path: 'aplicaciones',
    loadChildren: () =>
      import('./features/aplicaciones/aplicaciones.routes').then(m => m.aplicacionesRoutes),
    canActivate: [authGuard]
  },

  // Feature: Cosechas
  {
    path: 'cosechas',
    loadChildren: () =>
      import('./features/cosechas/cosechas.routes').then(m => m.cosechasRoutes),
    canActivate: [authGuard]
  },

  // Feature: Costos
  {
    path: 'costos',
    loadChildren: () =>
      import('./features/costos/costos.routes').then(m => m.costosRoutes),
    canActivate: [authGuard]
  },

  // Feature: Lotes
  {
    path: 'lotes',
    loadChildren: () =>
      import('./features/lotes/lotes.routes').then(m => m.lotesRoutes),
    canActivate: [authGuard]
  },

  // Feature: Autenticacion (no requiere guard)
  {
    path: 'auth',
    loadChildren: () =>
      import('./features/auth/auth.routes').then(m => m.authRoutes)
  },

  // Fallback: cualquier ruta desconocida va al dashboard
  {
    path: '**',
    redirectTo: 'dashboard'
  }
];

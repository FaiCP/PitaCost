// src/app/features/auth/auth.routes.ts
// Rutas del modulo de autenticacion. Sin authGuard (acceso publico).
// El componente de login maneja la redireccion post-auth via Router.

import { Routes } from '@angular/router';

export const authRoutes: Routes = [
  {
    path: 'login',
    loadComponent: () =>
      import('./pages/login/login.component').then(m => m.LoginComponent)
  },
  {
    path: '',
    redirectTo: 'login',
    pathMatch: 'full'
  }
];

// src/app/features/costos/costos.routes.ts
// Rutas lazy de la feature Costos. Protegidas por authGuard en app.routes.ts.

import { Routes } from '@angular/router';

export const costosRoutes: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./pages/lista-costos/lista-costos.component').then(
        m => m.ListaCostosComponent
      )
  },
  {
    path: 'nuevo',
    loadComponent: () =>
      import('./pages/nuevo-costo/nuevo-costo.component').then(
        m => m.NuevoCostoComponent
      )
  }
];

// src/app/features/aplicaciones/aplicaciones.routes.ts
// Rutas lazy del feature Aplicaciones de quimicos.

import { Routes } from '@angular/router';

export const aplicacionesRoutes: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./pages/lista-aplicaciones/lista-aplicaciones.component').then(
        m => m.ListaAplicacionesComponent
      )
  },
  {
    path: 'nueva',
    loadComponent: () =>
      import('./pages/nueva-aplicacion/nueva-aplicacion.component').then(
        m => m.NuevaAplicacionComponent
      )
  }
];

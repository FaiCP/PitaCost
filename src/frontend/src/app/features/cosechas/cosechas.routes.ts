// src/app/features/cosechas/cosechas.routes.ts
// Rutas lazy de la feature Cosechas. Protegidas por authGuard en app.routes.ts.
// Lista como ruta raiz del feature; nueva-cosecha como ruta hija.

import { Routes } from '@angular/router';

export const cosechasRoutes: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./pages/lista-cosechas/lista-cosechas.component').then(
        m => m.ListaCosechasComponent
      )
  },
  {
    path: 'nueva',
    loadComponent: () =>
      import('./pages/nueva-cosecha/nueva-cosecha.component').then(
        m => m.NuevaCosechaComponent
      )
  }
];

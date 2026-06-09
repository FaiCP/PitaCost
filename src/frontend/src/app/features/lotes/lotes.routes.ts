// src/app/features/lotes/lotes.routes.ts
// Rutas del feature Lotes con lazy loading por pagina.

import { Routes } from '@angular/router';

export const lotesRoutes: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./pages/lista-lotes/lista-lotes.component').then(m => m.ListaLotesComponent)
  },
  {
    path: 'nuevo',
    loadComponent: () =>
      import('./pages/nuevo-lote/nuevo-lote.component').then(m => m.NuevoLoteComponent)
  }
];

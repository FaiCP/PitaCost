// src/app/core/guards/auth.guard.ts
// Guard funcional de autenticacion. Protege las rutas que requieren sesion activa.
// Redirige al login si el usuario no esta autenticado.

import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

/**
 * Guard que verifica si el usuario tiene una sesion activa.
 * Usa la API funcional de guards de Angular 15+ con inject().
 * Se aplica en app.routes.ts a: dashboard, aplicaciones, cosechas, costos.
 */
export const authGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);

  // isAuthenticated es un computed() signal — acceso sin subscription
  if (auth.isAuthenticated()) {
    return true;
  }

  // Redirigir al login manteniendo la URL destino para redireccion post-login
  return router.createUrlTree(['/auth/login']);
};

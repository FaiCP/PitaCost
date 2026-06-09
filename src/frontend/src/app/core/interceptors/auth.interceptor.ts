// src/app/core/interceptors/auth.interceptor.ts
// Interceptor funcional que adjunta el JWT Bearer token a todos los requests de la API.
// Usa la funcion inject() compatible con el modelo de interceptores de Angular 15+.

import { HttpInterceptorFn, HttpRequest, HttpHandlerFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { AuthService } from '../services/auth.service';
import { environment } from '../../../environments/environment';

export const authInterceptor: HttpInterceptorFn = (
  req: HttpRequest<unknown>,
  next: HttpHandlerFn
) => {
  const authService = inject(AuthService);

  // Solo agregar token a requests dirigidos a nuestra API
  if (!req.url.startsWith(environment.apiBaseUrl)) {
    return next(req);
  }

  const token = authService.getAccessToken();
  if (!token) {
    return next(req);
  }

  const reqConToken = req.clone({
    setHeaders: {
      Authorization: `Bearer ${token}`
    }
  });

  return next(reqConToken);
};

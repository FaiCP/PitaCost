// src/app/core/interceptors/correlation-id.interceptor.ts
// Agrega el header X-Correlation-Id a todos los requests para trazabilidad.
// El servidor lo propaga en los logs para correlacionar request <-> respuesta.
// Convencion definida en api-contract.md "Convenciones Generales".

import { HttpInterceptorFn, HttpRequest, HttpHandlerFn } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { v4 as uuidv4 } from 'uuid';

export const correlationIdInterceptor: HttpInterceptorFn = (
  req: HttpRequest<unknown>,
  next: HttpHandlerFn
) => {
  // Solo para requests a nuestra API
  if (!req.url.startsWith(environment.apiBaseUrl)) {
    return next(req);
  }

  const correlationId = uuidv4();
  const reqConCorrelacion = req.clone({
    setHeaders: {
      'X-Correlation-Id': correlationId,
      'X-Client-Version': environment.appVersion,
      'X-Device-Id': localStorage.getItem(environment.deviceIdStorageKey) ?? 'unknown'
    }
  });

  return next(reqConCorrelacion);
};

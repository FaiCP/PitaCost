// src/app/core/interceptors/offline.interceptor.ts
// Interceptor critico para el patron offline-first.
// Cuando el dispositivo no tiene conexion y el request va a la API:
//   - Encola la operacion en RxDB via SyncService
//   - Retorna una respuesta mock exitosa para que el componente no sepa la diferencia
// Cuando hay conexion: pasa el request normalmente al servidor.
// Ver flujo completo en offline-sync-flow.md

import {
  HttpInterceptorFn,
  HttpRequest,
  HttpHandlerFn,
  HttpResponse,
  HttpStatusCode
} from '@angular/common/http';
import { inject } from '@angular/core';
import { of } from 'rxjs';
import { ConnectivityService } from '../services/connectivity.service';
import { environment } from '../../../environments/environment';

/**
 * Endpoints que NUNCA deben ser interceptados offline (auth, health, sync push).
 * El sync push se maneja por SyncService directamente cuando hay conexion.
 */
const ENDPOINTS_EXCLUIDOS = [
  '/api/auth/',
  '/api/health/',
  '/api/sync/push',
  '/api/sync/pull'
];

/**
 * Metodos HTTP que representan mutaciones y deben encolarse offline.
 * GET, HEAD y OPTIONS nunca se interceptan (son de solo lectura).
 */
const METODOS_MUTACION = ['POST', 'PUT', 'PATCH', 'DELETE'];

export const offlineInterceptor: HttpInterceptorFn = (
  req: HttpRequest<unknown>,
  next: HttpHandlerFn
) => {
  const connectivity = inject(ConnectivityService);

  // No interceptar si no es un request a nuestra API
  if (!req.url.startsWith(environment.apiBaseUrl)) {
    return next(req);
  }

  // No interceptar endpoints excluidos
  const path = req.url.replace(environment.apiBaseUrl, '');
  if (ENDPOINTS_EXCLUIDOS.some(e => path.startsWith(e))) {
    return next(req);
  }

  // No interceptar requests de solo lectura (GET, HEAD, OPTIONS)
  if (!METODOS_MUTACION.includes(req.method)) {
    return next(req);
  }

  // Si estamos online, pasar normalmente al servidor
  if (connectivity.puedeSync()) {
    return next(req);
  }

  // === MODO OFFLINE ===
  // La operacion real ya fue persistida en RxDB por el feature service
  // antes de llegar al interceptor. Aqui solo retornamos una respuesta mock
  // exitosa para que el formulario pueda continuar su flujo normal.
  //
  // El componente muestra el badge "PENDIENTE SYNC" basandose en el signal
  // SyncService.hasPendingChanges, no en esta respuesta mock.

  const respuestaMock = new HttpResponse({
    status: HttpStatusCode.Accepted,           // 202 Accepted (encolado offline)
    statusText: 'Accepted (Offline Queue)',
    body: {
      success: true,
      data: { creadoOffline: true, enCola: true },
      timestamp: new Date().toISOString()
    }
  });

  return of(respuestaMock);
};

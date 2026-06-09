// src/app/app.config.ts
// Configuracion central de la aplicacion standalone.
// Registra todos los providers globales: router, http, animaciones, service worker y RxDB.

import {
  ApplicationConfig,
  APP_INITIALIZER,
  isDevMode,
  provideZoneChangeDetection
} from '@angular/core';
import { provideRouter, withPreloading, PreloadAllModules } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { provideServiceWorker } from '@angular/service-worker';

import { routes } from './app.routes';
import { RxDBService } from './core/database/rxdb.service';
import { authInterceptor } from './core/interceptors/auth.interceptor';
import { offlineInterceptor } from './core/interceptors/offline.interceptor';
import { correlationIdInterceptor } from './core/interceptors/correlation-id.interceptor';

export const appConfig: ApplicationConfig = {
  providers: [
    // Zone-based change detection con coalescencia de eventos para rendimiento
    provideZoneChangeDetection({ eventCoalescing: true }),

    // Router con precarga de todos los modulos lazy para uso sin conexion
    provideRouter(routes, withPreloading(PreloadAllModules)),

    // HttpClient con interceptores funcionales en orden: auth -> offline -> correlacion
    provideHttpClient(
      withInterceptors([
        authInterceptor,
        offlineInterceptor,
        correlationIdInterceptor
      ])
    ),

    // Animaciones de Angular Material (async para no bloquear el bootstrap)
    provideAnimationsAsync(),

    // Service Worker para PWA (deshabilitado en desarrollo)
    provideServiceWorker('ngsw-worker.js', {
      enabled: !isDevMode(),
      // Registrar cuando la app este estable para no afectar el tiempo de carga inicial
      registrationStrategy: 'registerWhenStable:30000'
    }),

    // APP_INITIALIZER: inicializar RxDB antes de renderizar cualquier componente
    // Garantiza que las colecciones esten disponibles para SyncService y features
    {
      provide: APP_INITIALIZER,
      useFactory: (rxdb: RxDBService) => () => rxdb.initialize(),
      deps: [RxDBService],
      multi: true
    }
  ]
};

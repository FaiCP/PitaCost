// src/environments/environment.ts
// Configuracion para entorno de desarrollo
export const environment = {
  production: false,
  apiBaseUrl: 'http://localhost:58479/v1',
  apiHealthUrl: 'http://localhost:58479',
  signalrHubUrl: 'https://api.pitasmart.ec/hubs/precios',
  appVersion: '1.0.0',
  // Identificador unico del dispositivo generado una sola vez
  deviceIdStorageKey: 'pitasmart_device_id',
  // Nombre del Service Worker (deshabilitado en dev via angular.json)
  serviceWorkerEnabled: false,
  // Configuracion WebAuthn
  webAuthn: {
    rpId: 'localhost',
    rpName: 'PitaSmart (Dev)'
  },
  // Configuracion de sync
  sync: {
    // Intervalo de sincronizacion automatica en milisegundos (2 minutos)
    intervalMs: 120_000,
    // Maximo de operaciones por batch de sync push
    maxBatchSize: 100,
    // Maximo de intentos antes de marcar como FALLIDA
    maxRetries: 5,
    // Retardos de reintento en segundos: [inmediato, 5s, 15s, 60s, 300s]
    retryDelaysMs: [0, 5_000, 15_000, 60_000, 300_000]
  },
  // Nombre de la base de datos RxDB en IndexedDB
  rxdbName: 'pitasmart-dev'
};

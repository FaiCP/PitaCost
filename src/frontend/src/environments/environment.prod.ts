// src/environments/environment.prod.ts
// Configuracion para entorno de produccion
export const environment = {
  production: true,
  apiBaseUrl: 'https://api.pitasmart.ec/v1',
  signalrHubUrl: 'https://api.pitasmart.ec/hubs/precios',
  appVersion: '1.0.0',
  deviceIdStorageKey: 'pitasmart_device_id',
  serviceWorkerEnabled: true,
  webAuthn: {
    rpId: 'pitasmart.ec',
    rpName: 'PitaSmart'
  },
  sync: {
    intervalMs: 120_000,
    maxBatchSize: 100,
    maxRetries: 5,
    retryDelaysMs: [0, 5_000, 15_000, 60_000, 300_000]
  },
  rxdbName: 'pitasmart'
};

// src/app/core/services/connectivity.service.ts
// Detecta conectividad real con el servidor mediante ping HTTP.
// navigator.onLine es insuficiente en Android: solo detecta si hay WiFi/datos activos,
// no si hay ruta real al servidor. Ver offline-sync-flow.md seccion "Deteccion de Conectividad".

import { Injectable, signal, computed, OnDestroy } from '@angular/core';
import { interval, Subscription } from 'rxjs';
import { filter } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { CalidadConexion } from '../models/sync.model';

@Injectable({ providedIn: 'root' })
export class ConnectivityService implements OnDestroy {
  /** Signal reactivo del estado de conectividad real (no solo navigator.onLine) */
  readonly isOnline = signal<boolean>(navigator.onLine);

  /** Calidad de la conexion basada en latencia del ping */
  readonly connectionQuality = signal<CalidadConexion>(
    navigator.onLine ? 'good' : 'offline'
  );

  /** Signal computada: true si la conexion existe y es aceptable */
  readonly puedeSync = computed(() =>
    this.isOnline() && this.connectionQuality() !== 'offline'
  );

  private readonly pingUrl = `${environment.apiHealthUrl}/health`;
  private pingSubscription?: Subscription;
  private readonly onlineHandler: () => void;
  private readonly offlineHandler: () => void;

  constructor() {
    // Manejadores de eventos del navegador (online/offline)
    this.onlineHandler = () => void this.verificarConectividadReal();
    this.offlineHandler = () => {
      this.isOnline.set(false);
      this.connectionQuality.set('offline');
    };

    window.addEventListener('online', this.onlineHandler);
    window.addEventListener('offline', this.offlineHandler);

    // Ping periodico cada 30 segundos cuando navigator.onLine dice que hay conexion.
    // Esto captura casos de "conexion cautiva" (hotel, aeropuerto) o perdida de ruta.
    this.pingSubscription = interval(30_000)
      .pipe(filter(() => navigator.onLine))
      .subscribe(() => void this.verificarConectividadReal());

    // Verificacion inicial al arrancar la app
    if (navigator.onLine) {
      void this.verificarConectividadReal();
    }
  }

  /**
   * Realiza un HEAD request real al servidor para verificar conectividad.
   * Mide la latencia para clasificar la calidad de la conexion.
   * Timeout de 5 segundos para no bloquear la UI.
   */
  async verificarConectividadReal(): Promise<void> {
    try {
      const inicio = Date.now();
      await fetch(this.pingUrl, {
        method: 'HEAD',
        cache: 'no-cache',
        // AbortSignal.timeout es ES2022 y esta disponible en navegadores modernos
        signal: AbortSignal.timeout(5_000)
      });
      const latenciaMs = Date.now() - inicio;

      this.isOnline.set(true);
      // Conexion rapida < 2s, lenta >= 2s (considera condiciones de campo)
      this.connectionQuality.set(latenciaMs < 2_000 ? 'good' : 'slow');
    } catch {
      // Timeout o error de red: sin conectividad real
      this.isOnline.set(false);
      this.connectionQuality.set('offline');
    }
  }

  ngOnDestroy(): void {
    window.removeEventListener('online', this.onlineHandler);
    window.removeEventListener('offline', this.offlineHandler);
    this.pingSubscription?.unsubscribe();
  }
}

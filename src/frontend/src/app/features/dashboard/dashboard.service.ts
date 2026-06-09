// src/app/features/dashboard/dashboard.service.ts
// Servicio de datos para el Dashboard de Rentabilidad.
// Implementa el patron Cache-Aside: API cuando online, RxDB cuando offline.
// El caller (DashboardComponent) decide que fuente mostrar segun modoOffline.

import { Injectable, inject } from '@angular/core';
import { firstValueFrom } from 'rxjs';
import { ApiService } from '../../core/services/api.service';
import { RxDBService } from '../../core/database/rxdb.service';
import { ConnectivityService } from '../../core/services/connectivity.service';
import { DashboardRentabilidad, KpisLocales } from '../../core/models/rentabilidad.model';

/** Estructura de la cache de rentabilidad en RxDB (serializada en el costo doc) */
interface RentabilidadCacheEntry {
  loteId: string;
  desde: string;
  hasta: string;
  data: DashboardRentabilidad;
  cachedAt: string;
}

/** Resultado del servicio: datos completos del servidor o KPIs offline */
export interface RentabilidadResult {
  data: DashboardRentabilidad | null;
  kpisOffline: KpisLocales | null;
  fuenteOffline: boolean;
}

@Injectable({ providedIn: 'root' })
export class DashboardService {
  private readonly api = inject(ApiService);
  private readonly rxdb = inject(RxDBService);
  private readonly connectivity = inject(ConnectivityService);

  /**
   * Obtiene rentabilidad del API si hay conexion, o calcula KPIs desde
   * RxDB si esta offline. Nunca lanza excepciones hacia el caller.
   */
  async getRentabilidad(
    loteId: string,
    desde: string,    // YYYY-MM-DD
    hasta: string     // YYYY-MM-DD
  ): Promise<RentabilidadResult> {
    if (this.connectivity.isOnline()) {
      return await this.obtenerDesdeApi(loteId, desde, hasta);
    }
    return await this.calcularDesdeRxDB(loteId, desde, hasta);
  }

  /**
   * Solicita la rentabilidad completa al servidor y guarda el resultado
   * en RxDB para uso offline posterior.
   */
  private async obtenerDesdeApi(
    loteId: string,
    desde: string,
    hasta: string
  ): Promise<RentabilidadResult> {
    try {
      const respuesta = await firstValueFrom(
        this.api.get<DashboardRentabilidad>(
          `/api/lotes/${loteId}/rentabilidad`,
          { desde, hasta }
        )
      );

      const data = respuesta.data;
      await this.cacheRentabilidad(loteId, data);

      return { data, kpisOffline: null, fuenteOffline: false };
    } catch {
      // Si la API falla estando "online" (ej: servidor caido), fallback a cache
      return await this.calcularDesdeRxDB(loteId, desde, hasta);
    }
  }

  /**
   * Calcula KPIs basicos desde los datos locales de RxDB.
   * Formula documentada en bounded-contexts.md seccion 4.
   * Se activa cuando el dispositivo esta offline o el servidor no responde.
   */
  private async calcularDesdeRxDB(
    loteId: string,
    desde: string,
    hasta: string
  ): Promise<RentabilidadResult> {
    try {
      // Intentar retornar cache guardada anteriormente del servidor
      const cacheGuardada = await this.leerCacheRentabilidad(loteId, desde, hasta);
      if (cacheGuardada) {
        return { data: cacheGuardada, kpisOffline: null, fuenteOffline: true };
      }

      // Sin cache: calcular KPIs desde costos locales
      const costosDocs = await this.rxdb.costos
        .find({
          selector: {
            loteId,
            fecha: { $gte: desde, $lte: hasta },
            eliminado: false
          }
        })
        .exec();

      const totalCostos = costosDocs.reduce((suma, doc) => suma + doc.monto, 0);

      // Los ingresos locales se calcularan cuando se integre la coleccion cosechas
      const totalIngresos = 0;

      const utilidadBruta = totalIngresos - totalCostos;
      const margenBruto = totalIngresos > 0
        ? (utilidadBruta / totalIngresos) * 100
        : null;
      const roi = totalCostos > 0
        ? (utilidadBruta / totalCostos) * 100
        : null;

      const loteDoc = await this.rxdb.lotes.findOne(loteId).exec();
      const areaHa = loteDoc?.areaHa ?? 0;
      const utilidadPorHa = areaHa > 0 ? utilidadBruta / areaHa : null;

      const kpisOffline: KpisLocales = {
        totalIngresos,
        totalCostos,
        utilidadBruta,
        margenBruto,
        roi,
        utilidadPorHa,
        esCalculoOffline: true
      };

      return { data: null, kpisOffline, fuenteOffline: true };
    } catch {
      // Estado completamente degradado: sin datos
      return {
        data: null,
        kpisOffline: {
          totalIngresos: 0,
          totalCostos: 0,
          utilidadBruta: 0,
          margenBruto: null,
          roi: null,
          utilidadPorHa: null,
          esCalculoOffline: true
        },
        fuenteOffline: true
      };
    }
  }

  /**
   * Persiste la respuesta del servidor en un costo doc especial de tipo cache.
   * Usa la descripcion como clave serializada para recuperar luego sin coleccion extra.
   * Estrategia: upsert por clave compuesta loteId+desde+hasta.
   */
  private async cacheRentabilidad(
    loteId: string,
    data: DashboardRentabilidad
  ): Promise<void> {
    try {
      const desde = data.periodo.desde;
      const hasta = data.periodo.hasta;
      const cacheId = `__cache_rentabilidad__${loteId}_${desde}_${hasta}`;

      const entrada: RentabilidadCacheEntry = {
        loteId,
        desde,
        hasta,
        data,
        cachedAt: new Date().toISOString()
      };

      // Upsert: insertar o reemplazar si ya existe la cache para este periodo
      await this.rxdb.costos.upsert({
        id: cacheId,
        loteId,
        fecha: desde,
        categoria: 'OTROS',
        descripcion: JSON.stringify(entrada),
        monto: 0,
        aplicacionId: null,
        cosechaId: null,
        creadoOffline: false,
        clientTimestamp: new Date().toISOString(),
        eliminado: false,
        rowVersion: null,
        syncStatus: 'SYNCED'
      });
    } catch {
      // El cache es best-effort: un fallo no debe interrumpir el flujo principal
    }
  }

  /**
   * Lee la cache de rentabilidad guardada para un periodo especifico.
   * Retorna null si no existe o si los datos no corresponden al periodo.
   */
  private async leerCacheRentabilidad(
    loteId: string,
    desde: string,
    hasta: string
  ): Promise<DashboardRentabilidad | null> {
    try {
      const cacheId = `__cache_rentabilidad__${loteId}_${desde}_${hasta}`;
      const doc = await this.rxdb.costos.findOne(cacheId).exec();
      if (!doc || !doc.descripcion) return null;

      const entrada = JSON.parse(doc.descripcion) as RentabilidadCacheEntry;
      if (entrada.loteId !== loteId || entrada.desde !== desde || entrada.hasta !== hasta) {
        return null;
      }

      return entrada.data;
    } catch {
      return null;
    }
  }
}

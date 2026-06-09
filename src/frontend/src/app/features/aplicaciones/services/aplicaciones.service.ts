// src/app/features/aplicaciones/services/aplicaciones.service.ts
// Servicio del feature Aplicaciones. Implementa el patron offline-first:
// todas las escrituras van primero a RxDB y luego a la cola de sync.
// Las lecturas sirven siempre desde RxDB (disponibles offline).

import { Injectable, inject } from '@angular/core';
import { Observable, from } from 'rxjs';
import { map } from 'rxjs/operators';
import { v4 as uuidv4 } from 'uuid';
import { RxDBService } from '../../../core/database/rxdb.service';
import { SyncService } from '../../../core/services/sync.service';
import { ApiService } from '../../../core/services/api.service';
import { ConnectivityService } from '../../../core/services/connectivity.service';
import { AplicacionDocType } from '../../../core/database/rxdb-schemas';
import {
  AplicacionQuimico,
  CrearAplicacionRequest,
  AplicacionResponse
} from '../../../core/models/aplicacion.model';
import { firstValueFrom } from 'rxjs';

@Injectable({ providedIn: 'root' })
export class AplicacionesService {
  private readonly rxdb = inject(RxDBService);
  private readonly sync = inject(SyncService);
  private readonly api = inject(ApiService);
  private readonly connectivity = inject(ConnectivityService);

  /**
   * Retorna un Observable reactivo de aplicaciones de un lote.
   * RxDB emite automaticamente cuando los datos cambian (incluido sync).
   * Los componentes usan async pipe para suscribirse.
   */
  obtenerPorLote(loteId: string): Observable<AplicacionQuimico[]> {
    return from(
      this.rxdb.aplicaciones
        .find({
          selector: { loteId },
          sort: [{ fechaAplicacion: 'desc' }]
        })
        .$
    ).pipe(
      map(docs => docs.map(doc => this.docAModelo(doc.toJSON())))
    );
  }

  /**
   * Registra una nueva aplicacion.
   * Flujo offline-first segun offline-sync-flow.md:
   *   1. Generar UUID en cliente
   *   2. Validar localmente (hecho en el componente antes de llamar este metodo)
   *   3. Persistir en RxDB
   *   4. Encolar en SyncService
   *   5. Si hay conexion, SyncService sincroniza automaticamente
   */
  async registrar(
    request: Omit<CrearAplicacionRequest, 'id' | 'clientTimestamp' | 'creadoOffline'>
  ): Promise<AplicacionQuimico> {
    const id = uuidv4();
    const ahora = new Date().toISOString();
    const estaOffline = !this.connectivity.puedeSync();

    const aplicacionCompleta: CrearAplicacionRequest = {
      ...request,
      id,
      clientTimestamp: ahora,
      creadoOffline: estaOffline
    };

    // Paso 3: Persistir en RxDB (siempre, independiente de conectividad)
    const doc: AplicacionDocType = {
      id,
      loteId: request.loteId,
      insumoId: request.insumoId,
      fechaAplicacion: request.fechaAplicacion,
      dosisCantidad: request.dosis.cantidad,
      dosisUnidad: request.dosis.unidad,
      areaAplicadaHa: request.areaAplicadaHa,
      metodoAplicacion: request.metodoAplicacion,
      operadorNombre: request.operadorNombre,
      coordenadasLatitud: request.coordenadasGps?.latitud ?? null,
      coordenadasLongitud: request.coordenadasGps?.longitud ?? null,
      observaciones: request.observaciones ?? null,
      costoTotal: request.costoTotal,
      diasCarencia: 0,            // El servidor calcula y actualiza post-sync
      fechaFinCarencia: null,
      creadoOffline: estaOffline,
      clientTimestamp: ahora,
      rowVersion: null,
      createdAt: ahora,
      loteNombre: null,
      insumoNombre: null,
      syncStatus: 'PENDIENTE'
    };

    await this.rxdb.aplicaciones.insert(doc);

    // Paso 4: Encolar operacion de sync
    await this.sync.encolarOperacion(
      'CREAR_APLICACION',
      id,
      'AplicacionQuimico',
      aplicacionCompleta as unknown as Record<string, unknown>
    );

    return this.docAModelo(doc);
  }

  /**
   * Verifica localmente si un insumo tiene periodo de carencia activo en un lote.
   * Implementa la regla critica de negocio del lado del cliente (bounded-contexts.md sec 3).
   * El servidor SIEMPRE re-valida, pero el cliente bloquea inmediatamente para UX offline.
   */
  async verificarPeriodoCarencia(
    loteId: string,
    fechaCosecha: Date
  ): Promise<{ bloqueada: boolean; aplicacion?: AplicacionQuimico; diasRestantes?: number }> {
    const fechaCosechaStr = fechaCosecha.toISOString();

    // Buscar aplicaciones con fechaFinCarencia posterior a la fecha de cosecha
    const aplicaciones = await this.rxdb.aplicaciones
      .find({
        selector: {
          loteId,
          fechaFinCarencia: { $gt: fechaCosechaStr },
          syncStatus: { $nin: ['RECHAZADO'] }
        },
        sort: [{ fechaFinCarencia: 'desc' }]
      })
      .exec();

    if (aplicaciones.length === 0) {
      return { bloqueada: false };
    }

    const aplicacionBloqueante = aplicaciones[0];
    const finCarencia = new Date(aplicacionBloqueante.fechaFinCarencia!);
    const diasRestantes = Math.ceil(
      (finCarencia.getTime() - fechaCosecha.getTime()) / (1_000 * 60 * 60 * 24)
    );

    return {
      bloqueada: true,
      aplicacion: this.docAModelo(aplicacionBloqueante.toJSON()),
      diasRestantes
    };
  }

  /** Mapea un documento de RxDB al modelo de dominio */
  private docAModelo(doc: AplicacionDocType): AplicacionQuimico {
    return {
      id: doc.id,
      loteId: doc.loteId,
      insumoId: doc.insumoId,
      fechaAplicacion: doc.fechaAplicacion,
      dosis: { cantidad: doc.dosisCantidad, unidad: doc.dosisUnidad as AplicacionQuimico['dosis']['unidad'] },
      areaAplicadaHa: doc.areaAplicadaHa,
      metodoAplicacion: doc.metodoAplicacion as AplicacionQuimico['metodoAplicacion'],
      operadorNombre: doc.operadorNombre,
      coordenadasGps: doc.coordenadasLatitud != null && doc.coordenadasLongitud != null
        ? { latitud: doc.coordenadasLatitud, longitud: doc.coordenadasLongitud }
        : undefined,
      observaciones: doc.observaciones ?? undefined,
      costoTotal: doc.costoTotal,
      creadoOffline: doc.creadoOffline,
      clientTimestamp: doc.clientTimestamp,
      rowVersion: doc.rowVersion ?? undefined,
      createdAt: doc.createdAt,
      periodoCarencia: doc.fechaFinCarencia ? {
        diasCarencia: doc.diasCarencia,
        fechaFinCarencia: doc.fechaFinCarencia,
        cosechaBloqueada: new Date(doc.fechaFinCarencia) > new Date()
      } : undefined,
      loteNombre: doc.loteNombre ?? undefined,
      insumoNombre: doc.insumoNombre ?? undefined,
      syncStatus: doc.syncStatus as AplicacionQuimico['syncStatus']
    };
  }
}

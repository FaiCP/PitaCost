// src/app/features/lotes/services/lotes.service.ts
// Servicio del feature Lotes. Expone lotes del usuario desde RxDB (offline-first).
// Los lotes se descargan del servidor en el pull de sync y se leen siempre localmente.

import { Injectable, inject } from '@angular/core';
import { Observable, from } from 'rxjs';
import { map } from 'rxjs/operators';
import { v4 as uuidv4 } from 'uuid';
import { RxDBService } from '../../../core/database/rxdb.service';
import { LoteDocType } from '../../../core/database/rxdb-schemas';
import { Lote, LoteResumen } from '../../../core/models/lote.model';
import { SyncService } from '../../../core/services/sync.service';
import { ApiService } from '../../../core/services/api.service';

@Injectable({ providedIn: 'root' })
export class LotesService {
  private readonly rxdb = inject(RxDBService);
  private readonly syncService = inject(SyncService);
  private readonly api = inject(ApiService);

  /**
   * Retorna el resumen de todos los lotes activos del usuario.
   * Lee desde RxDB para disponibilidad offline garantizada.
   */
  async obtenerResumen(): Promise<LoteResumen[]> {
    const docs = await this.rxdb.lotes
      .find({
        selector: { activo: true },
        sort: [{ nombre: 'asc' }]
      })
      .exec();

    return docs.map(doc => this.docAResumen(doc.toJSON()));
  }

  /**
   * Observable reactivo de lotes. Emite cuando RxDB actualiza los datos
   * (por ejemplo, despues de un sync con el servidor).
   */
  obtenerTodos$(): Observable<Lote[]> {
    return from(
      this.rxdb.lotes
        .find({ selector: { activo: true }, sort: [{ nombre: 'asc' }] })
        .$
    ).pipe(
      map(docs => docs.map(doc => this.docAModelo(doc.toJSON())))
    );
  }

  /** Busca un lote por ID en RxDB */
  async obtenerPorId(id: string): Promise<Lote | null> {
    const doc = await this.rxdb.lotes.findOne(id).exec();
    return doc ? this.docAModelo(doc.toJSON()) : null;
  }

  /**
   * Persiste un nuevo lote en RxDB y encola la operacion de sync.
   * Retorna el ID del lote creado.
   */
  async crearLote(datos: {
    fincaId: string;
    nombre: string;
    cultivo: string;
    areaHa: number;
    fechaInicioSiembra: string;
  }): Promise<string> {
    const id = uuidv4();
    const ahora = new Date().toISOString();

    // 1. Persistir en RxDB inmediatamente (offline-first)
    await this.rxdb.lotes.insert({
      id,
      fincaId: datos.fincaId,
      nombre: datos.nombre,
      cultivo: datos.cultivo,
      areaHa: datos.areaHa,
      fechaInicioSiembra: datos.fechaInicioSiembra,
      activo: true,
      rowVersion: null,
      createdAt: ahora,
      updatedAt: null,
      creadoOffline: true,
      clientTimestamp: ahora,
      syncStatus: 'PENDIENTE'
    });

    // 2. Encolar operacion para sync con servidor
    await this.syncService.encolarOperacion(
      'CREAR_LOTE',
      id,
      'Lote',
      { id, ...datos, activo: true, clientTimestamp: ahora },
      null
    );

    return id;
  }

  /**
   * Busca un fincaId existente en lotes locales o crea una nueva finca via API.
   * Si no hay conexion, genera un UUID local como fallback.
   */
  async obtenerOCrearFincaId(nombreFinca: string): Promise<string> {
    const lotes = await this.rxdb.lotes.find({ selector: { activo: true } }).exec();
    if (lotes.length > 0) {
      return lotes[0].fincaId;
    }

    try {
      const fincaId = uuidv4();
      await this.api.post<object, { id: string }>('/api/fincas', {
        id: fincaId,
        nombre: nombreFinca,
        provincia: 'Sin especificar',
        canton: 'Sin especificar',
        parroquia: 'Sin especificar',
        areaTotalHa: 0
      }).toPromise();
      return fincaId;
    } catch {
      return uuidv4(); // offline fallback
    }
  }

  private docAResumen(doc: LoteDocType): LoteResumen {
    return {
      id: doc.id,
      nombre: doc.nombre,
      cultivo: doc.cultivo,
      areaHa: doc.areaHa,
      activo: doc.activo
    };
  }

  private docAModelo(doc: LoteDocType): Lote {
    return {
      id: doc.id,
      fincaId: doc.fincaId,
      nombre: doc.nombre,
      cultivo: doc.cultivo,
      areaHa: doc.areaHa,
      ubicacion: doc.ubicacionLatitud != null && doc.ubicacionLongitud != null
        ? { latitud: doc.ubicacionLatitud, longitud: doc.ubicacionLongitud }
        : undefined,
      fechaInicioSiembra: doc.fechaInicioSiembra,
      activo: doc.activo,
      rowVersion: doc.rowVersion ?? undefined,
      creadoOffline: doc.creadoOffline,
      clientTimestamp: doc.clientTimestamp,
      syncStatus: doc.syncStatus as Lote['syncStatus']
    };
  }
}

// src/app/core/services/sync.service.ts
// Motor de sincronizacion offline-first (SyncEngine).
// Orquesta el ciclo PUSH (dispositivo -> servidor) + PULL (servidor -> dispositivo).
// Implementa la logica completa de offline-sync-flow.md.

import { Injectable, signal, computed, inject, effect } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { v4 as uuidv4 } from 'uuid';
import { environment } from '../../../environments/environment';
import { RxDBService } from '../database/rxdb.service';
import { ConnectivityService } from './connectivity.service';
import {
  OperacionPendiente,
  SyncPushRequest,
  SyncPushResponse,
  SyncPullResponse,
  SyncLoteDto,
  SyncInsumoDto,
  SyncAplicacionDto,
  SyncCosechaDto,
  SyncCostoDto,
  SyncResult,
  EstadoSync,
  TipoOperacion
} from '../models/sync.model';
import { OperacionPendienteDocType } from '../database/rxdb-schemas';
import { ApiResponse } from './api.service';

@Injectable({ providedIn: 'root' })
export class SyncService {
  private readonly rxdb = inject(RxDBService);
  private readonly connectivity = inject(ConnectivityService);
  private readonly http = inject(HttpClient);

  // -------------------------------------------------------------------------
  // Signals de estado (fuente de verdad reactiva para la UI)
  // -------------------------------------------------------------------------

  /** Signal del estado actual del motor de sincronizacion */
  readonly syncStatus = signal<EstadoSync>('idle');

  /** Operaciones pendientes en cola (actualizadas reactivamente desde RxDB) */
  readonly pendingOperations = signal<OperacionPendiente[]>([]);

  /** Timestamp de la ultima sincronizacion exitosa */
  readonly lastSyncTime = signal<Date | null>(null);

  /** Alias de conectividad para que los componentes no inyecten ConnectivityService */
  readonly isOnline = this.connectivity.isOnline;

  /** Computed: hay cambios pendientes de sincronizar */
  readonly hasPendingChanges = computed(() => this.pendingOperations().length > 0);

  /** Computed: cantidad de operaciones pendientes (para badges) */
  readonly pendingCount = computed(() => this.pendingOperations().length);

  /** Computed: operaciones en estado CONFLICTO (requieren intervencion del usuario) */
  readonly conflictos = computed(() =>
    this.pendingOperations().filter(op => op.estado === 'CONFLICTO')
  );

  private syncIntervalId?: ReturnType<typeof setInterval>;
  private readonly deviceId: string;

  constructor() {
    // El deviceId se genera una vez y se persiste en localStorage
    this.deviceId = this.obtenerOCrearDeviceId();

    // Efecto reactivo: cuando vuelve la conexion, disparar sync automaticamente
    effect(() => {
      if (this.connectivity.isOnline() && this.connectivity.puedeSync()) {
        void this.sincronizarSiHayPendientes();
      }
    }, { allowSignalWrites: true });

    // Cargar operaciones pendientes al iniciar
    void this.cargarOperacionesPendientes();

    // Sincronizacion periodica cada 2 minutos cuando hay conexion
    this.iniciarSyncPeriodico();
  }

  // -------------------------------------------------------------------------
  // API publica
  // -------------------------------------------------------------------------

  /**
   * Encola una nueva operacion para sincronizacion posterior.
   * La operacion se persiste en RxDB inmediatamente (offline-first).
   */
  async encolarOperacion(
    tipo: TipoOperacion,
    entidadId: string,
    entidadTipo: OperacionPendiente['entidadTipo'],
    payload: Record<string, unknown>,
    rowVersionAnterior: string | null = null
  ): Promise<string> {
    const operacionId = uuidv4();
    const ahora = new Date().toISOString();

    const doc: OperacionPendienteDocType = {
      operacionId,
      tipo,
      entidadId,
      entidadTipo,
      payloadJson: JSON.stringify(payload),
      clientTimestamp: ahora,
      rowVersionAnterior,
      estado: 'PENDIENTE',
      intentos: 0,
      ultimoIntento: null,
      errorDetalle: null,
      serverDataJson: null
    };

    await this.rxdb.operacionesPendientes.insert(doc);
    await this.cargarOperacionesPendientes();

    // Si hay conexion, intentar sync inmediatamente
    if (this.connectivity.puedeSync()) {
      void this.sincronizar();
    }

    return operacionId;
  }

  /**
   * Ejecuta un ciclo completo de sincronizacion: PUSH + PULL.
   * Retorna el resultado del ciclo con estadisticas.
   */
  async sincronizar(): Promise<SyncResult> {
    if (this.syncStatus() === 'syncing') {
      return this.resultadoSinConexion('Sincronizacion ya en progreso');
    }

    if (!this.connectivity.puedeSync()) {
      this.syncStatus.set('offline');
      return this.resultadoSinConexion('Sin conexion con el servidor');
    }

    this.syncStatus.set('syncing');

    try {
      const resultado = await this.ejecutarPush();
      // Despues del push exitoso, ejecutar pull de datos frescos
      await this.ejecutarPull();

      this.lastSyncTime.set(new Date());
      this.syncStatus.set('idle');
      return resultado;
    } catch (error) {
      this.syncStatus.set('error');
      const mensaje = error instanceof Error ? error.message : 'Error desconocido';
      return this.resultadoSinConexion(mensaje);
    }
  }

  /**
   * Fuerza un reintento manual de operaciones FALLIDAS.
   * El usuario puede usar este metodo desde la UI.
   */
  async reintentarFallidas(): Promise<void> {
    const fallidas = await this.rxdb.operacionesPendientes
      .find({ selector: { estado: 'FALLIDA' } })
      .exec();

    for (const op of fallidas) {
      await op.patch({ estado: 'PENDIENTE', intentos: 0, errorDetalle: null });
    }

    await this.cargarOperacionesPendientes();
    await this.sincronizar();
  }

  // -------------------------------------------------------------------------
  // Metodos privados
  // -------------------------------------------------------------------------

  /** Ejecuta el PUSH: envia operaciones PENDIENTES al servidor en batches */
  private async ejecutarPush(): Promise<SyncResult> {
    const pendientes = await this.rxdb.operacionesPendientes
      .find({
        selector: { estado: { $in: ['PENDIENTE', 'RESUELTA'] } },
        sort: [{ clientTimestamp: 'asc' }]
      })
      .exec();

    if (pendientes.length === 0) {
      return {
        timestamp: new Date().toISOString(),
        operacionesEnviadas: 0,
        operacionesAplicadas: 0,
        operacionesConConflicto: 0,
        operacionesRechazadas: 0,
        operacionesConError: 0,
        sinConexion: false
      };
    }

    // Marcar todas como ENVIANDO antes del request HTTP
    for (const op of pendientes) {
      await op.patch({ estado: 'ENVIANDO' });
    }

    const lastSyncTimestamp = this.lastSyncTime()?.toISOString()
      ?? new Date(0).toISOString();

    const request: SyncPushRequest = {
      deviceId: this.deviceId,
      lastSyncTimestamp,
      operaciones: pendientes.map(op => ({
        operacionId: op.operacionId,
        tipo: op.tipo as TipoOperacion,
        entidadId: op.entidadId,
        entidadTipo: op.entidadTipo,
        payload: JSON.parse(op.payloadJson) as Record<string, unknown>,
        clientTimestamp: op.clientTimestamp,
        rowVersionAnterior: op.rowVersionAnterior,
        intentoNumero: op.intentos + 1
      }))
    };

    let respuesta: SyncPushResponse;
    try {
      const apiResp = await firstValueFrom(
        this.http.post<ApiResponse<SyncPushResponse>>(
          `${environment.apiBaseUrl}/api/sync/push`,
          request
        )
      );
      respuesta = apiResp.data;
    } catch {
      // Si falla el HTTP, revertir todas a PENDIENTE para retry posterior
      for (const op of pendientes) {
        const nuevosIntentos = op.intentos + 1;
        const nuevoEstado = nuevosIntentos >= environment.sync.maxRetries ? 'FALLIDA' : 'PENDIENTE';
        await op.patch({
          estado: nuevoEstado,
          intentos: nuevosIntentos,
          ultimoIntento: new Date().toISOString()
        });
      }
      await this.cargarOperacionesPendientes();
      throw new Error('Error de red al sincronizar');
    }

    // Procesar resultados individuales de cada operacion
    const stats = { aplicadas: 0, conflictos: 0, rechazadas: 0, errores: 0 };

    for (const resultado of respuesta.resultados) {
      const doc = pendientes.find(p => p.operacionId === resultado.operacionId);
      if (!doc) continue;

      switch (resultado.estado) {
        case 'APLICADA':
        case 'DUPLICADA':
          // Operacion exitosa: eliminar de la cola
          await doc.remove();
          stats.aplicadas++;
          break;

        case 'CONFLICTO':
          // Guardar datos del servidor para presentar al usuario
          await doc.patch({
            estado: 'CONFLICTO',
            serverDataJson: JSON.stringify(resultado.error?.serverData ?? {}),
            errorDetalle: resultado.error?.message ?? null
          });
          stats.conflictos++;
          break;

        case 'RECHAZADA':
          await doc.patch({
            estado: 'RECHAZADA',
            errorDetalle: resultado.error?.message ?? 'Rechazada por el servidor'
          });
          stats.rechazadas++;
          break;

        case 'ERROR':
        default: {
          const nuevosIntentos = doc.intentos + 1;
          const nuevoEstado = nuevosIntentos >= environment.sync.maxRetries ? 'FALLIDA' : 'PENDIENTE';
          await doc.patch({
            estado: nuevoEstado,
            intentos: nuevosIntentos,
            ultimoIntento: new Date().toISOString(),
            errorDetalle: resultado.error?.message ?? 'Error del servidor'
          });
          stats.errores++;
          break;
        }
      }
    }

    await this.cargarOperacionesPendientes();

    return {
      timestamp: respuesta.serverTimestamp,
      operacionesEnviadas: pendientes.length,
      operacionesAplicadas: stats.aplicadas,
      operacionesConConflicto: stats.conflictos,
      operacionesRechazadas: stats.rechazadas,
      operacionesConError: stats.errores,
      sinConexion: false
    };
  }

  /**
   * Ejecuta el PULL: descarga todos los datos del servidor y los persiste en RxDB.
   * Usa upsert para que datos locales pendientes no se sobreescriban si tienen syncStatus PENDIENTE.
   */
  private async ejecutarPull(): Promise<void> {
    const desde = this.lastSyncTime()?.toISOString() ?? new Date(0).toISOString();

    try {
      const apiResp = await firstValueFrom(
        this.http.get<ApiResponse<SyncPullResponse>>(
          `${environment.apiBaseUrl}/api/sync/pull?desde=${encodeURIComponent(desde)}`
        )
      );
      const datos = apiResp.data;

      // Upsert en paralelo todas las colecciones
      await Promise.all([
        this.upsertLotes(datos.lotes),
        this.upsertInsumos(datos.insumos),
        this.upsertAplicaciones(datos.aplicaciones),
        this.upsertCosechas(datos.cosechas),
        this.upsertCostos(datos.costos)
      ]);
    } catch {
      // Pull opcional: si falla, los datos locales siguen siendo validos
      console.warn('[SyncService] Pull fallo. Los datos locales siguen siendo validos.');
    }
  }

  /** Upsert de lotes del servidor en RxDB. Solo actualiza si el doc local esta SYNCED. */
  private async upsertLotes(lotes: SyncLoteDto[]): Promise<void> {
    const ahora = new Date().toISOString();
    for (const l of lotes) {
      const existente = await this.rxdb.lotes.findOne(l.id).exec();
      // No sobreescribir documentos con cambios locales pendientes
      if (existente && existente.syncStatus === 'PENDIENTE') continue;

      await this.rxdb.lotes.upsert({
        id: l.id,
        fincaId: l.fincaId,
        nombre: l.nombre,
        cultivo: l.cultivo,
        areaHa: l.areaHa,
        ubicacionLatitud: l.ubicacionLatitud,
        ubicacionLongitud: l.ubicacionLongitud,
        fechaInicioSiembra: l.fechaInicioSiembra,
        activo: l.activo,
        rowVersion: l.rowVersion,
        createdAt: existente?.createdAt ?? ahora,
        updatedAt: l.updatedAt,
        creadoOffline: false,
        clientTimestamp: existente?.clientTimestamp ?? ahora,
        syncStatus: 'SYNCED'
      });
    }
  }

  /** Upsert del catalogo de insumos (compartido, sin logica de conflicto). */
  private async upsertInsumos(insumos: SyncInsumoDto[]): Promise<void> {
    for (const i of insumos) {
      await this.rxdb.insumos.upsert({
        id: i.id,
        nombreComercial: i.nombreComercial,
        ingredienteActivo: i.ingredienteActivo,
        fabricante: '',
        registroAgrocalidad: '',
        tipoProducto: i.tipoProducto,
        categoriaToxico: i.categoriaToxico,
        concentracionValor: i.concentracionValor,
        concentracionUnidad: i.concentracionUnidad,
        dosisMinima: i.dosisMinima,
        dosisMaxima: i.dosisMaxima,
        unidadDosis: i.unidadDosis,
        periodoCarenciaJson: i.periodoCarenciaJson,
        activo: i.activo,
        updatedAt: i.updatedAt
      });
    }
  }

  /** Upsert de aplicaciones. No sobreescribe locales pendientes. */
  private async upsertAplicaciones(aplicaciones: SyncAplicacionDto[]): Promise<void> {
    const ahora = new Date().toISOString();
    for (const a of aplicaciones) {
      const existente = await this.rxdb.aplicaciones.findOne(a.id).exec();
      if (existente && existente.syncStatus === 'PENDIENTE') continue;

      await this.rxdb.aplicaciones.upsert({
        id: a.id,
        loteId: a.loteId,
        insumoId: a.insumoId,
        fechaAplicacion: a.fechaAplicacion,
        dosisCantidad: a.dosisCantidad,
        dosisUnidad: a.dosisUnidad,
        areaAplicadaHa: a.areaAplicadaHa,
        metodoAplicacion: a.metodoAplicacion,
        operadorNombre: a.operadorNombre,
        coordenadasLatitud: null,
        coordenadasLongitud: null,
        observaciones: null,
        costoTotal: a.costoTotal,
        diasCarencia: a.diasCarenciaAplicables,
        fechaFinCarencia: a.fechaFinCarencia,
        creadoOffline: a.creadoOffline,
        clientTimestamp: a.clientTimestamp,
        rowVersion: a.rowVersion,
        createdAt: existente?.createdAt ?? ahora,
        loteNombre: a.loteNombre || null,
        insumoNombre: a.insumoNombre || null,
        syncStatus: 'SYNCED'
      });
    }
  }

  /** Upsert de cosechas. No sobreescribe locales pendientes. */
  private async upsertCosechas(cosechas: SyncCosechaDto[]): Promise<void> {
    const ahora = new Date().toISOString();
    for (const c of cosechas) {
      const existente = await this.rxdb.cosechas.findOne(c.id).exec();
      if (existente && existente.syncStatus === 'PENDIENTE') continue;

      await this.rxdb.cosechas.upsert({
        id: c.id,
        loteId: c.loteId,
        fechaCosecha: c.fechaCosecha,
        pesoTotalKg: c.pesoTotalKg,
        calidad: c.calidadGrado as 'PRIMERA' | 'SEGUNDA' | 'TERCERA' | 'DESCARTE',
        precioVentaKg: c.precioVentaKg,
        comprador: c.comprador,
        observaciones: null,
        ingresoTotal: c.ingresoTotal,
        bloqueadaPorCarencia: c.bloqueadaPorCarencia,
        creadoOffline: c.creadoOffline,
        clientTimestamp: existente?.clientTimestamp ?? ahora,
        rowVersion: c.rowVersion,
        syncStatus: 'SYNCED'
      });
    }
  }

  /** Upsert de costos. No sobreescribe locales pendientes. */
  private async upsertCostos(costos: SyncCostoDto[]): Promise<void> {
    const ahora = new Date().toISOString();
    for (const c of costos) {
      const existente = await this.rxdb.costos.findOne(c.id).exec();
      if (existente && existente.syncStatus === 'PENDIENTE') continue;

      await this.rxdb.costos.upsert({
        id: c.id,
        loteId: c.loteId,
        fecha: c.fecha,
        categoria: c.categoria,
        descripcion: c.descripcion,
        monto: c.monto,
        aplicacionId: c.aplicacionId,
        cosechaId: c.cosechaId,
        creadoOffline: c.creadoOffline,
        clientTimestamp: existente?.clientTimestamp ?? ahora,
        eliminado: false,
        rowVersion: c.rowVersion,
        syncStatus: 'SYNCED'
      });
    }
  }

  /** Carga el estado actual de operaciones pendientes desde RxDB al signal */
  private async cargarOperacionesPendientes(): Promise<void> {
    const docs = await this.rxdb.operacionesPendientes
      .find({
        selector: {
          estado: { $nin: ['APLICADA', 'DUPLICADA'] }
        }
      })
      .exec();

    const operaciones: OperacionPendiente[] = docs.map(doc => ({
      operacionId: doc.operacionId,
      tipo: doc.tipo as TipoOperacion,
      entidadId: doc.entidadId,
      entidadTipo: doc.entidadTipo as OperacionPendiente['entidadTipo'],
      payload: JSON.parse(doc.payloadJson) as Record<string, unknown>,
      clientTimestamp: doc.clientTimestamp,
      rowVersionAnterior: doc.rowVersionAnterior,
      estado: doc.estado as OperacionPendiente['estado'],
      intentos: doc.intentos,
      ultimoIntento: doc.ultimoIntento,
      errorDetalle: doc.errorDetalle,
      serverData: doc.serverDataJson ? JSON.parse(doc.serverDataJson) as Record<string, unknown> : null
    }));

    this.pendingOperations.set(operaciones);
  }

  /** Sincroniza solo si hay operaciones pendientes */
  private async sincronizarSiHayPendientes(): Promise<void> {
    if (this.hasPendingChanges()) {
      await this.sincronizar();
    }
  }

  /** Inicia el timer de sincronizacion periodica */
  private iniciarSyncPeriodico(): void {
    this.syncIntervalId = setInterval(() => {
      if (this.connectivity.puedeSync()) {
        void this.sincronizar();
      }
    }, environment.sync.intervalMs);
  }

  /** Obtiene o genera el ID unico del dispositivo, persistido en localStorage */
  private obtenerOCrearDeviceId(): string {
    const key = environment.deviceIdStorageKey;
    let id = localStorage.getItem(key);
    if (!id) {
      id = uuidv4();
      localStorage.setItem(key, id);
    }
    return id;
  }

  /** Construye un SyncResult de fallo sin conexion */
  private resultadoSinConexion(motivo: string): SyncResult {
    return {
      timestamp: new Date().toISOString(),
      operacionesEnviadas: 0,
      operacionesAplicadas: 0,
      operacionesConConflicto: 0,
      operacionesRechazadas: 0,
      operacionesConError: 0,
      sinConexion: true,
      error: motivo
    };
  }
}

// Importacion circular evitada: ApiResponse se define en api.service.ts
// pero se declara aqui como tipo local para no crear dependencia circular

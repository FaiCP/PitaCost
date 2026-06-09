// src/app/core/models/sync.model.ts
// Modelos del Bounded Context: Sincronizacion
// Alineados exactamente con api-contract.md seccion 4 y offline-sync-flow.md

/** Tipos de operacion sincronizables con el servidor */
export type TipoOperacion =
  | 'CREAR_APLICACION'
  | 'ACTUALIZAR_APLICACION'
  | 'CREAR_COSECHA'
  | 'CREAR_COSTO'
  | 'ACTUALIZAR_COSTO'
  | 'ELIMINAR_COSTO'
  | 'CREAR_LOTE'
  | 'ACTUALIZAR_LOTE';

/**
 * Estados del ciclo de vida de una operacion en la cola offline.
 * Diagrama completo en offline-sync-flow.md
 */
export type EstadoOperacion =
  | 'PENDIENTE'    // Estado inicial, esperando conexion
  | 'ENVIANDO'     // En proceso de envio HTTP (estado efimero)
  | 'APLICADA'     // Servidor proceso exitosamente (se elimina de la cola)
  | 'DUPLICADA'    // Ya fue procesada antes (idempotencia - se elimina)
  | 'CONFLICTO'    // RowVersion mismatch - requiere intervencion
  | 'RECHAZADA'    // Validacion de negocio fallida en servidor
  | 'RESUELTA'     // Conflicto resuelto, lista para re-sync
  | 'FALLIDA';     // Excedio MAX_RETRIES (5 intentos)

/**
 * OperacionPendiente: unidad atomica de sincronizacion.
 * Se persiste en RxDB (IndexedDB) hasta ser confirmada por el servidor.
 * El operacionId es la idempotency key.
 */
export interface OperacionPendiente {
  /** UUID generado en cliente - idempotency key */
  operacionId: string;
  tipo: TipoOperacion;
  /** ID de la entidad afectada (tambien generado en cliente para CREARs) */
  entidadId: string;
  /** Nombre del tipo de entidad de dominio */
  entidadTipo: 'AplicacionQuimico' | 'Cosecha' | 'CostoLote' | 'Lote';
  /** JSON del payload especifico segun el tipo de operacion */
  payload: Record<string, unknown>;
  /** Momento en que el agricultor realizo la accion (UTC-5) */
  clientTimestamp: string;       // ISO 8601
  /** RowVersion conocido por el cliente (requerido para ACTUALIZAR y ELIMINAR) */
  rowVersionAnterior: string | null;
  estado: EstadoOperacion;
  intentos: number;
  ultimoIntento: string | null;  // ISO 8601
  errorDetalle: string | null;
  /** Datos del servidor en caso de CONFLICTO (para merge manual) */
  serverData: Record<string, unknown> | null;
}

/** Resultado individual de procesar una operacion en el servidor */
export interface ResultadoOperacion {
  operacionId: string;
  estado: 'APLICADA' | 'DUPLICADA' | 'CONFLICTO' | 'RECHAZADA' | 'ERROR';
  entidadId: string;
  rowVersionNuevo: string | null;
  error: ErrorOperacion | null;
}

/** Detalle de error de una operacion rechazada o en conflicto */
export interface ErrorOperacion {
  code: string;
  message: string;
  /** Datos actuales del servidor (para resolucion de conflictos) */
  serverData?: Record<string, unknown>;
}

/** Request para el endpoint POST /api/sync/push */
export interface SyncPushRequest {
  deviceId: string;
  lastSyncTimestamp: string;     // ISO 8601
  operaciones: OperacionPendienteRequest[];
}

/** Formato serializado de una operacion para enviar al servidor */
export interface OperacionPendienteRequest {
  operacionId: string;
  tipo: TipoOperacion;
  entidadId: string;
  entidadTipo: string;
  payload: Record<string, unknown>;
  clientTimestamp: string;
  rowVersionAnterior: string | null;
  intentoNumero: number;
}

/** Respuesta del endpoint POST /api/sync/push */
export interface SyncPushResponse {
  deviceId: string;
  serverTimestamp: string;       // ISO 8601
  resultados: ResultadoOperacion[];
  datosActualizados: DatosActualizadosSync;
}

/** Datos frescos del servidor enviados junto con la respuesta de sync */
export interface DatosActualizadosSync {
  insumosModificados: number;
  preciosMercadoActualizados: boolean;
  ultimoPrecio: PrecioMercadoSync | null;
}

/** Precio de mercado actualizado incluido en la respuesta de sync */
export interface PrecioMercadoSync {
  cultivo: string;
  precioKg: number;
  fecha: string;
}

/** Resultado general de un ciclo de sincronizacion (push + pull) */
export interface SyncResult {
  timestamp: string;
  operacionesEnviadas: number;
  operacionesAplicadas: number;
  operacionesConConflicto: number;
  operacionesRechazadas: number;
  operacionesConError: number;
  sinConexion: boolean;
  error?: string;
}

/** Estado del motor de sincronizacion */
export type EstadoSync = 'idle' | 'syncing' | 'error' | 'offline';

/** Calidad de la conexion detectada via ping real */
export type CalidadConexion = 'good' | 'slow' | 'offline';

/** Informacion del dispositivo enviada al servidor */
export interface DeviceInfo {
  deviceId: string;
  platform: string;
  appVersion: string;
}

// -----------------------------------------------------------------------
// Tipos para la respuesta del endpoint GET /api/sync/pull
// Estructura exacta del servidor (camelCase por configuracion CamelCase en .NET)
// -----------------------------------------------------------------------

export interface SyncPullResponse {
  serverTimestamp: string;
  lotes: SyncLoteDto[];
  insumos: SyncInsumoDto[];
  aplicaciones: SyncAplicacionDto[];
  cosechas: SyncCosechaDto[];
  costos: SyncCostoDto[];
}

export interface SyncLoteDto {
  id: string;
  fincaId: string;
  nombre: string;
  cultivo: string;
  areaHa: number;
  ubicacionLatitud?: number;
  ubicacionLongitud?: number;
  fechaInicioSiembra: string;
  activo: boolean;
  rowVersion: string | null;
  updatedAt: string | null;
}

export interface SyncInsumoDto {
  id: string;
  nombreComercial: string;
  ingredienteActivo: string;
  tipoProducto: string;
  categoriaToxico: string;
  concentracionValor: number;
  concentracionUnidad: string;
  dosisMinima: number;
  dosisMaxima: number;
  unidadDosis: string;
  periodoCarenciaJson: string;
  activo: boolean;
  updatedAt: string;
}

export interface SyncAplicacionDto {
  id: string;
  loteId: string;
  insumoId: string;
  fechaAplicacion: string;
  dosisCantidad: number;
  dosisUnidad: string;
  areaAplicadaHa: number;
  metodoAplicacion: string;
  operadorNombre: string;
  costoTotal: number;
  diasCarenciaAplicables: number;
  fechaFinCarencia: string | null;
  creadoOffline: boolean;
  clientTimestamp: string;
  loteNombre: string;
  insumoNombre: string;
  rowVersion: string | null;
}

export interface SyncCosechaDto {
  id: string;
  loteId: string;
  fechaCosecha: string;
  pesoTotalKg: number;
  calidadGrado: string;
  comprador: string | null;
  precioVentaKg: number | null;
  ingresoTotal: number;
  bloqueadaPorCarencia: boolean;
  creadoOffline: boolean;
  rowVersion: string | null;
}

export interface SyncCostoDto {
  id: string;
  loteId: string;
  fecha: string;
  categoria: string;
  descripcion: string;
  monto: number;
  aplicacionId: string | null;
  cosechaId: string | null;
  creadoOffline: boolean;
  rowVersion: string | null;
}

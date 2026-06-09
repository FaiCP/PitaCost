// src/app/core/models/aplicacion.model.ts
// Modelos del Bounded Context: Aplicaciones
// Alineados exactamente con api-contract.md seccion 1 y bounded-contexts.md seccion 2

import { UnidadDosis } from './insumo.model';

// Re-exportamos el tipo compartido para que el modulo de aplicaciones sea autonomo
export type { UnidadDosis };

/** Metodos de aplicacion de insumos */
export type MetodoAplicacion =
  | 'FUMIGACION'
  | 'DRENCH'
  | 'INYECCION'
  | 'GRANULAR'
  | 'OTRO';

/** Value Object: Dosis de aplicacion */
export interface Dosis {
  cantidad: number;
  unidad: UnidadDosis;
}

/** Informacion del periodo de carencia resultante de una aplicacion */
export interface ResultadoCarencia {
  diasCarencia: number;
  fechaFinCarencia: string;  // ISO 8601
  cosechaBloqueada: boolean;
}

/** Coordenadas GPS (alias del modelo compartido para desacoplar) */
export interface CoordenadasGps {
  latitud: number;
  longitud: number;
}

/**
 * Entidad AplicacionQuimico - Aggregate Root del contexto Aplicaciones.
 * El id es un UUID generado en cliente para soporte offline (idempotencia).
 */
export interface AplicacionQuimico {
  /** UUID generado en cliente antes de sincronizar */
  id: string;
  loteId: string;
  insumoId: string;
  fechaAplicacion: string;        // ISO 8601 con timezone Ecuador (UTC-5)
  dosis: Dosis;
  areaAplicadaHa: number;
  metodoAplicacion: MetodoAplicacion;
  operadorNombre: string;
  coordenadasGps?: CoordenadasGps;
  observaciones?: string;
  costoTotal: number;             // USD, 2 decimales
  /** Indica si fue registrado sin conexion a internet */
  creadoOffline: boolean;
  /** Timestamp del cliente al crear (para resolucion de conflictos LWW) */
  clientTimestamp: string;        // ISO 8601
  rowVersion?: string;
  createdAt?: string;
  // Datos calculados por el servidor (disponibles post-sync)
  periodoCarencia?: ResultadoCarencia;
  // Datos de presentacion enriquecidos por el servidor
  loteNombre?: string;
  insumoNombre?: string;
  // Estado de sincronizacion local
  syncStatus?: EstadoSyncAplicacion;
}

/** Estado de la aplicacion en el ciclo de sincronizacion offline */
export type EstadoSyncAplicacion =
  | 'SYNCED'
  | 'PENDIENTE'
  | 'ENVIANDO'
  | 'CONFLICTO'
  | 'RECHAZADO'
  | 'FALLIDO';

/**
 * Request para crear una nueva aplicacion (POST /api/aplicaciones).
 * Es tambien el payload de la OperacionPendiente cuando tipo = CREAR_APLICACION.
 */
export interface CrearAplicacionRequest {
  id: string;
  loteId: string;
  insumoId: string;
  fechaAplicacion: string;
  dosis: Dosis;
  areaAplicadaHa: number;
  metodoAplicacion: MetodoAplicacion;
  operadorNombre: string;
  coordenadasGps?: CoordenadasGps;
  observaciones?: string;
  costoTotal: number;
  creadoOffline: boolean;
  clientTimestamp: string;
}

/** Respuesta del servidor al registrar una aplicacion (201 Created) */
export interface AplicacionResponse {
  id: string;
  loteId: string;
  loteNombre: string;
  insumoId: string;
  insumoNombre: string;
  fechaAplicacion: string;
  dosis: Dosis;
  areaAplicadaHa: number;
  metodoAplicacion: MetodoAplicacion;
  operadorNombre: string;
  costoTotal: number;
  periodoCarencia: ResultadoCarencia;
  rowVersion: string;
  createdAt: string;
}

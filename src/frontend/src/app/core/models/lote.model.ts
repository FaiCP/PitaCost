// src/app/core/models/lote.model.ts
// Modelos del Bounded Context: Costos (Lote y Finca)
// Alineados con bounded-contexts.md y api-contract.md

/** Coordenadas GPS del lote (rango Ecuador continental + Galapagos) */
export interface CoordenadasGps {
  latitud: number;   // Rango: [-5, 2]
  longitud: number;  // Rango: [-92, -75]
}

/** Finca: propiedad agricola completa del agricultor */
export interface Finca {
  id: string;
  nombre: string;
  provincia: string;
  canton: string;
  parroquia: string;
  areaTotalHa: number;
  /** Cantidad de lotes en la finca (calculado en API) */
  lotes?: number;
}

/** Lote: subdivision de una finca dedicada a un cultivo especifico */
export interface Lote {
  id: string;
  fincaId: string;
  nombre: string;
  /** Cultivo actual: Banano, Cacao, etc. */
  cultivo: string;
  areaHa: number;
  ubicacion?: CoordenadasGps;
  /** Inicio del ciclo de cultivo actual (ISO 8601 date) */
  fechaInicioSiembra: string;
  activo: boolean;
  rowVersion?: string;
  // Campos de auditoria
  createdAt?: string;
  updatedAt?: string;
  // Campos de control offline
  creadoOffline?: boolean;
  clientTimestamp?: string;
  syncStatus?: EstadoSyncEntidad;
}

/** Estado de sincronizacion de una entidad local */
export type EstadoSyncEntidad = 'SYNCED' | 'PENDIENTE' | 'CONFLICTO' | 'RECHAZADO';

/** DTO simplificado de lote para selectores y listas */
export interface LoteResumen {
  id: string;
  nombre: string;
  cultivo: string;
  areaHa: number;
  activo: boolean;
}

/** Alerta de rentabilidad/carencia retornada por el dashboard */
export interface AlertaLote {
  tipo: 'PERIODO_CARENCIA' | 'COSECHA_BLOQUEADA' | 'COSTO_ELEVADO' | 'SIN_DATOS';
  mensaje: string;
  severidad: 'CRITICA' | 'ADVERTENCIA' | 'INFO';
}

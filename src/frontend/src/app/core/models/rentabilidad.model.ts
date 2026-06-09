// src/app/core/models/rentabilidad.model.ts
// Modelos del Bounded Context: Costos - Dashboard de Rentabilidad
// Alineados exactamente con api-contract.md seccion 2

import { AlertaLote } from './lote.model';

/** Categorias de costo permitidas en el sistema */
export type CategoriaCosto =
  | 'INSUMOS_QUIMICOS'
  | 'MANO_DE_OBRA'
  | 'TRANSPORTE'
  | 'RIEGO'
  | 'MAQUINARIA'
  | 'OTROS';

/** Item detallado dentro de una categoria de costo */
export interface ItemCosto {
  descripcion: string;
  total: number;
}

/** Desglose de costos por categoria */
export interface DesgloseCosto {
  categoria: CategoriaCosto;
  total: number;
  porcentaje: number;
  items: ItemCosto[];
}

/** Detalle de una venta individual */
export interface DetalleVenta {
  fecha: string;            // YYYY-MM-DD
  comprador: string;
  kgVendidos: number;
  precioKg: number;
  totalVenta: number;
}

/** Resumen de ingresos del periodo */
export interface ResumenIngresos {
  totalVentas: number;
  precioPromedioKg: number;
  totalKgCosechados: number;
  detalleVentas: DetalleVenta[];
}

/** Resumen de costos del periodo */
export interface ResumenCostos {
  totalCostos: number;
  costoPorHa: number;
  costoPorKg: number;
  desglose: DesgloseCosto[];
}

/** Indicadores de rentabilidad calculados por el servidor */
export interface IndicadoresRentabilidad {
  utilidadBruta: number;
  margenBruto: number;       // Porcentaje
  utilidadPorHa: number;
  utilidadPorKg: number;
  roi: number;               // Return On Investment en porcentaje
}

/** Periodo de consulta */
export interface PeriodoConsulta {
  desde: string;             // YYYY-MM-DD
  hasta: string;             // YYYY-MM-DD
}

/** Respuesta completa del dashboard de rentabilidad (GET /api/lotes/{id}/rentabilidad) */
export interface DashboardRentabilidad {
  loteId: string;
  loteNombre: string;
  cultivoActual: string;
  areaHa: number;
  periodo: PeriodoConsulta;
  ingresos: ResumenIngresos;
  costos: ResumenCostos;
  rentabilidad: IndicadoresRentabilidad;
  alertas: AlertaLote[];
}

/**
 * KPIs calculados localmente desde RxDB cuando el dispositivo esta offline.
 * Calculo basado en la formula de bounded-contexts.md seccion 4.
 */
export interface KpisLocales {
  totalIngresos: number;
  totalCostos: number;
  utilidadBruta: number;
  margenBruto: number | null;    // null si totalIngresos = 0
  roi: number | null;             // null si totalCostos = 0
  utilidadPorHa: number | null;  // null si area = 0
  /** Indica que los KPIs fueron calculados offline (pueden ser incompletos) */
  esCalculoOffline: boolean;
}

/** Costo local registrado en RxDB */
export interface CostoLote {
  id: string;
  loteId: string;
  fecha: string;             // YYYY-MM-DD
  categoria: CategoriaCosto;
  descripcion: string;
  monto: number;             // USD
  aplicacionId?: string;
  cosechaId?: string;
  creadoOffline: boolean;
  clientTimestamp: string;
  eliminado: boolean;
  rowVersion?: string;
  syncStatus?: string;
}

// src/app/core/models/insumo.model.ts
// Modelos del Bounded Context: Agroquimicos
// Alineados exactamente con api-contract.md seccion 3 y bounded-contexts.md seccion 1

/** Unidades de medida validas para dosis de insumos */
export type UnidadDosis = 'L_HA' | 'KG_HA' | 'ML_HA' | 'G_HA' | 'CC_HA';

/** Tipo de producto agroquimico segun clasificacion Agrocalidad */
export type TipoProducto =
  | 'FUNGICIDA'
  | 'HERBICIDA'
  | 'INSECTICIDA'
  | 'FERTILIZANTE'
  | 'NEMATICIDA'
  | 'OTRO';

/** Categoria toxicologica segun Agrocalidad */
export type CategoriaToxico = 'I' | 'II' | 'III' | 'IV';

/** Concentracion del principio activo */
export interface Concentracion {
  valor: number;
  unidad: 'PORCENTAJE' | 'G_L' | 'G_KG';
}

/** Dosis recomendada del insumo */
export interface DosisRecomendada {
  minima: number;
  maxima: number;
  unidad: UnidadDosis;
}

/** Periodo de carencia especifico por cultivo */
export interface CarenciaPorCultivo {
  cultivo: string;
  diasEspecificos: number;
}

/** Informacion de periodo de carencia del insumo */
export interface PeriodoCarenciaInsumo {
  dias: number;
  cultivos: CarenciaPorCultivo[];
}

/** Estado de carencia activa para un lote especifico */
export interface EstadoCarenciaLote {
  loteId: string;
  loteNombre: string;
  carenciaActiva: boolean;
  ultimaAplicacion: string;       // ISO 8601
  fechaFinCarencia: string;       // ISO 8601
  diasRestantes: number;
  cosechaBloqueada: boolean;
}

/** Catalogo de insumos agroquimicos */
export interface Insumo {
  id: string;
  nombreComercial: string;
  ingredienteActivo: string;
  fabricante: string;
  registroAgrocalidad: string;
  concentracion: Concentracion;
  tipoProducto: TipoProducto;
  categoriaToxico: CategoriaToxico;
  periodoCarencia: PeriodoCarenciaInsumo;
  dosisRecomendada: DosisRecomendada;
  activo: boolean;
  rowVersion?: string;
}

/** Respuesta completa del endpoint GET /api/insumos/{id}/periodo-carencia */
export interface InsumoConCarencia extends Insumo {
  /** Estado de carencia activa para el lote consultado (null si no se envio loteId) */
  estadoCarenciaLote: EstadoCarenciaLote | null;
}

/** DTO simplificado para el selector de insumos en el formulario de aplicacion */
export interface InsumoResumen {
  id: string;
  nombreComercial: string;
  ingredienteActivo: string;
  tipoProducto: TipoProducto;
  dosisRecomendada: DosisRecomendada;
  diasCarencia: number;
}

// src/app/core/database/rxdb-schemas.ts
// Schemas de colecciones RxDB. Deben coincidir con las interfaces de los modelos.
// RxDB utiliza JSON Schema Draft-07 con extensions propias.
// primaryKey identifica el campo que actua como PK en IndexedDB.

import type { RxJsonSchema } from 'rxdb';

// -----------------------------------------------------------------------
// Schema: Lotes
// -----------------------------------------------------------------------
export interface LoteDocType {
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
  createdAt: string;
  updatedAt: string | null;
  creadoOffline: boolean;
  clientTimestamp: string;
  syncStatus: string;
}

export const loteSchema: RxJsonSchema<LoteDocType> = {
  title: 'lote',
  version: 0,
  type: 'object',
  primaryKey: 'id',
  properties: {
    id:                   { type: 'string', maxLength: 36 },
    fincaId:              { type: 'string', maxLength: 36 },
    nombre:               { type: 'string', maxLength: 200 },
    cultivo:              { type: 'string', maxLength: 100 },
    areaHa:               { type: 'number' },
    ubicacionLatitud:     { type: 'number' },
    ubicacionLongitud:    { type: 'number' },
    fechaInicioSiembra:   { type: 'string' },
    activo:               { type: 'boolean', default: true },
    rowVersion:           { type: ['string', 'null'] },
    createdAt:            { type: 'string' },
    updatedAt:            { type: ['string', 'null'] },
    creadoOffline:        { type: 'boolean', default: false },
    clientTimestamp:      { type: 'string' },
    syncStatus:           { type: 'string', maxLength: 20, default: 'SYNCED', enum: ['SYNCED', 'PENDIENTE', 'CONFLICTO', 'RECHAZADO'] }
  },
  required: ['id', 'fincaId', 'nombre', 'cultivo', 'areaHa', 'fechaInicioSiembra', 'activo', 'creadoOffline', 'clientTimestamp', 'syncStatus'],
  indexes: ['fincaId', 'activo', 'syncStatus']
};

// -----------------------------------------------------------------------
// Schema: Insumos (catalogo de agroquimicos)
// -----------------------------------------------------------------------
export interface InsumoDocType {
  id: string;
  nombreComercial: string;
  ingredienteActivo: string;
  fabricante: string;
  registroAgrocalidad: string;
  tipoProducto: string;
  categoriaToxico: string;
  concentracionValor: number;
  concentracionUnidad: string;
  dosisMinima: number;
  dosisMaxima: number;
  unidadDosis: string;
  /** JSON serializado de CarenciaPorCultivo[] */
  periodoCarenciaJson: string;
  activo: boolean;
  updatedAt: string;
}

export const insumoSchema: RxJsonSchema<InsumoDocType> = {
  title: 'insumo',
  version: 0,
  type: 'object',
  primaryKey: 'id',
  properties: {
    id:                     { type: 'string', maxLength: 36 },
    nombreComercial:        { type: 'string', maxLength: 200 },
    ingredienteActivo:      { type: 'string', maxLength: 200 },
    fabricante:             { type: 'string', maxLength: 200 },
    registroAgrocalidad:    { type: 'string', maxLength: 50 },
    tipoProducto:           { type: 'string', maxLength: 50 },
    categoriaToxico:        { type: 'string' },
    concentracionValor:     { type: 'number' },
    concentracionUnidad:    { type: 'string' },
    dosisMinima:            { type: 'number' },
    dosisMaxima:            { type: 'number' },
    unidadDosis:            { type: 'string' },
    periodoCarenciaJson:    { type: 'string' },
    activo:                 { type: 'boolean', default: true },
    updatedAt:              { type: 'string' }
  },
  required: ['id', 'nombreComercial', 'ingredienteActivo', 'tipoProducto', 'periodoCarenciaJson', 'activo', 'updatedAt'],
  indexes: ['nombreComercial', 'tipoProducto', 'activo']
};

// -----------------------------------------------------------------------
// Schema: Aplicaciones de quimicos
// -----------------------------------------------------------------------
export interface AplicacionDocType {
  id: string;
  loteId: string;
  insumoId: string;
  fechaAplicacion: string;
  dosisCantidad: number;
  dosisUnidad: string;
  areaAplicadaHa: number;
  metodoAplicacion: string;
  operadorNombre: string;
  coordenadasLatitud: number | null;
  coordenadasLongitud: number | null;
  observaciones: string | null;
  costoTotal: number;
  diasCarencia: number;
  fechaFinCarencia: string | null;
  creadoOffline: boolean;
  clientTimestamp: string;
  rowVersion: string | null;
  createdAt: string;
  loteNombre: string | null;
  insumoNombre: string | null;
  syncStatus: string;
}

export const aplicacionSchema: RxJsonSchema<AplicacionDocType> = {
  title: 'aplicacion',
  version: 0,
  type: 'object',
  primaryKey: 'id',
  properties: {
    id:                     { type: 'string', maxLength: 36 },
    loteId:                 { type: 'string', maxLength: 36 },
    insumoId:               { type: 'string', maxLength: 36 },
    fechaAplicacion:        { type: 'string', maxLength: 30 },
    dosisCantidad:          { type: 'number' },
    dosisUnidad:            { type: 'string' },
    areaAplicadaHa:         { type: 'number' },
    metodoAplicacion:       { type: 'string' },
    operadorNombre:         { type: 'string', maxLength: 200 },
    coordenadasLatitud:     { type: ['number', 'null'] },
    coordenadasLongitud:    { type: ['number', 'null'] },
    observaciones:          { type: ['string', 'null'] },
    costoTotal:             { type: 'number', default: 0 },
    diasCarencia:           { type: 'number', default: 0 },
    fechaFinCarencia:       { type: ['string', 'null'], maxLength: 30 },
    creadoOffline:          { type: 'boolean', default: false },
    clientTimestamp:        { type: 'string' },
    rowVersion:             { type: ['string', 'null'] },
    createdAt:              { type: 'string' },
    loteNombre:             { type: ['string', 'null'] },
    insumoNombre:           { type: ['string', 'null'] },
    syncStatus:             { type: 'string', maxLength: 20, default: 'PENDIENTE' }
  },
  required: ['id', 'loteId', 'insumoId', 'fechaAplicacion', 'dosisCantidad', 'dosisUnidad', 'areaAplicadaHa', 'metodoAplicacion', 'operadorNombre', 'clientTimestamp', 'syncStatus'],
  indexes: ['loteId', 'insumoId', 'fechaAplicacion', 'syncStatus']
};

// -----------------------------------------------------------------------
// Schema: Costos de lote
// -----------------------------------------------------------------------
export interface CostoDocType {
  id: string;
  loteId: string;
  fecha: string;
  categoria: string;
  descripcion: string;
  monto: number;
  aplicacionId: string | null;
  cosechaId: string | null;
  creadoOffline: boolean;
  clientTimestamp: string;
  eliminado: boolean;
  rowVersion: string | null;
  syncStatus: string;
}

export const costoSchema: RxJsonSchema<CostoDocType> = {
  title: 'costo',
  version: 0,
  type: 'object',
  primaryKey: 'id',
  properties: {
    id:             { type: 'string', maxLength: 36 },
    loteId:         { type: 'string', maxLength: 36 },
    fecha:          { type: 'string', maxLength: 10 },
    categoria:      { type: 'string', maxLength: 50 },
    descripcion:    { type: 'string', maxLength: 500 },
    monto:          { type: 'number' },
    aplicacionId:   { type: ['string', 'null'] },
    cosechaId:      { type: ['string', 'null'] },
    creadoOffline:  { type: 'boolean', default: false },
    clientTimestamp:{ type: 'string' },
    eliminado:      { type: 'boolean', default: false },
    rowVersion:     { type: ['string', 'null'] },
    syncStatus:     { type: 'string', maxLength: 20, default: 'PENDIENTE' }
  },
  required: ['id', 'loteId', 'fecha', 'categoria', 'descripcion', 'monto', 'creadoOffline', 'clientTimestamp', 'eliminado', 'syncStatus'],
  indexes: ['loteId', 'fecha', 'categoria', 'eliminado', 'syncStatus']
};

// -----------------------------------------------------------------------
// Schema: Cosechas
// -----------------------------------------------------------------------
export interface CosechaDocType {
  id: string;
  loteId: string;
  fechaCosecha: string;      // YYYY-MM-DD
  pesoTotalKg: number;
  calidad: string;           // PRIMERA, SEGUNDA, TERCERA, DESCARTE
  precioVentaKg: number | null;
  comprador: string | null;
  observaciones: string | null;
  ingresoTotal: number;      // calculado: pesoTotalKg * precioVentaKg
  bloqueadaPorCarencia: boolean;
  creadoOffline: boolean;
  clientTimestamp: string;
  rowVersion: string | null;
  syncStatus: string;
}

export const cosechaSchema: RxJsonSchema<CosechaDocType> = {
  title: 'cosecha',
  version: 0,
  type: 'object',
  primaryKey: 'id',
  properties: {
    id:                   { type: 'string', maxLength: 36 },
    loteId:               { type: 'string', maxLength: 36 },
    fechaCosecha:         { type: 'string', maxLength: 10 },
    pesoTotalKg:          { type: 'number', minimum: 0 },
    calidad:              { type: 'string', enum: ['PRIMERA', 'SEGUNDA', 'TERCERA', 'DESCARTE'] },
    precioVentaKg:        { type: ['number', 'null'] },
    comprador:            { type: ['string', 'null'], maxLength: 200 },
    observaciones:        { type: ['string', 'null'], maxLength: 1000 },
    ingresoTotal:         { type: 'number', default: 0 },
    bloqueadaPorCarencia: { type: 'boolean', default: false },
    creadoOffline:        { type: 'boolean', default: false },
    clientTimestamp:      { type: 'string' },
    rowVersion:           { type: ['string', 'null'] },
    syncStatus:           { type: 'string', maxLength: 20, default: 'PENDIENTE', enum: ['SYNCED', 'PENDIENTE', 'CONFLICTO', 'RECHAZADO'] }
  },
  required: ['id', 'loteId', 'fechaCosecha', 'pesoTotalKg', 'calidad', 'clientTimestamp', 'syncStatus'],
  indexes: ['loteId', 'fechaCosecha', 'syncStatus']
};

// -----------------------------------------------------------------------
// Schema: Cola de operaciones pendientes de sincronizacion
// -----------------------------------------------------------------------
export interface OperacionPendienteDocType {
  operacionId: string;
  tipo: string;
  entidadId: string;
  entidadTipo: string;
  /** JSON serializado del payload especifico */
  payloadJson: string;
  clientTimestamp: string;
  rowVersionAnterior: string | null;
  estado: string;
  intentos: number;
  ultimoIntento: string | null;
  errorDetalle: string | null;
  /** JSON serializado de los datos del servidor en caso de conflicto */
  serverDataJson: string | null;
}

export const operacionPendienteSchema: RxJsonSchema<OperacionPendienteDocType> = {
  title: 'operacion_pendiente',
  version: 0,
  type: 'object',
  primaryKey: 'operacionId',
  properties: {
    operacionId:        { type: 'string', maxLength: 36 },
    tipo:               { type: 'string' },
    entidadId:          { type: 'string', maxLength: 36 },
    entidadTipo:        { type: 'string' },
    payloadJson:        { type: 'string' },
    clientTimestamp:    { type: 'string', maxLength: 30 },
    rowVersionAnterior: { type: ['string', 'null'] },
    estado:             {
      type: 'string',
      maxLength: 20,
      enum: ['PENDIENTE', 'ENVIANDO', 'APLICADA', 'DUPLICADA', 'CONFLICTO', 'RECHAZADA', 'RESUELTA', 'FALLIDA']
    },
    intentos:           { type: 'integer', default: 0, minimum: 0 },
    ultimoIntento:      { type: ['string', 'null'] },
    errorDetalle:       { type: ['string', 'null'] },
    serverDataJson:     { type: ['string', 'null'] }
  },
  required: ['operacionId', 'tipo', 'entidadId', 'entidadTipo', 'payloadJson', 'clientTimestamp', 'estado'],
  indexes: ['estado', 'clientTimestamp', 'entidadId']
};

// src/app/core/database/rxdb.service.ts
// Servicio singleton que inicializa RxDB con IndexedDB como storage.
// Expone las colecciones tipadas que usa el resto de la aplicacion.
// Se inicializa via APP_INITIALIZER en app.config.ts antes de renderizar.

import { Injectable, inject } from '@angular/core';
import {
  createRxDatabase,
  addRxPlugin,
  RxDatabase,
  RxCollection,
  RxCollectionCreator
} from 'rxdb';
import { getRxStorageDexie } from 'rxdb/plugins/storage-dexie';
import { RxDBDevModePlugin, disableWarnings } from 'rxdb/plugins/dev-mode';
import { RxDBQueryBuilderPlugin } from 'rxdb/plugins/query-builder';
import { environment } from '../../../environments/environment';
import {
  loteSchema, LoteDocType,
  insumoSchema, InsumoDocType,
  aplicacionSchema, AplicacionDocType,
  costoSchema, CostoDocType,
  cosechaSchema, CosechaDocType,
  operacionPendienteSchema, OperacionPendienteDocType
} from './rxdb-schemas';

/** Mapa tipado de todas las colecciones de la base de datos */
export interface PitaSmartCollections {
  lotes: RxCollection<LoteDocType>;
  insumos: RxCollection<InsumoDocType>;
  aplicaciones: RxCollection<AplicacionDocType>;
  costos: RxCollection<CostoDocType>;
  cosechas: RxCollection<CosechaDocType>;
  operaciones_pendientes: RxCollection<OperacionPendienteDocType>;
}

export type PitaSmartDatabase = RxDatabase<PitaSmartCollections>;

@Injectable({ providedIn: 'root' })
export class RxDBService {
  /** Instancia de la base de datos RxDB (disponible despues de initialize()) */
  private db!: PitaSmartDatabase;

  /** Indica si la base de datos ya fue inicializada */
  private initialized = false;

  /**
   * Inicializa RxDB con IndexedDB via Dexie.js como storage adapter.
   * Se llama una sola vez desde APP_INITIALIZER.
   * Los plugins se registran antes de crear la base de datos.
   */
  async initialize(): Promise<void> {
    if (this.initialized) {
      return;
    }

    // Activar plugin de desarrollo solo en entorno no productivo
    if (!environment.production) {
      addRxPlugin(RxDBDevModePlugin);
      disableWarnings();
    }

    // Plugin para query builder fluido: collection.find().where('campo').eq('valor')
    addRxPlugin(RxDBQueryBuilderPlugin);

    // Crear base de datos con Dexie.js como storage (IndexedDB nativo del navegador)
    this.db = await createRxDatabase<PitaSmartCollections>({
      name: environment.rxdbName,
      storage: getRxStorageDexie(),
      // Ignorar errores de duplicados en inicializaciones paralelas
      ignoreDuplicate: true,
      // Multi-tab: permite que multiples pestanas compartan la misma BD
      multiInstance: true
    });

    // Definicion de todas las colecciones con sus schemas tipados
    const colecciones: { [K in keyof PitaSmartCollections]: RxCollectionCreator } = {
      lotes: {
        schema: loteSchema
      },
      insumos: {
        schema: insumoSchema
      },
      aplicaciones: {
        schema: aplicacionSchema
      },
      costos: {
        schema: costoSchema
      },
      cosechas: {
        schema: cosechaSchema
      },
      operaciones_pendientes: {
        schema: operacionPendienteSchema
      }
    };

    await this.db.addCollections(colecciones);

    this.initialized = true;
  }

  /**
   * Retorna una coleccion tipada por nombre.
   * Lanza error si la BD no fue inicializada.
   */
  getCollection<K extends keyof PitaSmartCollections>(
    name: K
  ): PitaSmartCollections[K] {
    if (!this.initialized) {
      throw new Error('[RxDBService] La base de datos no fue inicializada. Verifique APP_INITIALIZER.');
    }
    return this.db[name];
  }

  /** Acceso directo a la coleccion de lotes */
  get lotes(): RxCollection<LoteDocType> {
    return this.getCollection('lotes');
  }

  /** Acceso directo a la coleccion de insumos */
  get insumos(): RxCollection<InsumoDocType> {
    return this.getCollection('insumos');
  }

  /** Acceso directo a la coleccion de aplicaciones */
  get aplicaciones(): RxCollection<AplicacionDocType> {
    return this.getCollection('aplicaciones');
  }

  /** Acceso directo a la coleccion de costos */
  get costos(): RxCollection<CostoDocType> {
    return this.getCollection('costos');
  }

  /** Acceso directo a la coleccion de cosechas */
  get cosechas(): RxCollection<CosechaDocType> {
    return this.getCollection('cosechas');
  }

  /** Acceso directo a la cola de operaciones pendientes */
  get operacionesPendientes(): RxCollection<OperacionPendienteDocType> {
    return this.getCollection('operaciones_pendientes');
  }

  /**
   * Destruye la base de datos. Util para logout completo o tests.
   * Despues de llamar esto, initialize() debe llamarse nuevamente.
   */
  async destroy(): Promise<void> {
    if (this.initialized) {
      await this.db.destroy();
      this.initialized = false;
    }
  }
}

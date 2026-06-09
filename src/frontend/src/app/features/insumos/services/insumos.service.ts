// src/app/features/insumos/services/insumos.service.ts
// Servicio del feature Insumos. Expone el catalogo de insumos agroquimicos
// registrados por Agrocalidad Ecuador. Se lee desde RxDB (offline-first).

import { Injectable, inject } from '@angular/core';
import { RxDBService } from '../../../core/database/rxdb.service';
import { InsumoDocType } from '../../../core/database/rxdb-schemas';
import { InsumoResumen } from '../../../core/models/insumo.model';
import { CarenciaPorCultivo } from '../../../core/models/insumo.model';

@Injectable({ providedIn: 'root' })
export class InsumosService {
  private readonly rxdb = inject(RxDBService);

  /**
   * Retorna el resumen de todos los insumos activos del catalogo.
   * Lee desde RxDB para disponibilidad offline garantizada.
   */
  async obtenerResumen(): Promise<InsumoResumen[]> {
    const docs = await this.rxdb.insumos
      .find({
        selector: { activo: true },
        sort: [{ nombreComercial: 'asc' }]
      })
      .exec();

    return docs.map(doc => this.docAResumen(doc.toJSON()));
  }

  /** Busca un insumo por ID en RxDB */
  async obtenerPorId(id: string): Promise<InsumoResumen | null> {
    const doc = await this.rxdb.insumos.findOne(id).exec();
    return doc ? this.docAResumen(doc.toJSON()) : null;
  }

  private docAResumen(doc: InsumoDocType): InsumoResumen {
    // El periodo de carencia se guarda como JSON serializado en RxDB.
    // Extraer los dias del primer cultivo del array, o usar 0 si esta vacio.
    let diasCarencia = 0;
    try {
      const cultivos = JSON.parse(doc.periodoCarenciaJson) as CarenciaPorCultivo[];
      diasCarencia = cultivos.length > 0 ? cultivos[0].diasEspecificos : 0;
    } catch {
      diasCarencia = 0;
    }

    return {
      id: doc.id,
      nombreComercial: doc.nombreComercial,
      ingredienteActivo: doc.ingredienteActivo,
      tipoProducto: doc.tipoProducto as InsumoResumen['tipoProducto'],
      diasCarencia,
      dosisRecomendada: {
        minima: doc.dosisMinima,
        maxima: doc.dosisMaxima,
        unidad: doc.unidadDosis as InsumoResumen['dosisRecomendada']['unidad']
      }
    };
  }
}

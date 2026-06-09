// src/app/features/costos/pages/lista-costos/lista-costos.component.ts
// Lista de costos del lote seleccionado leida desde RxDB.
// Total acumulado calculado reactivamente con computed().
// Agrupacion por categoria para mostrar desglose.

import {
  Component,
  signal,
  computed,
  inject,
  OnInit,
  ChangeDetectionStrategy
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { MatSelectModule } from '@angular/material/select';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatChipsModule } from '@angular/material/chips';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatDividerModule } from '@angular/material/divider';
import { MatExpansionModule } from '@angular/material/expansion';

import { SyncService } from '../../../../core/services/sync.service';
import { RxDBService } from '../../../../core/database/rxdb.service';
import { LotesService } from '../../../lotes/services/lotes.service';
import { SyncStatusBadgeComponent } from '../../../../shared/components/sync-status-badge/sync-status-badge.component';
import { OfflineBannerComponent } from '../../../../shared/components/offline-banner/offline-banner.component';
import { LoteResumen } from '../../../../core/models/lote.model';
import { CostoDocType } from '../../../../core/database/rxdb-schemas';
import { CategoriaCosto } from '../../../../core/models/rentabilidad.model';

/** Estado de carga de la lista */
type EstadoLista = 'cargando' | 'exito' | 'error' | 'vacio';

/** Desglose de costo por categoria para mostrar en UI */
interface ResumenCategoria {
  categoria: CategoriaCosto;
  etiqueta: string;
  icono: string;
  total: number;
  porcentaje: number;
  cantidad: number;
}

@Component({
  selector: 'app-lista-costos',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule,
    FormsModule,
    MatCardModule,
    MatTableModule,
    MatSelectModule,
    MatFormFieldModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatChipsModule,
    MatTooltipModule,
    MatDividerModule,
    MatExpansionModule,
    SyncStatusBadgeComponent,
    OfflineBannerComponent
  ],
  templateUrl: './lista-costos.component.html',
  styleUrl: './lista-costos.component.scss'
})
export class ListaCostosComponent implements OnInit {
  private readonly router = inject(Router);
  private readonly syncService = inject(SyncService);
  private readonly rxdb = inject(RxDBService);
  private readonly lotesService = inject(LotesService);

  // -------------------------------------------------------------------------
  // Signals de estado
  // -------------------------------------------------------------------------

  readonly lotes = signal<LoteResumen[]>([]);
  readonly loteIdSeleccionado = signal<string>('');
  readonly costos = signal<CostoDocType[]>([]);
  readonly estadoLista = signal<EstadoLista>('cargando');
  readonly errorMensaje = signal<string | null>(null);

  // Computed del SyncService
  readonly modoOffline = computed(() => !this.syncService.isOnline());

  // -------------------------------------------------------------------------
  // Computed reactivos (totales y desglose)
  // -------------------------------------------------------------------------

  /** Total acumulado de costos del lote (excluye eliminados) */
  readonly totalCostos = computed(() =>
    this.costos()
      .filter(c => !c.eliminado)
      .reduce((suma, c) => suma + c.monto, 0)
  );

  /** Cantidad de registros activos */
  readonly cantidadRegistros = computed(() =>
    this.costos().filter(c => !c.eliminado).length
  );

  /** Desglose por categoria, ordenado de mayor a menor */
  readonly desgloseCategoria = computed<ResumenCategoria[]>(() => {
    const costosActivos = this.costos().filter(c => !c.eliminado);
    const total = this.totalCostos();
    const mapa = new Map<CategoriaCosto, { suma: number; cantidad: number }>();

    for (const costo of costosActivos) {
      const cat = costo.categoria as CategoriaCosto;
      const existente = mapa.get(cat) ?? { suma: 0, cantidad: 0 };
      mapa.set(cat, {
        suma: existente.suma + costo.monto,
        cantidad: existente.cantidad + 1
      });
    }

    return Array.from(mapa.entries())
      .map(([categoria, datos]) => ({
        categoria,
        etiqueta: this.etiquetaCategoria(categoria),
        icono: this.iconoCategoria(categoria),
        total: datos.suma,
        porcentaje: total > 0 ? (datos.suma / total) * 100 : 0,
        cantidad: datos.cantidad
      }))
      .sort((a, b) => b.total - a.total);
  });

  /** Columnas de la tabla */
  readonly columnas = ['fecha', 'categoria', 'descripcion', 'monto', 'syncStatus'];

  ngOnInit(): void {
    void this.inicializar();
  }

  // -------------------------------------------------------------------------
  // Handlers
  // -------------------------------------------------------------------------

  async onLoteChange(loteId: string): Promise<void> {
    this.loteIdSeleccionado.set(loteId);
    await this.cargarCostos(loteId);
  }

  irANuevoCosto(): void {
    void this.router.navigate(['/costos/nuevo']);
  }

  /** Etiqueta legible para la categoria */
  etiquetaCategoria(categoria: CategoriaCosto): string {
    const mapa: Record<CategoriaCosto, string> = {
      INSUMOS_QUIMICOS: 'Insumos Quimicos',
      MANO_DE_OBRA:     'Mano de Obra',
      TRANSPORTE:       'Transporte',
      RIEGO:            'Riego',
      MAQUINARIA:       'Maquinaria',
      OTROS:            'Otros'
    };
    return mapa[categoria] ?? categoria;
  }

  /** Icono Material para la categoria */
  iconoCategoria(categoria: CategoriaCosto): string {
    const mapa: Record<CategoriaCosto, string> = {
      INSUMOS_QUIMICOS: 'science',
      MANO_DE_OBRA:     'people',
      TRANSPORTE:       'local_shipping',
      RIEGO:            'water_drop',
      MAQUINARIA:       'agriculture',
      OTROS:            'more_horiz'
    };
    return mapa[categoria] ?? 'attach_money';
  }

  /** Etiqueta sync status */
  etiquetaSyncStatus(status: string): string {
    const mapa: Record<string, string> = {
      SYNCED:    'Sincronizado',
      PENDIENTE: 'Pendiente',
      CONFLICTO: 'Conflicto',
      RECHAZADO: 'Rechazado'
    };
    return mapa[status] ?? status;
  }

  /** CSS class para chip de sync */
  claseSyncStatus(status: string): string {
    const mapa: Record<string, string> = {
      SYNCED:    'chip-synced',
      PENDIENTE: 'chip-pendiente',
      CONFLICTO: 'chip-conflicto',
      RECHAZADO: 'chip-rechazado'
    };
    return mapa[status] ?? '';
  }

  // -------------------------------------------------------------------------
  // Metodos privados
  // -------------------------------------------------------------------------

  private async inicializar(): Promise<void> {
    try {
      const lotes = await this.lotesService.obtenerResumen();
      this.lotes.set(lotes);

      if (lotes.length > 0) {
        this.loteIdSeleccionado.set(lotes[0].id);
        await this.cargarCostos(lotes[0].id);
      } else {
        this.estadoLista.set('vacio');
      }
    } catch {
      this.estadoLista.set('error');
      this.errorMensaje.set('No se pudieron cargar los lotes.');
    }
  }

  /**
   * Carga los costos activos del lote desde RxDB.
   * Excluye costos eliminados (soft delete) y rechazados por el servidor.
   * Ordena por fecha descendente.
   */
  private async cargarCostos(loteId: string): Promise<void> {
    this.estadoLista.set('cargando');
    this.errorMensaje.set(null);

    try {
      const docs = await this.rxdb.costos
        .find({
          selector: {
            loteId,
            eliminado: false,
            syncStatus: { $nin: ['RECHAZADO'] }
          },
          sort: [{ fecha: 'desc' }]
        })
        .exec();

      this.costos.set(docs.map(d => d.toJSON() as CostoDocType));
      this.estadoLista.set(docs.length > 0 ? 'exito' : 'vacio');
    } catch {
      this.costos.set([]);
      this.estadoLista.set('error');
      this.errorMensaje.set('Error al cargar los costos.');
    }
  }
}

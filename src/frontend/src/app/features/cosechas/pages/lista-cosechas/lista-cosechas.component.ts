// src/app/features/cosechas/pages/lista-cosechas/lista-cosechas.component.ts
// Lista de cosechas del lote seleccionado, leida desde RxDB.
// Columnas: Fecha, Peso, Calidad, Precio/kg, Ingreso Total, Estado sync.
// OnPush + Signals: sin suscripciones manuales en la clase.

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

import { SyncService } from '../../../../core/services/sync.service';
import { RxDBService } from '../../../../core/database/rxdb.service';
import { LotesService } from '../../../lotes/services/lotes.service';
import { SyncStatusBadgeComponent } from '../../../../shared/components/sync-status-badge/sync-status-badge.component';
import { OfflineBannerComponent } from '../../../../shared/components/offline-banner/offline-banner.component';
import { LoteResumen } from '../../../../core/models/lote.model';
import { CosechaDocType } from '../../../../core/database/rxdb-schemas';

/** Estado de carga de la lista */
type EstadoLista = 'cargando' | 'exito' | 'error' | 'vacio';

@Component({
  selector: 'app-lista-cosechas',
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
    SyncStatusBadgeComponent,
    OfflineBannerComponent
  ],
  templateUrl: './lista-cosechas.component.html',
  styleUrl: './lista-cosechas.component.scss'
})
export class ListaCosechasComponent implements OnInit {
  private readonly router = inject(Router);
  private readonly syncService = inject(SyncService);
  private readonly rxdb = inject(RxDBService);
  private readonly lotesService = inject(LotesService);

  // -------------------------------------------------------------------------
  // Signals de estado
  // -------------------------------------------------------------------------

  readonly lotes = signal<LoteResumen[]>([]);
  readonly loteIdSeleccionado = signal<string>('');
  readonly cosechas = signal<CosechaDocType[]>([]);
  readonly estadoLista = signal<EstadoLista>('cargando');
  readonly errorMensaje = signal<string | null>(null);

  // Computed del SyncService
  readonly modoOffline = computed(() => !this.syncService.isOnline());

  /** Total de ingresos de las cosechas cargadas (computed reactivo) */
  readonly totalIngresos = computed(() =>
    this.cosechas().reduce((suma, c) => suma + (c.ingresoTotal ?? 0), 0)
  );

  /** Total de kg cosechados */
  readonly totalKg = computed(() =>
    this.cosechas().reduce((suma, c) => suma + c.pesoTotalKg, 0)
  );

  /** Precio promedio por kg */
  readonly precioPromedio = computed(() => {
    const total = this.totalKg();
    const ingresos = this.totalIngresos();
    return total > 0 ? ingresos / total : null;
  });

  /** Columnas de la tabla Material */
  readonly columnas = ['fechaCosecha', 'pesoTotalKg', 'calidad', 'precioVentaKg', 'ingresoTotal', 'syncStatus'];

  ngOnInit(): void {
    void this.inicializar();
  }

  // -------------------------------------------------------------------------
  // Handlers
  // -------------------------------------------------------------------------

  async onLoteChange(loteId: string): Promise<void> {
    this.loteIdSeleccionado.set(loteId);
    await this.cargarCosechas(loteId);
  }

  irANuevaCosecha(): void {
    void this.router.navigate(['/cosechas/nueva']);
  }

  /** Etiqueta legible para el estado de sync */
  etiquetaSyncStatus(status: string): string {
    const mapa: Record<string, string> = {
      SYNCED:    'Sincronizado',
      PENDIENTE: 'Pendiente',
      CONFLICTO: 'Conflicto',
      RECHAZADO: 'Rechazado'
    };
    return mapa[status] ?? status;
  }

  /** CSS class para el chip de sync */
  claseSyncStatus(status: string): string {
    const mapa: Record<string, string> = {
      SYNCED:    'chip-synced',
      PENDIENTE: 'chip-pendiente',
      CONFLICTO: 'chip-conflicto',
      RECHAZADO: 'chip-rechazado'
    };
    return mapa[status] ?? '';
  }

  /** Etiqueta legible para la calidad */
  etiquetaCalidad(calidad: string): string {
    const mapa: Record<string, string> = {
      PRIMERA:  'Primera',
      SEGUNDA:  'Segunda',
      TERCERA:  'Tercera',
      DESCARTE: 'Descarte'
    };
    return mapa[calidad] ?? calidad;
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
        await this.cargarCosechas(lotes[0].id);
      } else {
        this.estadoLista.set('vacio');
      }
    } catch {
      this.estadoLista.set('error');
      this.errorMensaje.set('No se pudieron cargar los lotes.');
    }
  }

  /**
   * Carga las cosechas del lote desde RxDB, ordenadas por fecha descendente.
   * Excluye cosechas rechazadas del servidor.
   */
  private async cargarCosechas(loteId: string): Promise<void> {
    this.estadoLista.set('cargando');
    this.errorMensaje.set(null);

    try {
      const docs = await this.rxdb.cosechas
        .find({
          selector: {
            loteId,
            syncStatus: { $nin: ['RECHAZADO'] }
          },
          sort: [{ fechaCosecha: 'desc' }]
        })
        .exec();

      this.cosechas.set(docs.map(d => d.toJSON() as CosechaDocType));
      this.estadoLista.set(docs.length > 0 ? 'exito' : 'vacio');
    } catch {
      this.cosechas.set([]);
      this.estadoLista.set('error');
      this.errorMensaje.set('Error al cargar las cosechas.');
    }
  }
}

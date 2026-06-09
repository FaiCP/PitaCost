// src/app/features/dashboard/dashboard.component.ts
// Dashboard de Rentabilidad.
// Toda la reactividad via Signals + computed(). Sin RxJS Subjects para estado.
// Calculo local de KPIs desde RxDB cuando offline; datos del servidor cuando online.

import {
  Component,
  signal,
  computed,
  inject,
  effect,
  OnInit,
  ChangeDetectionStrategy
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { firstValueFrom } from 'rxjs';
import { MatCardModule } from '@angular/material/card';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatChipsModule } from '@angular/material/chips';
import { MatDividerModule } from '@angular/material/divider';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatBadgeModule } from '@angular/material/badge';

import { SyncService } from '../../core/services/sync.service';
import { ApiService } from '../../core/services/api.service';
import { RxDBService } from '../../core/database/rxdb.service';
import { LotesService } from '../lotes/services/lotes.service';
import { SyncStatusBadgeComponent } from '../../shared/components/sync-status-badge/sync-status-badge.component';
import { OfflineBannerComponent } from '../../shared/components/offline-banner/offline-banner.component';

import { Lote, LoteResumen, AlertaLote } from '../../core/models/lote.model';
import {
  DashboardRentabilidad,
  KpisLocales,
  CategoriaCosto
} from '../../core/models/rentabilidad.model';
import { AplicacionQuimico } from '../../core/models/aplicacion.model';

/** Estado de carga de datos del dashboard */
type EstadoCarga = 'inicial' | 'cargando' | 'exito' | 'error' | 'vacio';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule,
    FormsModule,
    MatCardModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatChipsModule,
    MatDividerModule,
    MatTableModule,
    MatTooltipModule,
    MatBadgeModule,
    SyncStatusBadgeComponent,
    OfflineBannerComponent
  ],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss'
})
export class DashboardComponent implements OnInit {
  private readonly syncService = inject(SyncService);
  private readonly api = inject(ApiService);
  private readonly rxdb = inject(RxDBService);
  private readonly lotesService = inject(LotesService);

  // -------------------------------------------------------------------------
  // Signals de estado local
  // -------------------------------------------------------------------------

  /** Lista de lotes disponibles del usuario */
  readonly lotes = signal<LoteResumen[]>([]);

  /** Lote actualmente seleccionado para el dashboard */
  readonly loteSeleccionado = signal<LoteResumen | null>(null);

  /** Periodo de consulta */
  readonly periodo = signal<{ desde: Date; hasta: Date }>({
    desde: new Date(new Date().getFullYear(), 0, 1), // Inicio del ano actual
    hasta: new Date()
  });

  /** Datos de rentabilidad cargados del servidor */
  readonly datosRentabilidad = signal<DashboardRentabilidad | null>(null);

  /** KPIs calculados localmente cuando offline */
  readonly kpisLocales = signal<KpisLocales | null>(null);

  /** Ultimas aplicaciones del lote seleccionado */
  readonly ultimasAplicaciones = signal<AplicacionQuimico[]>([]);

  /** Estado de la carga del dashboard */
  readonly estadoCarga = signal<EstadoCarga>('inicial');

  /** Mensaje de error si la carga falla */
  readonly errorMensaje = signal<string | null>(null);

  // -------------------------------------------------------------------------
  // Signals del SyncService (estado de conectividad y sync)
  // -------------------------------------------------------------------------

  /** true cuando el dispositivo esta sin conexion */
  readonly modoOffline = computed(() => !this.syncService.isOnline());

  /** Cantidad de operaciones pendientes de sincronizar */
  readonly pendientesSyncCount = computed(() => this.syncService.pendingCount());

  /** true si hay cambios sin sincronizar */
  readonly hayPendientes = computed(() => this.syncService.hasPendingChanges());

  /** Estado del motor de sync */
  readonly estadoSync = computed(() => this.syncService.syncStatus());

  // -------------------------------------------------------------------------
  // Computed: rentabilidad (fuente de verdad reactiva)
  // -------------------------------------------------------------------------

  /**
   * Rentabilidad calculada reactivamente.
   * Si hay datos del servidor: usa datosRentabilidad().
   * Si offline: usa kpisLocales() calculados desde RxDB.
   */
  readonly rentabilidad = computed<KpisLocales | null>(() => {
    const datos = this.datosRentabilidad();
    if (datos) {
      return {
        totalIngresos: datos.ingresos.totalVentas,
        totalCostos: datos.costos.totalCostos,
        utilidadBruta: datos.rentabilidad.utilidadBruta,
        margenBruto: datos.rentabilidad.margenBruto,
        roi: datos.rentabilidad.roi,
        utilidadPorHa: datos.rentabilidad.utilidadPorHa,
        esCalculoOffline: false
      };
    }
    return this.kpisLocales();
  });

  /** Alertas del lote seleccionado */
  readonly alertas = computed<AlertaLote[]>(() =>
    this.datosRentabilidad()?.alertas ?? []
  );

  /** Alertas criticas (periodo de carencia) */
  readonly alertasCriticas = computed(() =>
    this.alertas().filter(a => a.severidad === 'CRITICA')
  );

  /** Columnas de la tabla de ultimas aplicaciones */
  readonly columnasTabla = ['insumoNombre', 'fechaAplicacion', 'areaHa', 'costoTotal', 'carencia', 'sync'];

  constructor() {
    // Efecto: cuando cambia el lote o el periodo, recargar datos
    effect(() => {
      const lote = this.loteSeleccionado();
      const periodo = this.periodo();
      if (lote) {
        void this.cargarDashboard(lote.id, periodo);
      }
    });
  }

  ngOnInit(): void {
    void this.cargarLotes();
  }

  // -------------------------------------------------------------------------
  // Handlers de eventos del template
  // -------------------------------------------------------------------------

  /** El usuario selecciona un lote diferente */
  onLoteChange(loteId: string): void {
    const lote = this.lotes().find(l => l.id === loteId);
    this.loteSeleccionado.set(lote ?? null);
  }

  /** Forzar sincronizacion manual */
  async sincronizarAhora(): Promise<void> {
    await this.syncService.sincronizar();
    // Recargar datos despues de sync exitoso
    const lote = this.loteSeleccionado();
    if (lote) {
      await this.cargarDashboard(lote.id, this.periodo());
    }
  }

  /** Calcular estado de carencia de una aplicacion para la tabla */
  estadoCarenciaAplicacion(aplicacion: AplicacionQuimico): string {
    if (!aplicacion.periodoCarencia?.fechaFinCarencia) return 'Sin carencia';

    const finCarencia = new Date(aplicacion.periodoCarencia.fechaFinCarencia);
    const hoy = new Date();

    if (finCarencia > hoy) {
      const diasRestantes = Math.ceil(
        (finCarencia.getTime() - hoy.getTime()) / (1_000 * 60 * 60 * 24)
      );
      return `En carencia (${diasRestantes}d restantes)`;
    }

    return 'Carencia finalizada';
  }

  /** Retorna CSS class para el estado de carencia */
  claseCarencia(aplicacion: AplicacionQuimico): string {
    if (!aplicacion.periodoCarencia?.fechaFinCarencia) return '';
    const finCarencia = new Date(aplicacion.periodoCarencia.fechaFinCarencia);
    return finCarencia > new Date() ? 'carencia--activa' : 'carencia--finalizada';
  }

  // -------------------------------------------------------------------------
  // Metodos privados
  // -------------------------------------------------------------------------

  private async cargarLotes(): Promise<void> {
    try {
      const lotes = await this.lotesService.obtenerResumen();
      this.lotes.set(lotes);

      // Seleccionar el primer lote automaticamente
      if (lotes.length > 0) {
        this.loteSeleccionado.set(lotes[0]);
      } else {
        this.estadoCarga.set('vacio');
      }
    } catch {
      this.lotes.set([]);
      this.estadoCarga.set('error');
      this.errorMensaje.set('No se pudieron cargar los lotes.');
    }
  }

  private async cargarDashboard(
    loteId: string,
    periodo: { desde: Date; hasta: Date }
  ): Promise<void> {
    this.estadoCarga.set('cargando');
    this.errorMensaje.set(null);

    // Siempre cargar aplicaciones locales (disponibles offline)
    await this.cargarAplicacionesLocales(loteId);

    if (this.syncService.isOnline()) {
      // Online: cargar rentabilidad completa del servidor
      await this.cargarRentabilidadServidor(loteId, periodo);
    } else {
      // Offline: calcular KPIs desde RxDB local
      await this.calcularKpisLocales(loteId, periodo);
    }
  }

  /** Carga las ultimas 10 aplicaciones del lote desde RxDB */
  private async cargarAplicacionesLocales(loteId: string): Promise<void> {
    try {
      const docs = await this.rxdb.aplicaciones
        .find({
          selector: { loteId, syncStatus: { $nin: ['RECHAZADO'] } },
          sort: [{ fechaAplicacion: 'desc' }],
          limit: 10
        })
        .exec();

      const aplicaciones: AplicacionQuimico[] = docs.map(doc => ({
        id: doc.id,
        loteId: doc.loteId,
        insumoId: doc.insumoId,
        fechaAplicacion: doc.fechaAplicacion,
        dosis: { cantidad: doc.dosisCantidad, unidad: doc.dosisUnidad as AplicacionQuimico['dosis']['unidad'] },
        areaAplicadaHa: doc.areaAplicadaHa,
        metodoAplicacion: doc.metodoAplicacion as AplicacionQuimico['metodoAplicacion'],
        operadorNombre: doc.operadorNombre,
        costoTotal: doc.costoTotal,
        creadoOffline: doc.creadoOffline,
        clientTimestamp: doc.clientTimestamp,
        periodoCarencia: doc.fechaFinCarencia ? {
          diasCarencia: doc.diasCarencia,
          fechaFinCarencia: doc.fechaFinCarencia,
          cosechaBloqueada: new Date(doc.fechaFinCarencia) > new Date()
        } : undefined,
        loteNombre: doc.loteNombre ?? undefined,
        insumoNombre: doc.insumoNombre ?? undefined,
        syncStatus: doc.syncStatus as AplicacionQuimico['syncStatus']
      }));

      this.ultimasAplicaciones.set(aplicaciones);
    } catch {
      this.ultimasAplicaciones.set([]);
    }
  }

  /** Solicita rentabilidad completa al servidor */
  private async cargarRentabilidadServidor(
    loteId: string,
    periodo: { desde: Date; hasta: Date }
  ): Promise<void> {
    try {
      const desde = periodo.desde.toISOString().split('T')[0];
      const hasta = periodo.hasta.toISOString().split('T')[0];

      const resp = await firstValueFrom(
        this.api.get<DashboardRentabilidad>(
          `/api/lotes/${loteId}/rentabilidad`,
          { desde, hasta }
        )
      );

      this.datosRentabilidad.set(resp.data ?? null);
      this.kpisLocales.set(null);
      this.estadoCarga.set(resp.data ? 'exito' : 'vacio');
    } catch (error) {
      const mensaje = error instanceof Error ? error.message : 'Error al cargar rentabilidad';
      this.errorMensaje.set(mensaje);
      this.estadoCarga.set('error');
      // Fallback a calculo local cuando el servidor falla
      await this.calcularKpisLocales(loteId, periodo);
    }
  }

  /**
   * Calcula KPIs localmente desde RxDB para uso offline.
   * Formula de bounded-contexts.md seccion 4 "Calculo de Rentabilidad".
   */
  private async calcularKpisLocales(
    loteId: string,
    periodo: { desde: Date; hasta: Date }
  ): Promise<void> {
    try {
      const desdeStr = periodo.desde.toISOString().split('T')[0];
      const hastaStr = periodo.hasta.toISOString().split('T')[0];

      const costos = await this.rxdb.costos
        .find({
          selector: {
            loteId,
            fecha: { $gte: desdeStr, $lte: hastaStr },
            eliminado: false
          }
        })
        .exec();

      const totalCostos = costos.reduce((sum, c) => sum + c.monto, 0);

      // TODO: cuando el modelo de IngresoLote este en RxDB, sumar aqui
      const totalIngresos = 0;

      const utilidadBruta = totalIngresos - totalCostos;
      const margenBruto = totalIngresos > 0
        ? (utilidadBruta / totalIngresos) * 100
        : null;
      const roi = totalCostos > 0
        ? (utilidadBruta / totalCostos) * 100
        : null;

      const loteDoc = await this.rxdb.lotes.findOne(loteId).exec();
      const areaHa = loteDoc?.areaHa ?? 0;
      const utilidadPorHa = areaHa > 0 ? utilidadBruta / areaHa : null;

      this.kpisLocales.set({
        totalIngresos,
        totalCostos,
        utilidadBruta,
        margenBruto,
        roi,
        utilidadPorHa,
        esCalculoOffline: true
      });

      this.estadoCarga.set('exito');
    } catch {
      this.kpisLocales.set(null);
      this.estadoCarga.set('error');
    }
  }
}

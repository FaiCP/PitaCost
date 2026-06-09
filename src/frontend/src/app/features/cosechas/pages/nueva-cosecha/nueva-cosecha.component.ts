// src/app/features/cosechas/pages/nueva-cosecha/nueva-cosecha.component.ts
// Registro de cosechas con validacion de periodo de carencia offline.
// Regla critica: consultar aplicaciones con carencia activa en RxDB antes
// de permitir el registro. La alerta es advertencia (no bloqueo total en UI).
// Si online: POST /api/cosechas. Si offline: encolarOperacion.

import {
  Component,
  signal,
  computed,
  inject,
  OnInit,
  ChangeDetectionStrategy
} from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  ReactiveFormsModule,
  FormBuilder,
  Validators,
  FormGroup
} from '@angular/forms';
import { Router } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { v4 as uuidv4 } from 'uuid';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule } from '@angular/material/core';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatCardModule } from '@angular/material/card';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatBadgeModule } from '@angular/material/badge';
import { MatDividerModule } from '@angular/material/divider';
import { MatChipsModule } from '@angular/material/chips';

import { SyncService } from '../../../../core/services/sync.service';
import { ApiService } from '../../../../core/services/api.service';
import { RxDBService } from '../../../../core/database/rxdb.service';
import { LotesService } from '../../../lotes/services/lotes.service';
import { SyncStatusBadgeComponent } from '../../../../shared/components/sync-status-badge/sync-status-badge.component';
import { OfflineBannerComponent } from '../../../../shared/components/offline-banner/offline-banner.component';
import { LoteResumen } from '../../../../core/models/lote.model';
import { CosechaDocType } from '../../../../core/database/rxdb-schemas';
import { firstValueFrom } from 'rxjs';

/** Calidad de cosecha segun estandares de exportacion */
export type CalidadCosecha = 'PRIMERA' | 'SEGUNDA' | 'TERCERA' | 'DESCARTE';

/** Datos de la aplicacion que genera la alerta de carencia */
export interface AplicacionCarencia {
  insumoNombre: string;
  fechaAplicacion: string;
  fechaFinCarencia: string;
  diasRestantes: number;
}

/** Estado del proceso de guardado */
interface EstadoGuardado {
  guardando: boolean;
  error: string | null;
  exito: boolean;
}

/** Payload que va al API o a la cola de sync */
interface CosechaPayload {
  id: string;
  loteId: string;
  fechaCosecha: string;
  pesoTotalKg: number;
  calidad: CalidadCosecha;
  precioVentaKg: number | null;
  comprador: string | null;
  observaciones: string | null;
}

@Component({
  selector: 'app-nueva-cosecha',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
    MatDatepickerModule,
    MatNativeDateModule,
    MatSnackBarModule,
    MatCardModule,
    MatProgressSpinnerModule,
    MatBadgeModule,
    MatDividerModule,
    MatChipsModule,
    SyncStatusBadgeComponent,
    OfflineBannerComponent
  ],
  templateUrl: './nueva-cosecha.component.html',
  styleUrl: './nueva-cosecha.component.scss'
})
export class NuevaCosechaComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);
  private readonly snackBar = inject(MatSnackBar);
  private readonly syncService = inject(SyncService);
  private readonly api = inject(ApiService);
  private readonly rxdb = inject(RxDBService);
  private readonly lotesService = inject(LotesService);

  // -------------------------------------------------------------------------
  // Signals de estado
  // -------------------------------------------------------------------------

  readonly lotes = signal<LoteResumen[]>([]);
  readonly cargandoDatos = signal(true);
  readonly estadoGuardado = signal<EstadoGuardado>({
    guardando: false,
    error: null,
    exito: false
  });

  /**
   * Alerta de carencia activa en el lote en la fecha de cosecha seleccionada.
   * Null = sin carencia activa. Advertencia, no bloqueo total.
   */
  readonly alertaCarencia = signal<AplicacionCarencia | null>(null);

  // Computed del SyncService para la UI
  readonly modoOffline = computed(() => !this.syncService.isOnline());
  readonly pendientesSync = computed(() => this.syncService.pendingCount());

  // -------------------------------------------------------------------------
  // Formulario reactivo
  // -------------------------------------------------------------------------

  readonly form: FormGroup = this.fb.group({
    loteId:        ['', [Validators.required]],
    fechaCosecha:  [new Date(), [Validators.required]],
    pesoTotalKg:   [null, [Validators.required, Validators.min(0.001)]],
    calidad:       ['PRIMERA', [Validators.required]],
    precioVentaKg: [null, [Validators.min(0)]],
    comprador:     ['', [Validators.maxLength(200)]],
    observaciones: ['', [Validators.maxLength(1000)]]
  });

  /** Opciones de calidad de cosecha */
  readonly opcionesCalidad: Array<{ valor: CalidadCosecha; etiqueta: string }> = [
    { valor: 'PRIMERA',  etiqueta: 'Primera calidad' },
    { valor: 'SEGUNDA',  etiqueta: 'Segunda calidad' },
    { valor: 'TERCERA',  etiqueta: 'Tercera calidad' },
    { valor: 'DESCARTE', etiqueta: 'Descarte' }
  ];

  /** Ingreso estimado calculado reactivamente */
  readonly ingresoEstimado = computed(() => {
    const peso   = this.form.get('pesoTotalKg')?.value as number | null;
    const precio = this.form.get('precioVentaKg')?.value as number | null;
    if (peso && precio && peso > 0 && precio > 0) {
      return peso * precio;
    }
    return null;
  });

  /** Fecha maxima de cosecha (hoy) */
  readonly fechaMaxima = new Date();

  ngOnInit(): void {
    this.cargarLotes();

    // Verificar carencia cuando cambia el lote o la fecha de cosecha
    this.form.get('loteId')?.valueChanges
      .pipe(takeUntilDestroyed())
      .subscribe(() => void this.verificarCarenciaActiva());

    this.form.get('fechaCosecha')?.valueChanges
      .pipe(takeUntilDestroyed())
      .subscribe(() => void this.verificarCarenciaActiva());
  }

  // -------------------------------------------------------------------------
  // Handlers de eventos
  // -------------------------------------------------------------------------

  async guardar(): Promise<void> {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.estadoGuardado.set({ guardando: true, error: null, exito: false });

    const values = this.form.value as {
      loteId: string;
      fechaCosecha: Date;
      pesoTotalKg: number;
      calidad: CalidadCosecha;
      precioVentaKg: number | null;
      comprador: string | null;
      observaciones: string | null;
    };

    const cosechaId = uuidv4();
    const fechaStr = values.fechaCosecha.toISOString().split('T')[0];
    const ingresoTotal = (values.pesoTotalKg ?? 0) * (values.precioVentaKg ?? 0);

    const payload: CosechaPayload = {
      id: cosechaId,
      loteId: values.loteId,
      fechaCosecha: fechaStr,
      pesoTotalKg: values.pesoTotalKg,
      calidad: values.calidad,
      precioVentaKg: values.precioVentaKg ?? null,
      comprador: values.comprador || null,
      observaciones: values.observaciones || null
    };

    try {
      if (this.syncService.isOnline()) {
        // Online: POST directo al API
        await firstValueFrom(
          this.api.post<CosechaPayload, unknown>('/api/cosechas', payload)
        );
      } else {
        // Offline: encolar para sync posterior
        await this.syncService.encolarOperacion(
          'CREAR_COSECHA',
          cosechaId,
          'Cosecha',
          payload as unknown as Record<string, unknown>
        );
      }

      // Persistir en RxDB local (fuente de verdad del dispositivo)
      const doc: CosechaDocType = {
        id: cosechaId,
        loteId: values.loteId,
        fechaCosecha: fechaStr,
        pesoTotalKg: values.pesoTotalKg,
        calidad: values.calidad,
        precioVentaKg: values.precioVentaKg ?? null,
        comprador: values.comprador || null,
        observaciones: values.observaciones || null,
        ingresoTotal,
        bloqueadaPorCarencia: !!this.alertaCarencia(),
        creadoOffline: !this.syncService.isOnline(),
        clientTimestamp: new Date().toISOString(),
        rowVersion: null,
        syncStatus: this.syncService.isOnline() ? 'SYNCED' : 'PENDIENTE'
      };

      await this.rxdb.cosechas.insert(doc);

      this.estadoGuardado.set({ guardando: false, error: null, exito: true });

      const mensaje = this.modoOffline()
        ? 'Cosecha guardada. Se sincronizara cuando haya conexion.'
        : 'Cosecha registrada exitosamente.';

      this.snackBar.open(mensaje, 'Aceptar', {
        duration: 4_000,
        panelClass: this.modoOffline() ? ['snack-offline'] : ['snack-exito']
      });

      await this.router.navigate(['/cosechas']);
    } catch (error) {
      const mensaje = error instanceof Error ? error.message : 'Error al guardar la cosecha';
      this.estadoGuardado.set({ guardando: false, error: mensaje, exito: false });
    }
  }

  cancelar(): void {
    void this.router.navigate(['/cosechas']);
  }

  // -------------------------------------------------------------------------
  // Metodos privados
  // -------------------------------------------------------------------------

  private async cargarLotes(): Promise<void> {
    this.cargandoDatos.set(true);
    try {
      const lotes = await this.lotesService.obtenerResumen();
      this.lotes.set(lotes);
    } catch {
      this.lotes.set([]);
    } finally {
      this.cargandoDatos.set(false);
    }
  }

  /**
   * Verifica en RxDB si existe alguna aplicacion con carencia activa
   * en el lote y fecha de cosecha seleccionados.
   * Implementa la regla critica de Agrocalidad (lado cliente).
   */
  private async verificarCarenciaActiva(): Promise<void> {
    const loteId = this.form.get('loteId')?.value as string | null;
    const fechaCosecha = this.form.get('fechaCosecha')?.value as Date | null;

    if (!loteId || !fechaCosecha) {
      this.alertaCarencia.set(null);
      return;
    }

    const fechaStr = fechaCosecha instanceof Date
      ? fechaCosecha.toISOString().split('T')[0]
      : fechaCosecha;

    try {
      // Buscar aplicaciones del lote cuya fecha de fin de carencia sea posterior
      // a la fecha de cosecha seleccionada (carencia AUN no vencida)
      const aplicaciones = await this.rxdb.aplicaciones
        .find({
          selector: {
            loteId,
            fechaFinCarencia: { $gt: fechaStr }
          },
          sort: [{ fechaFinCarencia: 'desc' }],
          limit: 1
        })
        .exec();

      if (aplicaciones.length === 0) {
        this.alertaCarencia.set(null);
        return;
      }

      const app = aplicaciones[0];
      const finCarencia = new Date(app.fechaFinCarencia!);
      const fechaCosechaDate = new Date(fechaStr);
      const diasRestantes = Math.ceil(
        (finCarencia.getTime() - fechaCosechaDate.getTime()) / (1_000 * 60 * 60 * 24)
      );

      this.alertaCarencia.set({
        insumoNombre: app.insumoNombre ?? app.insumoId,
        fechaAplicacion: app.fechaAplicacion,
        fechaFinCarencia: app.fechaFinCarencia!,
        diasRestantes
      });
    } catch {
      // Si falla la consulta RxDB, no bloquear al usuario
      this.alertaCarencia.set(null);
    }
  }
}

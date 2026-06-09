// src/app/features/aplicaciones/pages/nueva-aplicacion/nueva-aplicacion.component.ts
// Componente de registro de aplicaciones de quimicos.
// Implementa validacion de periodo de carencia en cliente (offline) y en servidor.
// OnPush para rendimiento optimo en dispositivos moviles de campo.

import {
  Component,
  signal,
  computed,
  inject,
  OnInit,
  ChangeDetectionStrategy
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators, FormGroup, AbstractControl } from '@angular/forms';
import { Router } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
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
import { MatChipsModule } from '@angular/material/chips';

import { SyncService } from '../../../../core/services/sync.service';
import { AplicacionesService } from '../../services/aplicaciones.service';
import { LotesService } from '../../../lotes/services/lotes.service';
import { InsumosService } from '../../../insumos/services/insumos.service';
import { MetodoAplicacion, AplicacionQuimico } from '../../../../core/models/aplicacion.model';
import { LoteResumen } from '../../../../core/models/lote.model';
import { InsumoResumen } from '../../../../core/models/insumo.model';
import { SyncStatusBadgeComponent } from '../../../../shared/components/sync-status-badge/sync-status-badge.component';
import { OfflineBannerComponent } from '../../../../shared/components/offline-banner/offline-banner.component';

/** Estado del proceso de guardado */
interface EstadoGuardado {
  guardando: boolean;
  error: string | null;
  exito: boolean;
}

/** Alerta de periodo de carencia calculada localmente */
interface AlertaCarencia {
  activa: boolean;
  mensaje: string;
  fechaFin?: string;
  diasRestantes?: number;
}

@Component({
  selector: 'app-nueva-aplicacion',
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
    MatChipsModule,
    SyncStatusBadgeComponent,
    OfflineBannerComponent
  ],
  templateUrl: './nueva-aplicacion.component.html',
  styleUrl: './nueva-aplicacion.component.scss'
})
export class NuevaAplicacionComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);
  private readonly snackBar = inject(MatSnackBar);
  private readonly aplicacionesService = inject(AplicacionesService);
  private readonly lotesService = inject(LotesService);
  private readonly insumosService = inject(InsumosService);

  // Signals publicos del SyncService para la UI
  readonly syncService = inject(SyncService);
  readonly modoOffline = computed(() => !this.syncService.isOnline());
  readonly pendientesSync = computed(() => this.syncService.pendingCount());

  // -------------------------------------------------------------------------
  // Signals de estado del componente
  // -------------------------------------------------------------------------

  readonly lotes = signal<LoteResumen[]>([]);
  readonly insumos = signal<InsumoResumen[]>([]);
  readonly cargandoDatos = signal(true);
  readonly estadoGuardado = signal<EstadoGuardado>({
    guardando: false,
    error: null,
    exito: false
  });

  /** Alerta de carencia calculada cuando cambia lote o insumo */
  readonly alertaCarencia = signal<AlertaCarencia>({ activa: false, mensaje: '' });

  // -------------------------------------------------------------------------
  // Formulario reactivo
  // -------------------------------------------------------------------------

  readonly form: FormGroup = this.fb.group({
    loteId:            ['', [Validators.required]],
    insumoId:          ['', [Validators.required]],
    fechaAplicacion:   [new Date(), [Validators.required]],
    dosisCantidad:     [null, [Validators.required, Validators.min(0.001)]],
    dosisUnidad:       ['L_HA', [Validators.required]],
    areaAplicadaHa:    [null, [Validators.required, Validators.min(0.001)]],
    metodoAplicacion:  ['FUMIGACION', [Validators.required]],
    operadorNombre:    ['', [Validators.required, Validators.minLength(2), Validators.maxLength(200)]],
    costoTotal:        [0, [Validators.min(0)]],
    observaciones:     ['', [Validators.maxLength(1000)]]
  });

  /** Opciones de metodo de aplicacion */
  readonly metodosAplicacion: Array<{ valor: MetodoAplicacion; etiqueta: string }> = [
    { valor: 'FUMIGACION', etiqueta: 'Fumigacion' },
    { valor: 'DRENCH', etiqueta: 'Drench' },
    { valor: 'INYECCION', etiqueta: 'Inyeccion' },
    { valor: 'GRANULAR', etiqueta: 'Granular' },
    { valor: 'OTRO', etiqueta: 'Otro' }
  ];

  /** Opciones de unidad de dosis */
  readonly unidadesDosis = [
    { valor: 'L_HA', etiqueta: 'L/Ha' },
    { valor: 'KG_HA', etiqueta: 'Kg/Ha' },
    { valor: 'ML_HA', etiqueta: 'mL/Ha' },
    { valor: 'G_HA', etiqueta: 'g/Ha' },
    { valor: 'CC_HA', etiqueta: 'cc/Ha' }
  ];

  /** Fecha maxima permitida para aplicacion (hoy + 1 hora de tolerancia GPS) */
  readonly fechaMaxima = new Date(Date.now() + 3_600_000);

  /** Fecha minima (30 dias atras segun validacion del API) */
  readonly fechaMinima = new Date(Date.now() - 30 * 24 * 3_600_000);

  ngOnInit(): void {
    this.cargarDatosMaestros();

    // Validar periodo de carencia cuando cambia el lote o la fecha
    this.form.get('loteId')?.valueChanges
      .pipe(takeUntilDestroyed())
      .subscribe(() => void this.evaluarPeriodoCarencia());

    this.form.get('fechaAplicacion')?.valueChanges
      .pipe(takeUntilDestroyed())
      .subscribe(() => void this.evaluarPeriodoCarencia());
  }

  // -------------------------------------------------------------------------
  // Handlers de eventos del template
  // -------------------------------------------------------------------------

  /** Maneja el submit del formulario */
  async guardar(): Promise<void> {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.estadoGuardado.set({ guardando: true, error: null, exito: false });

    try {
      const values = this.form.value as {
        loteId: string;
        insumoId: string;
        fechaAplicacion: Date;
        dosisCantidad: number;
        dosisUnidad: string;
        areaAplicadaHa: number;
        metodoAplicacion: MetodoAplicacion;
        operadorNombre: string;
        costoTotal: number;
        observaciones: string;
      };

      await this.aplicacionesService.registrar({
        loteId: values.loteId,
        insumoId: values.insumoId,
        fechaAplicacion: values.fechaAplicacion.toISOString(),
        dosis: {
          cantidad: values.dosisCantidad,
          unidad: values.dosisUnidad as AplicacionQuimico['dosis']['unidad']
        },
        areaAplicadaHa: values.areaAplicadaHa,
        metodoAplicacion: values.metodoAplicacion,
        operadorNombre: values.operadorNombre,
        costoTotal: values.costoTotal ?? 0,
        observaciones: values.observaciones || undefined
      });

      this.estadoGuardado.set({ guardando: false, error: null, exito: true });

      const mensaje = this.modoOffline()
        ? 'Aplicacion guardada. Se sincronizara cuando haya conexion.'
        : 'Aplicacion registrada exitosamente.';

      this.snackBar.open(mensaje, 'Aceptar', {
        duration: 4_000,
        panelClass: this.modoOffline() ? ['snack-offline'] : ['snack-exito']
      });

      await this.router.navigate(['/aplicaciones']);
    } catch (error) {
      const mensaje = error instanceof Error ? error.message : 'Error al guardar la aplicacion';
      this.estadoGuardado.set({ guardando: false, error: mensaje, exito: false });
    }
  }

  cancelar(): void {
    void this.router.navigate(['/aplicaciones']);
  }

  // -------------------------------------------------------------------------
  // Metodos privados
  // -------------------------------------------------------------------------

  private async cargarDatosMaestros(): Promise<void> {
    this.cargandoDatos.set(true);

    try {
      const [lotesResult, insumosResult] = await Promise.all([
        this.lotesService.obtenerResumen(),
        this.insumosService.obtenerResumen()
      ]);

      this.lotes.set(lotesResult);
      this.insumos.set(insumosResult);
    } catch {
      // Los datos se cargan desde RxDB, error solo si la BD no fue inicializada
      this.lotes.set([]);
      this.insumos.set([]);
    } finally {
      this.cargandoDatos.set(false);
    }
  }

  /**
   * Verifica localmente si el insumo tiene periodo de carencia activo en el lote.
   * Bloqueo preventivo offline segun offline-sync-flow.md "Escenario Critico 1".
   */
  private async evaluarPeriodoCarencia(): Promise<void> {
    const loteId = this.form.get('loteId')?.value as string | null;
    if (!loteId) {
      this.alertaCarencia.set({ activa: false, mensaje: '' });
      return;
    }

    // Usamos la fecha de hoy para la verificacion de carencia de cosecha
    // (el agricultor quiere saber si PUEDE cosechar pronto, no si puede aplicar)
    const hoy = new Date();
    const resultado = await this.aplicacionesService.verificarPeriodoCarencia(loteId, hoy);

    if (resultado.bloqueada && resultado.aplicacion) {
      const finCarencia = resultado.aplicacion.periodoCarencia?.fechaFinCarencia;
      const diasRestantes = resultado.diasRestantes ?? 0;
      this.alertaCarencia.set({
        activa: true,
        mensaje: `Cosecha bloqueada por periodo de carencia. Quedan ${diasRestantes} dia(s).`,
        fechaFin: finCarencia,
        diasRestantes
      });
    } else {
      this.alertaCarencia.set({ activa: false, mensaje: '' });
    }
  }
}

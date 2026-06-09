// src/app/features/costos/pages/nuevo-costo/nuevo-costo.component.ts
// Registro de costos por lote con soporte offline completo.
// Si online: POST /api/costos. Si offline: encolarOperacion.
// Validacion: monto > 0, loteId requerido.

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
import { v4 as uuidv4 } from 'uuid';
import { firstValueFrom } from 'rxjs';
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
import { MatDividerModule } from '@angular/material/divider';

import { SyncService } from '../../../../core/services/sync.service';
import { ApiService } from '../../../../core/services/api.service';
import { RxDBService } from '../../../../core/database/rxdb.service';
import { LotesService } from '../../../lotes/services/lotes.service';
import { SyncStatusBadgeComponent } from '../../../../shared/components/sync-status-badge/sync-status-badge.component';
import { OfflineBannerComponent } from '../../../../shared/components/offline-banner/offline-banner.component';
import { LoteResumen } from '../../../../core/models/lote.model';
import { CategoriaCosto } from '../../../../core/models/rentabilidad.model';
import { CostoDocType } from '../../../../core/database/rxdb-schemas';

/** Payload que va al API o a la cola de sync */
interface CostoPayload {
  id: string;
  loteId: string;
  fecha: string;
  categoria: CategoriaCosto;
  monto: number;
  descripcion: string;
}

/** Estado del proceso de guardado */
interface EstadoGuardado {
  guardando: boolean;
  error: string | null;
  exito: boolean;
}

@Component({
  selector: 'app-nuevo-costo',
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
    MatDividerModule,
    SyncStatusBadgeComponent,
    OfflineBannerComponent
  ],
  templateUrl: './nuevo-costo.component.html',
  styleUrl: './nuevo-costo.component.scss'
})
export class NuevoCostoComponent implements OnInit {
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

  // Computed del SyncService para la UI
  readonly modoOffline = computed(() => !this.syncService.isOnline());
  readonly pendientesSync = computed(() => this.syncService.pendingCount());

  // -------------------------------------------------------------------------
  // Formulario reactivo
  // -------------------------------------------------------------------------

  readonly form: FormGroup = this.fb.group({
    loteId:      ['', [Validators.required]],
    fecha:       [new Date(), [Validators.required]],
    categoria:   ['INSUMOS_QUIMICOS', [Validators.required]],
    monto:       [null, [Validators.required, Validators.min(0.01)]],
    descripcion: ['', [Validators.required, Validators.minLength(3), Validators.maxLength(500)]]
  });

  /** Categorias de costo con etiquetas en espanol */
  readonly categorias: Array<{ valor: CategoriaCosto; etiqueta: string; icono: string }> = [
    { valor: 'INSUMOS_QUIMICOS', etiqueta: 'Insumos Quimicos',  icono: 'science' },
    { valor: 'MANO_DE_OBRA',     etiqueta: 'Mano de Obra',       icono: 'people' },
    { valor: 'TRANSPORTE',       etiqueta: 'Transporte',          icono: 'local_shipping' },
    { valor: 'RIEGO',            etiqueta: 'Riego',               icono: 'water_drop' },
    { valor: 'MAQUINARIA',       etiqueta: 'Maquinaria',          icono: 'agriculture' },
    { valor: 'OTROS',            etiqueta: 'Otros',               icono: 'more_horiz' }
  ];

  /** Fecha maxima permitida (hoy) */
  readonly fechaMaxima = new Date();

  /** Fecha minima (inicio del ano actual) */
  readonly fechaMinima = new Date(new Date().getFullYear(), 0, 1);

  ngOnInit(): void {
    void this.cargarLotes();
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
      fecha: Date;
      categoria: CategoriaCosto;
      monto: number;
      descripcion: string;
    };

    const costoId = uuidv4();
    const fechaStr = values.fecha.toISOString().split('T')[0];

    const payload: CostoPayload = {
      id: costoId,
      loteId: values.loteId,
      fecha: fechaStr,
      categoria: values.categoria,
      monto: values.monto,
      descripcion: values.descripcion.trim()
    };

    try {
      if (this.syncService.isOnline()) {
        // Online: POST directo al API
        await firstValueFrom(
          this.api.post<CostoPayload, unknown>('/api/costos', payload)
        );
      } else {
        // Offline: encolar para sync posterior
        await this.syncService.encolarOperacion(
          'CREAR_COSTO',
          costoId,
          'CostoLote',
          payload as unknown as Record<string, unknown>
        );
      }

      // Persistir en RxDB local
      const doc: CostoDocType = {
        id: costoId,
        loteId: values.loteId,
        fecha: fechaStr,
        categoria: values.categoria,
        descripcion: values.descripcion.trim(),
        monto: values.monto,
        aplicacionId: null,
        cosechaId: null,
        creadoOffline: !this.syncService.isOnline(),
        clientTimestamp: new Date().toISOString(),
        eliminado: false,
        rowVersion: null,
        syncStatus: this.syncService.isOnline() ? 'SYNCED' : 'PENDIENTE'
      };

      await this.rxdb.costos.insert(doc);

      this.estadoGuardado.set({ guardando: false, error: null, exito: true });

      const mensaje = this.modoOffline()
        ? 'Costo guardado. Se sincronizara cuando haya conexion.'
        : 'Costo registrado exitosamente.';

      this.snackBar.open(mensaje, 'Aceptar', {
        duration: 4_000,
        panelClass: this.modoOffline() ? ['snack-offline'] : ['snack-exito']
      });

      await this.router.navigate(['/costos']);
    } catch (error) {
      const mensaje = error instanceof Error ? error.message : 'Error al guardar el costo';
      this.estadoGuardado.set({ guardando: false, error: mensaje, exito: false });
    }
  }

  cancelar(): void {
    void this.router.navigate(['/costos']);
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
}

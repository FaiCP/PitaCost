// src/app/features/lotes/pages/lista-lotes/lista-lotes.component.ts
// Pagina de listado de lotes. Lee desde RxDB via LotesService (offline-first).
// Usa Signals para estado local y OnPush para rendimiento optimo.
// Se suscribe al observable reactivo obtenerTodos$() para recibir actualizaciones
// de sync en tiempo real sin polling manual.

import {
  Component,
  ChangeDetectionStrategy,
  inject,
  signal,
  OnInit,
  DestroyRef
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { DecimalPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatChipsModule } from '@angular/material/chips';
import { MatTooltipModule } from '@angular/material/tooltip';

import { LotesService } from '../../services/lotes.service';
import { SyncService } from '../../../../core/services/sync.service';
import { Lote, EstadoSyncEntidad } from '../../../../core/models/lote.model';
import { OfflineBannerComponent } from '../../../../shared/components/offline-banner/offline-banner.component';
import { SyncStatusBadgeComponent } from '../../../../shared/components/sync-status-badge/sync-status-badge.component';

interface SyncVisual {
  label: string;
  clase: string;
  tooltip: string;
}

const SYNC_VISUAL: Record<EstadoSyncEntidad, SyncVisual> = {
  SYNCED:    { label: 'Guardado',  clase: 'synced',    tooltip: 'Sincronizado con el servidor' },
  PENDIENTE: { label: 'Pendiente', clase: 'pendiente', tooltip: 'Pendiente de sincronizar' },
  CONFLICTO: { label: 'Conflicto', clase: 'conflicto', tooltip: 'Conflicto de sincronizacion' },
  RECHAZADO: { label: 'Rechazado', clase: 'rechazado', tooltip: 'Rechazado por el servidor' }
};

@Component({
  selector: 'app-lista-lotes',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DecimalPipe,
    RouterLink,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatChipsModule,
    MatTooltipModule,
    OfflineBannerComponent,
    SyncStatusBadgeComponent
  ],
  template: `
    <app-offline-banner />

    <div class="lista-lotes">
      <header class="lista-lotes__header">
        <h1 class="lista-lotes__titulo">Mis Lotes</h1>
        <app-sync-status-badge />
      </header>

      <!-- Estado de carga -->
      @if (cargando()) {
        <div class="lista-lotes__estado-central" role="status" aria-label="Cargando lotes">
          <mat-spinner diameter="48" />
          <p>Cargando lotes...</p>
        </div>
      }

      <!-- Estado de error -->
      @if (!cargando() && error()) {
        <div class="lista-lotes__estado-central lista-lotes__estado-central--error" role="alert">
          <mat-icon>error_outline</mat-icon>
          <p>{{ error() }}</p>
          <button mat-stroked-button (click)="iniciarCarga()">
            <mat-icon>refresh</mat-icon>
            Reintentar
          </button>
        </div>
      }

      <!-- Estado vacio -->
      @if (!cargando() && !error() && lotes().length === 0) {
        <div class="lista-lotes__estado-central" role="status">
          <mat-icon class="lista-lotes__icono-vacio">grass</mat-icon>
          <h2>Sin lotes registrados</h2>
          <p>Crea tu primer lote para comenzar a registrar costos y cosechas.</p>
          <button mat-flat-button color="primary" routerLink="/lotes/nuevo">
            <mat-icon>add</mat-icon>
            Crear primer lote
          </button>
        </div>
      }

      <!-- Lista de lotes -->
      @if (!cargando() && !error() && lotes().length > 0) {
        <ul class="lista-lotes__grid" role="list">
          @for (lote of lotes(); track lote.id) {
            <li>
              <mat-card class="lote-card" role="article">
                <mat-card-header>
                  <mat-card-title>{{ lote.nombre }}</mat-card-title>
                  <mat-card-subtitle>{{ lote.cultivo }}</mat-card-subtitle>
                </mat-card-header>

                <mat-card-content>
                  <p class="lote-card__dato">
                    <mat-icon aria-hidden="true">straighten</mat-icon>
                    {{ lote.areaHa | number:'1.1-2' }} ha
                  </p>
                  @if (lote.fechaInicioSiembra) {
                    <p class="lote-card__dato">
                      <mat-icon aria-hidden="true">event</mat-icon>
                      Siembra: {{ lote.fechaInicioSiembra }}
                    </p>
                  }
                </mat-card-content>

                <mat-card-actions align="end">
                  @if (lote.syncStatus) {
                    <mat-chip
                      [class]="'lote-card__sync-chip lote-card__sync-chip--' + syncVisual(lote.syncStatus).clase"
                      [matTooltip]="syncVisual(lote.syncStatus).tooltip"
                      [attr.aria-label]="'Sincronizacion: ' + syncVisual(lote.syncStatus).tooltip"
                      disableRipple
                    >
                      {{ syncVisual(lote.syncStatus).label }}
                    </mat-chip>
                  }
                </mat-card-actions>
              </mat-card>
            </li>
          }
        </ul>
      }
    </div>

    <!-- FAB: nuevo lote -->
    <button
      mat-fab
      color="primary"
      class="lista-lotes__fab"
      routerLink="/lotes/nuevo"
      aria-label="Crear nuevo lote"
      matTooltip="Nuevo lote"
    >
      <mat-icon>add</mat-icon>
    </button>
  `,
  styles: [`
    .lista-lotes {
      padding: 16px;
      max-width: 800px;
      margin: 0 auto;
      padding-bottom: 96px;

      &__header {
        display: flex;
        align-items: center;
        justify-content: space-between;
        margin-bottom: 20px;
        flex-wrap: wrap;
        gap: 8px;
      }

      &__titulo {
        font-size: 1.5rem;
        font-weight: 600;
        color: #2e7d32;
        margin: 0;
      }

      &__estado-central {
        display: flex;
        flex-direction: column;
        align-items: center;
        gap: 16px;
        padding: 48px 16px;
        text-align: center;
        color: #555;

        h2 {
          font-size: 1.25rem;
          font-weight: 600;
          margin: 0;
          color: #333;
        }

        p {
          margin: 0;
          color: #666;
          max-width: 280px;
        }

        &--error {
          color: #c62828;

          mat-icon {
            font-size: 48px;
            height: 48px;
            width: 48px;
          }
        }
      }

      &__icono-vacio {
        font-size: 64px;
        height: 64px;
        width: 64px;
        color: #a5d6a7;
      }

      &__grid {
        list-style: none;
        margin: 0;
        padding: 0;
        display: grid;
        grid-template-columns: 1fr;
        gap: 12px;

        @media (min-width: 600px) {
          grid-template-columns: repeat(2, 1fr);
        }

        @media (min-width: 960px) {
          grid-template-columns: repeat(3, 1fr);
        }
      }

      &__fab {
        position: fixed;
        bottom: 80px;
        right: 16px;
        z-index: 100;
      }
    }

    .lote-card {
      height: 100%;
      border-left: 4px solid #4caf50;
      transition: box-shadow 0.2s;

      &:hover {
        box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
      }

      &__dato {
        display: flex;
        align-items: center;
        gap: 4px;
        color: #555;
        font-size: 0.9rem;
        margin: 6px 0 0;

        mat-icon {
          font-size: 16px;
          height: 16px;
          width: 16px;
          color: #4caf50;
        }
      }

      &__sync-chip {
        font-size: 0.7rem;
        min-height: 22px;
        cursor: default;

        &--synced    { background-color: #e8f5e9; color: #2e7d32; }
        &--pendiente { background-color: #fff8e1; color: #e65100; }
        &--conflicto { background-color: #fce4ec; color: #880e4f; }
        &--rechazado { background-color: #fbe9e7; color: #bf360c; }
      }
    }
  `]
})
export class ListaLotesComponent implements OnInit {
  private readonly lotesService = inject(LotesService);
  private readonly destroyRef = inject(DestroyRef);
  readonly syncService = inject(SyncService);

  readonly lotes = signal<Lote[]>([]);
  readonly cargando = signal(true);
  readonly error = signal<string | null>(null);

  ngOnInit(): void {
    this.iniciarCarga();
  }

  iniciarCarga(): void {
    this.cargando.set(true);
    this.error.set(null);

    // obtenerTodos$() emite cada vez que RxDB actualiza (sync incluido),
    // por lo que la lista se refresca automaticamente sin polling.
    this.lotesService.obtenerTodos$()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (lista) => {
          this.lotes.set(lista);
          this.cargando.set(false);
        },
        error: () => {
          this.error.set('No se pudieron cargar los lotes. Verifica tu conexion.');
          this.cargando.set(false);
        }
      });
  }

  syncVisual(status: EstadoSyncEntidad | undefined): SyncVisual {
    return SYNC_VISUAL[status ?? 'SYNCED'];
  }
}

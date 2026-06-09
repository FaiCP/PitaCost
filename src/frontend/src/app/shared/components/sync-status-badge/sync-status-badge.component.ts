// src/app/shared/components/sync-status-badge/sync-status-badge.component.ts
// Indicador visual del estado de sincronizacion: punto verde/rojo/amarillo
// con tooltip descriptivo. Componente presentacional reutilizable en el header.

import { Component, computed, inject, ChangeDetectionStrategy } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { SyncService } from '../../../core/services/sync.service';
import { EstadoSync } from '../../../core/models/sync.model';

interface EstadoVisual {
  icono: string;
  clase: string;
  tooltip: string;
}

@Component({
  selector: 'app-sync-status-badge',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatIconModule, MatTooltipModule, MatProgressSpinnerModule],
  template: `
    <div
      class="sync-badge"
      [class]="'sync-badge sync-badge--' + estadoVisual().clase"
      [matTooltip]="estadoVisual().tooltip"
      role="status"
      [attr.aria-label]="estadoVisual().tooltip"
    >
      @if (estadoSync() === 'syncing') {
        <mat-spinner diameter="14" class="sync-badge__spinner" />
      } @else {
        <span class="sync-badge__dot" aria-hidden="true"></span>
      }
      <span class="sync-badge__texto">{{ estadoVisual().tooltip }}</span>
    </div>
  `,
  styles: [`
    .sync-badge {
      display: inline-flex;
      align-items: center;
      gap: 5px;
      font-size: 0.75rem;
      font-weight: 500;
      padding: 2px 8px;
      border-radius: 12px;
      cursor: default;

      .sync-badge__dot {
        width: 8px;
        height: 8px;
        border-radius: 50%;
        display: inline-block;
        flex-shrink: 0;
      }

      .sync-badge__spinner {
        flex-shrink: 0;
      }

      .sync-badge__texto {
        white-space: nowrap;
        /* Ocultar texto en pantallas muy pequeñas, mantener solo el punto */
        @media (max-width: 360px) {
          display: none;
        }
      }

      &.sync-badge--online {
        background-color: #e8f5e9;
        color: #2e7d32;
        .sync-badge__dot { background-color: #4caf50; }
      }

      &.sync-badge--offline {
        background-color: #fbe9e7;
        color: #bf360c;
        .sync-badge__dot { background-color: #f44336; }
      }

      &.sync-badge--syncing {
        background-color: #e3f2fd;
        color: #1565c0;
      }

      &.sync-badge--error {
        background-color: #fff8e1;
        color: #e65100;
        .sync-badge__dot { background-color: #ff9800; }
      }
    }
  `]
})
export class SyncStatusBadgeComponent {
  private readonly syncService = inject(SyncService);

  readonly estadoSync = computed(() => this.syncService.syncStatus());
  readonly isOnline = computed(() => this.syncService.isOnline());

  readonly estadoVisual = computed<EstadoVisual>(() => {
    const sync: EstadoSync = this.estadoSync();
    const online = this.isOnline();

    if (sync === 'syncing') {
      return { icono: 'sync', clase: 'syncing', tooltip: 'Sincronizando...' };
    }
    if (sync === 'error') {
      return { icono: 'sync_problem', clase: 'error', tooltip: 'Error de sincronizacion' };
    }
    if (!online) {
      return { icono: 'cloud_off', clase: 'offline', tooltip: 'Sin conexion' };
    }
    return { icono: 'cloud_done', clase: 'online', tooltip: 'Conectado' };
  });
}

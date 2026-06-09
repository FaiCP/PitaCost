// src/app/shared/components/offline-banner/offline-banner.component.ts
// Banner global de modo offline. Se muestra en la parte superior de cada pagina
// cuando el dispositivo pierde conexion. Componente presentacional puro (dumb).

import { Component, computed, inject, ChangeDetectionStrategy } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { SyncService } from '../../../core/services/sync.service';

@Component({
  selector: 'app-offline-banner',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatIconModule],
  template: `
    @if (modoOffline()) {
      <div class="offline-banner" role="status" aria-live="polite">
        <mat-icon aria-hidden="true">cloud_off</mat-icon>
        <span>
          Sin conexion
          @if (pendientes() > 0) {
            &nbsp;&middot;&nbsp;{{ pendientes() }} cambio{{ pendientes() === 1 ? '' : 's' }} pendiente{{ pendientes() === 1 ? '' : 's' }} de sincronizar
          }
        </span>
      </div>
    }
  `,
  styles: [`
    .offline-banner {
      display: flex;
      align-items: center;
      gap: 8px;
      background-color: #f59e0b;
      color: #fff;
      padding: 8px 16px;
      font-size: 0.875rem;
      font-weight: 500;
      width: 100%;
      box-sizing: border-box;

      mat-icon {
        font-size: 18px;
        height: 18px;
        width: 18px;
        flex-shrink: 0;
      }
    }
  `]
})
export class OfflineBannerComponent {
  private readonly syncService = inject(SyncService);

  readonly modoOffline = computed(() => !this.syncService.isOnline());
  readonly pendientes = computed(() => this.syncService.pendingCount());
}

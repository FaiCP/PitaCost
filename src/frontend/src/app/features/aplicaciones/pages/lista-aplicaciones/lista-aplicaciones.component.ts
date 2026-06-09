// src/app/features/aplicaciones/pages/lista-aplicaciones/lista-aplicaciones.component.ts
// Pantalla de lista de aplicaciones de quimicos.
// Lista simple con enlace a registrar nueva aplicacion.
// Los datos se sirven desde RxDB (disponibles offline).

import {
  Component,
  inject,
  signal,
  OnInit,
  ChangeDetectionStrategy
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { MatListModule } from '@angular/material/list';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { DatePipe, CurrencyPipe, DecimalPipe } from '@angular/common';

import { SyncService } from '../../../../core/services/sync.service';
import { RxDBService } from '../../../../core/database/rxdb.service';
import { OfflineBannerComponent } from '../../../../shared/components/offline-banner/offline-banner.component';
import { AplicacionQuimico } from '../../../../core/models/aplicacion.model';

@Component({
  selector: 'app-lista-aplicaciones',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterLink,
    MatButtonModule,
    MatIconModule,
    MatCardModule,
    MatListModule,
    MatProgressSpinnerModule,
    DatePipe,
    CurrencyPipe,
    DecimalPipe,
    OfflineBannerComponent
  ],
  template: `
    <app-offline-banner />

    <div class="lista-aplicaciones">
      <div class="lista-aplicaciones__header">
        <h1>Aplicaciones</h1>
      </div>

      @if (cargando()) {
        <div class="lista-aplicaciones__loading" role="status" aria-label="Cargando">
          <mat-spinner diameter="40" />
        </div>
      } @else if (aplicaciones().length === 0) {
        <div class="lista-aplicaciones__vacio">
          <mat-icon aria-hidden="true">science</mat-icon>
          <p>No hay aplicaciones registradas.</p>
          <p>Usa el boton + para registrar la primera.</p>
        </div>
      } @else {
        <mat-list role="list">
          @for (aplicacion of aplicaciones(); track aplicacion.id) {
            <mat-list-item role="listitem">
              <mat-icon matListItemIcon aria-hidden="true">science</mat-icon>
              <span matListItemTitle>
                {{ aplicacion.insumoNombre ?? aplicacion.insumoId }}
              </span>
              <span matListItemLine>
                {{ aplicacion.fechaAplicacion | date:'dd/MM/yyyy' }}
                &middot; {{ aplicacion.areaAplicadaHa | number:'1.1-2' }} ha
                &middot; {{ aplicacion.costoTotal | currency:'USD' }}
              </span>
              @if (aplicacion.periodoCarencia?.cosechaBloqueada) {
                <span matListItemLine class="lista-aplicaciones__carencia-activa">
                  En carencia
                </span>
              }
            </mat-list-item>
          }
        </mat-list>
      }

      <!-- Boton flotante: Registrar nueva aplicacion -->
      <button
        mat-fab
        color="primary"
        class="lista-aplicaciones__fab"
        routerLink="/aplicaciones/nueva"
        aria-label="Registrar nueva aplicacion"
      >
        <mat-icon>add</mat-icon>
      </button>
    </div>
  `,
  styles: [`
    .lista-aplicaciones {
      padding: 16px 12px 80px;
      max-width: 800px;
      margin: 0 auto;
      position: relative;

      .lista-aplicaciones__header {
        display: flex;
        align-items: center;
        justify-content: space-between;
        margin-bottom: 16px;

        h1 {
          margin: 0;
          font-size: 1.4rem;
          font-weight: 700;
        }
      }

      .lista-aplicaciones__loading {
        display: flex;
        justify-content: center;
        padding: 48px;
      }

      .lista-aplicaciones__vacio {
        display: flex;
        flex-direction: column;
        align-items: center;
        padding: 48px 24px;
        color: #9e9e9e;
        text-align: center;

        mat-icon {
          font-size: 56px;
          height: 56px;
          width: 56px;
          margin-bottom: 16px;
          color: #bdbdbd;
        }

        p { margin: 4px 0; font-size: 0.875rem; }
      }

      .lista-aplicaciones__carencia-activa {
        color: #c62828;
        font-weight: 600;
        font-size: 0.75rem;
      }

      .lista-aplicaciones__fab {
        position: fixed;
        bottom: 80px;
        right: 16px;
        z-index: 50;

        @media (min-width: 768px) {
          bottom: 24px;
          right: 24px;
        }
      }
    }
  `]
})
export class ListaAplicacionesComponent implements OnInit {
  private readonly rxdb = inject(RxDBService);
  readonly syncService = inject(SyncService);

  readonly aplicaciones = signal<AplicacionQuimico[]>([]);
  readonly cargando = signal(true);

  ngOnInit(): void {
    void this.cargar();
  }

  private async cargar(): Promise<void> {
    this.cargando.set(true);
    try {
      const docs = await this.rxdb.aplicaciones
        .find({ sort: [{ fechaAplicacion: 'desc' }], limit: 50 })
        .exec();

      this.aplicaciones.set(
        docs.map(d => {
          const doc = d.toJSON();
          return {
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
            insumoNombre: doc.insumoNombre ?? undefined,
            syncStatus: doc.syncStatus as AplicacionQuimico['syncStatus']
          } as AplicacionQuimico;
        })
      );
    } catch {
      this.aplicaciones.set([]);
    } finally {
      this.cargando.set(false);
    }
  }
}

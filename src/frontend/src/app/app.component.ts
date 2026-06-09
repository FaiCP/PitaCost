// src/app/app.component.ts
// Shell principal de la aplicacion. Contiene el header, la barra de navegacion
// inferior (mobile-first) y el router-outlet. Escucha lastSyncTime para mostrar
// el toast de sincronizacion usando MatSnackBar.

import {
  Component,
  inject,
  computed,
  effect,
  ChangeDetectionStrategy,
  signal
} from '@angular/core';
import { RouterOutlet, RouterLink, RouterLinkActive } from '@angular/router';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatBadgeModule } from '@angular/material/badge';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';

import { SyncService } from './core/services/sync.service';
import { AuthService } from './core/services/auth.service';
import { OfflineBannerComponent } from './shared/components/offline-banner/offline-banner.component';

/** Item de la barra de navegacion inferior */
interface NavItem {
  ruta: string;
  icono: string;
  etiqueta: string;
}

@Component({
  selector: 'app-root',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    MatToolbarModule,
    MatIconModule,
    MatButtonModule,
    MatBadgeModule,
    MatSnackBarModule,
    OfflineBannerComponent
  ],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss'
})
export class AppComponent {
  private readonly snackBar = inject(MatSnackBar);
  readonly syncService = inject(SyncService);
  readonly authService = inject(AuthService);

  // -------------------------------------------------------------------------
  // Signals reactivos del estado global
  // -------------------------------------------------------------------------

  /** true cuando el dispositivo no tiene conexion */
  readonly isOnline = computed(() => this.syncService.isOnline());

  /** Cantidad de operaciones pendientes (para badge en header) */
  readonly pendingCount = computed(() => this.syncService.pendingCount());

  /** true si hay operaciones pendientes de sincronizar */
  readonly hasPending = computed(() => this.syncService.hasPendingChanges());

  /** Indica si el usuario esta autenticado (controla visibilidad del nav) */
  readonly isAuthenticated = computed(() => this.authService.isAuthenticated());

  // -------------------------------------------------------------------------
  // Navegacion inferior
  // -------------------------------------------------------------------------

  readonly navItems: NavItem[] = [
    { ruta: '/dashboard',     icono: 'bar_chart',     etiqueta: 'Dashboard' },
    { ruta: '/lotes',         icono: 'grass',         etiqueta: 'Lotes' },
    { ruta: '/aplicaciones',  icono: 'science',       etiqueta: 'Aplicaciones' },
    { ruta: '/cosechas',      icono: 'agriculture',   etiqueta: 'Cosechas' },
    { ruta: '/costos',        icono: 'receipt_long',  etiqueta: 'Costos' }
  ];

  // -------------------------------------------------------------------------
  // Efecto: toast de sincronizacion completada
  // -------------------------------------------------------------------------

  constructor() {
    // Signal para seguir el valor anterior de lastSyncTime y detectar cambios
    const ultimoSyncConocido = signal<Date | null>(null);

    effect(() => {
      const nuevoSync = this.syncService.lastSyncTime();
      const anterior = ultimoSyncConocido();

      // Solo mostrar si cambio (nueva sincronizacion completada) y hay sesion
      if (nuevoSync && nuevoSync !== anterior && this.isAuthenticated()) {
        ultimoSyncConocido.set(nuevoSync);

        // Calcular cuantas operaciones se sincronizaron en este ciclo
        // (el SyncService ya las elimino de la cola, por eso usamos el badge que acaba de bajar a 0)
        this.snackBar.open(
          'Datos sincronizados correctamente',
          'OK',
          {
            duration: 3_000,
            verticalPosition: 'top',
            panelClass: ['snack-sync-exito']
          }
        );
      }
    });
  }
}

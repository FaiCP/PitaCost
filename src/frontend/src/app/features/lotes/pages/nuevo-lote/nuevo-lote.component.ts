// src/app/features/lotes/pages/nuevo-lote/nuevo-lote.component.ts
// Formulario de creacion de un nuevo lote. Offline-first: persiste en RxDB
// inmediatamente y encola la operacion de sync. Si ya existe un lote, reutiliza
// el fincaId existente; si no, crea la finca primero via API.

import {
  Component,
  ChangeDetectionStrategy,
  inject,
  signal,
  OnInit
} from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import {
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  Validators
} from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';

import { LotesService } from '../../services/lotes.service';

/** Cultivos disponibles en el selector */
const CULTIVOS = [
  'Banano',
  'Cacao',
  'Palma africana',
  'Pitahaya',
  'Maiz',
  'Arroz',
  'Cana de azucar',
  'Cafe',
  'Otro'
] as const;

interface NuevoLoteForm {
  fincaNombre: FormControl<string>;
  nombre: FormControl<string>;
  cultivo: FormControl<string>;
  areaHa: FormControl<number | null>;
  fechaInicioSiembra: FormControl<string>;
}

@Component({
  selector: 'app-nuevo-lote',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterLink,
    ReactiveFormsModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatSnackBarModule
  ],
  template: `
    <div class="nuevo-lote">
      <!-- Header con boton de regreso -->
      <header class="nuevo-lote__header">
        <button
          mat-icon-button
          routerLink="/lotes"
          aria-label="Volver a la lista de lotes"
        >
          <mat-icon>arrow_back</mat-icon>
        </button>
        <h1 class="nuevo-lote__titulo">Nuevo Lote</h1>
      </header>

      <form
        [formGroup]="form"
        (ngSubmit)="guardar()"
        class="nuevo-lote__form"
        novalidate
        aria-label="Formulario de nuevo lote"
      >
        <!-- Nombre de finca: solo si no hay lotes existentes -->
        @if (!tienesFinca()) {
          <mat-form-field appearance="outline" class="nuevo-lote__field">
            <mat-label>Nombre de tu propiedad agricola</mat-label>
            <input
              matInput
              formControlName="fincaNombre"
              placeholder="Ej: Finca La Esperanza"
              autocomplete="organization"
            />
            <mat-hint>Nombre de tu finca o propiedad agricola</mat-hint>
            @if (form.controls.fincaNombre.hasError('required') && form.controls.fincaNombre.touched) {
              <mat-error>El nombre de la finca es obligatorio</mat-error>
            }
          </mat-form-field>
        }

        <!-- Nombre del lote -->
        <mat-form-field appearance="outline" class="nuevo-lote__field">
          <mat-label>Nombre del lote</mat-label>
          <input
            matInput
            formControlName="nombre"
            placeholder="Ej: Lote Norte A"
            autocomplete="off"
          />
          @if (form.controls.nombre.hasError('required') && form.controls.nombre.touched) {
            <mat-error>El nombre del lote es obligatorio</mat-error>
          }
        </mat-form-field>

        <!-- Cultivo -->
        <mat-form-field appearance="outline" class="nuevo-lote__field">
          <mat-label>Cultivo</mat-label>
          <mat-select formControlName="cultivo" aria-label="Seleccionar cultivo">
            @for (cultivo of cultivos; track cultivo) {
              <mat-option [value]="cultivo">{{ cultivo }}</mat-option>
            }
          </mat-select>
          @if (form.controls.cultivo.hasError('required') && form.controls.cultivo.touched) {
            <mat-error>Selecciona un cultivo</mat-error>
          }
        </mat-form-field>

        <!-- Area en hectareas -->
        <mat-form-field appearance="outline" class="nuevo-lote__field">
          <mat-label>Area del lote</mat-label>
          <input
            matInput
            type="number"
            formControlName="areaHa"
            placeholder="0.0"
            min="0.1"
            step="0.1"
            aria-label="Area del lote en hectareas"
          />
          <span matTextSuffix>ha</span>
          @if (form.controls.areaHa.hasError('required') && form.controls.areaHa.touched) {
            <mat-error>El area es obligatoria</mat-error>
          }
          @if (form.controls.areaHa.hasError('min') && form.controls.areaHa.touched) {
            <mat-error>El area minima es 0.1 ha</mat-error>
          }
        </mat-form-field>

        <!-- Fecha de inicio de siembra -->
        <mat-form-field appearance="outline" class="nuevo-lote__field">
          <mat-label>Fecha de inicio de siembra</mat-label>
          <input
            matInput
            type="date"
            formControlName="fechaInicioSiembra"
            aria-label="Fecha de inicio de siembra"
          />
          @if (form.controls.fechaInicioSiembra.hasError('required') && form.controls.fechaInicioSiembra.touched) {
            <mat-error>La fecha de siembra es obligatoria</mat-error>
          }
        </mat-form-field>

        <!-- Botones de accion -->
        <div class="nuevo-lote__acciones">
          <button
            mat-stroked-button
            type="button"
            routerLink="/lotes"
            [disabled]="guardando()"
          >
            Cancelar
          </button>

          <button
            mat-flat-button
            color="primary"
            type="submit"
            [disabled]="form.invalid || guardando()"
            aria-label="Guardar nuevo lote"
          >
            @if (guardando()) {
              <mat-spinner diameter="20" />
              <span>Guardando...</span>
            } @else {
              <mat-icon>save</mat-icon>
              <span>Guardar lote</span>
            }
          </button>
        </div>
      </form>
    </div>
  `,
  styles: [`
    .nuevo-lote {
      padding: 16px;
      max-width: 560px;
      margin: 0 auto;

      &__header {
        display: flex;
        align-items: center;
        gap: 8px;
        margin-bottom: 24px;
      }

      &__titulo {
        font-size: 1.375rem;
        font-weight: 600;
        color: #2e7d32;
        margin: 0;
      }

      &__form {
        display: flex;
        flex-direction: column;
        gap: 4px;
      }

      &__field {
        width: 100%;
      }

      &__acciones {
        display: flex;
        justify-content: flex-end;
        gap: 12px;
        margin-top: 16px;
        flex-wrap: wrap;

        button {
          display: flex;
          align-items: center;
          gap: 6px;
          min-width: 120px;
        }
      }
    }
  `]
})
export class NuevoLoteComponent implements OnInit {
  private readonly lotesService = inject(LotesService);
  private readonly router = inject(Router);
  private readonly snackBar = inject(MatSnackBar);

  readonly tienesFinca = signal(false);
  readonly guardando = signal(false);

  readonly cultivos = CULTIVOS;

  readonly form = new FormGroup<NuevoLoteForm>({
    fincaNombre:        new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    nombre:             new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    cultivo:            new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    areaHa:             new FormControl<number | null>(null, [Validators.required, Validators.min(0.1)]),
    fechaInicioSiembra: new FormControl('', { nonNullable: true, validators: [Validators.required] })
  });

  async ngOnInit(): Promise<void> {
    try {
      const lotes = await this.lotesService.obtenerResumen();
      if (lotes.length > 0) {
        this.tienesFinca.set(true);
        // Finca ya existe: el campo fincaNombre no es necesario, limpiar validacion
        this.form.controls.fincaNombre.clearValidators();
        this.form.controls.fincaNombre.updateValueAndValidity();
      }
    } catch {
      // Si falla la carga, asumimos que no hay finca aun
    }
  }

  async guardar(): Promise<void> {
    if (this.form.invalid || this.guardando()) return;

    this.guardando.set(true);

    try {
      const { fincaNombre, nombre, cultivo, areaHa, fechaInicioSiembra } = this.form.getRawValue();

      const fincaId = await this.lotesService.obtenerOCrearFincaId(fincaNombre || 'Mi finca');

      await this.lotesService.crearLote({
        fincaId,
        nombre,
        cultivo,
        areaHa: areaHa ?? 0,
        fechaInicioSiembra
      });

      this.snackBar.open('Lote creado', 'OK', {
        duration: 3_000,
        verticalPosition: 'top',
        panelClass: ['snack-sync-exito']
      });

      await this.router.navigate(['/lotes']);
    } catch {
      this.snackBar.open('Error al guardar el lote. Intenta de nuevo.', 'Cerrar', {
        duration: 5_000,
        verticalPosition: 'top'
      });
    } finally {
      this.guardando.set(false);
    }
  }
}

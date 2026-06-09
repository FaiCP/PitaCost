// src/app/features/auth/pages/login/login.component.ts
// Pagina de login con soporte de Passkeys (WebAuthn) como metodo primario
// y formulario email+password como fallback para dispositivos incompatibles.
// OnPush: todos los cambios de estado se propagan via Signals.

import {
  Component,
  signal,
  inject,
  ChangeDetectionStrategy
} from '@angular/core';
import { CommonModule } from '@angular/common';
import {
  ReactiveFormsModule,
  FormGroup,
  FormControl,
  Validators
} from '@angular/forms';
import { Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatDividerModule } from '@angular/material/divider';
import { MatCardModule } from '@angular/material/card';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';

import { AuthService } from '../../../../core/services/auth.service';
import { ApiService } from '../../../../core/services/api.service';
import { AuthResult } from '../../../../core/models/auth.model';
import { environment } from '../../../../../environments/environment';

/** Modo activo del formulario */
type ModoFormulario = 'login' | 'registro' | 'registro_passkey';

@Component({
  selector: 'app-login',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatDividerModule,
    MatCardModule,
    MatSnackBarModule
  ],
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss'
})
export class LoginComponent {
  readonly authService = inject(AuthService);
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);
  private readonly snackBar = inject(MatSnackBar);

  /** true en entorno de desarrollo — muestra login directo sin Passkey */
  readonly isDevMode = !environment.production;

  // -------------------------------------------------------------------------
  // Signals de estado del componente
  // -------------------------------------------------------------------------

  /** true si el navegador soporta WebAuthn (Passkeys) */
  readonly supportsPasskeys = signal<boolean>(
    typeof window !== 'undefined' &&
    !!window.PublicKeyCredential &&
    !!navigator.credentials
  );

  /** Controla que seccion del formulario esta visible */
  readonly modoFormulario = signal<ModoFormulario>('login');

  /** Estado de carga durante operaciones async */
  readonly isLoading = signal(false);

  /** Mensaje de error para mostrar en la UI */
  readonly errorMessage = signal<string | null>(null);

  /** Muestra u oculta la password en el input */
  readonly mostrarPassword = signal(false);

  // -------------------------------------------------------------------------
  // Formulario reactivo de fallback (email + password)
  // -------------------------------------------------------------------------

  readonly loginForm = new FormGroup({
    email: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.email]
    }),
    password: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.minLength(6)]
    })
  });

  /** Formulario de registro con email, nombre y contraseña */
  readonly registroForm = new FormGroup({
    nombreCompleto: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.minLength(3)]
    }),
    email: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.email]
    }),
    password: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.minLength(6)]
    })
  });

  /** Formulario de email solo para iniciar flujo Passkey */
  readonly passkeyForm = new FormGroup({
    email: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.email]
    })
  });

  // -------------------------------------------------------------------------
  // Handlers de eventos del template
  // -------------------------------------------------------------------------

  /** Inicia el flujo de autenticacion con Passkey existente */
  async loginConPasskey(): Promise<void> {
    if (this.passkeyForm.invalid) {
      this.passkeyForm.markAllAsTouched();
      return;
    }

    this.isLoading.set(true);
    this.errorMessage.set(null);

    try {
      const email = this.passkeyForm.controls.email.value;
      await this.authService.loginConPasskey(email);
      await this.router.navigate(['/dashboard']);
    } catch (error) {
      const mensaje = this.resolverMensajeError(error);
      this.errorMessage.set(mensaje);
    } finally {
      this.isLoading.set(false);
    }
  }

  /** Login con email y password (dispositivos sin soporte Passkey) */
  async loginConPassword(): Promise<void> {
    if (this.loginForm.invalid) {
      this.loginForm.markAllAsTouched();
      return;
    }

    this.isLoading.set(true);
    this.errorMessage.set(null);

    try {
      const { email, password } = this.loginForm.getRawValue();
      await this.authService.loginConPassword({ email, password });
      await this.router.navigate(['/dashboard']);
    } catch (error) {
      const mensaje = this.resolverMensajeError(error);
      this.errorMessage.set(mensaje);
    } finally {
      this.isLoading.set(false);
    }
  }

  /** Registra una cuenta nueva con email y contraseña */
  async registrarConPassword(): Promise<void> {
    if (this.registroForm.invalid) {
      this.registroForm.markAllAsTouched();
      return;
    }
    this.isLoading.set(true);
    this.errorMessage.set(null);
    try {
      const { email, password, nombreCompleto } = this.registroForm.getRawValue();
      const resp = await this.api.post<object, AuthResult>(
        '/api/auth/register', { email, password, nombreCompleto }
      ).toPromise();
      if (resp) {
        this.authService['guardarSesion'](resp.data);
        await this.router.navigate(['/dashboard']);
      }
    } catch (error) {
      this.errorMessage.set(this.resolverMensajeError(error));
    } finally {
      this.isLoading.set(false);
    }
  }

  /** Registra una nueva Passkey en el dispositivo */
  async registrarPasskey(): Promise<void> {
    if (this.passkeyForm.invalid) {
      this.passkeyForm.markAllAsTouched();
      return;
    }

    this.isLoading.set(true);
    this.errorMessage.set(null);

    try {
      const email = this.passkeyForm.controls.email.value;
      await this.authService.registrarPasskey(email);

      this.snackBar.open(
        'Huella/Face ID registrado correctamente. Ya puede ingresar.',
        'Aceptar',
        { duration: 5_000, panelClass: ['snack-exito'] }
      );

      await this.router.navigate(['/dashboard']);
    } catch (error) {
      const mensaje = this.resolverMensajeError(error);
      this.errorMessage.set(mensaje);
    } finally {
      this.isLoading.set(false);
    }
  }

  /** Login directo sin WebAuthn — solo visible en Development */
  async devLogin(email: string): Promise<void> {
    if (!email) return;
    this.isLoading.set(true);
    this.errorMessage.set(null);
    try {
      const resp = await this.api.post<{ email: string }, AuthResult>(
        '/api/auth/dev-login', { email }
      ).toPromise();
      if (resp) {
        this.authService['guardarSesion'](resp.data);
        await this.router.navigate(['/dashboard']);
      }
    } catch (error) {
      this.errorMessage.set('Error al iniciar sesion de desarrollo.');
    } finally {
      this.isLoading.set(false);
    }
  }

  /** Alterna visibilidad de la contrasena en el input */
  toggleMostrarPassword(): void {
    this.mostrarPassword.update(v => !v);
  }

  irALogin(): void {
    this.modoFormulario.set('login');
    this.errorMessage.set(null);
  }

  irARegistro(): void {
    this.modoFormulario.set('registro');
    this.errorMessage.set(null);
  }

  /** Muestra el formulario para registrar nueva Passkey */
  mostrarFormularioRegistro(): void {
    this.modoFormulario.set('registro_passkey');
    this.errorMessage.set(null);
  }

  /** Limpia el mensaje de error */
  limpiarError(): void {
    this.errorMessage.set(null);
  }

  // -------------------------------------------------------------------------
  // Metodos privados
  // -------------------------------------------------------------------------

  /**
   * Convierte errores del AuthService en mensajes amigables para el agricultor.
   * WebAuthn lanza errores por nombre (NotAllowedError, AbortError, etc.).
   */
  private resolverMensajeError(error: unknown): string {
    if (error instanceof Error) {
      // Errores especificos de WebAuthn
      if (error.name === 'NotAllowedError') {
        return 'Autenticacion cancelada o no permitida. Intente nuevamente.';
      }
      if (error.name === 'AbortError') {
        return 'La autenticacion fue interrumpida. Intente nuevamente.';
      }
      if (error.name === 'NotSupportedError') {
        return 'Este dispositivo no soporta autenticacion biometrica.';
      }
      if (error.name === 'SecurityError') {
        return 'Error de seguridad. Verifique que accede desde una conexion segura (HTTPS).';
      }
      if (error.message.includes('Credenciales incorrectas') ||
          error.message.includes('UNAUTHORIZED')) {
        return 'Email o contrasena incorrectos.';
      }
      return error.message;
    }
    return 'Error inesperado. Por favor intente nuevamente.';
  }
}

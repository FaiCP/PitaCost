// src/app/core/services/auth.service.ts
// Autenticacion via WebAuthn/Passkeys con fallback a contrasena.
// Implementa el flujo completo de api-contract.md secciones 5 y 6.
// El JWT se almacena en memoria (no localStorage) para evitar XSS.

import { Injectable, signal, computed, inject } from '@angular/core';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ApiService } from './api.service';
import {
  Agricultor,
  AuthResult,
  Credentials,
  ChallengeRequest,
  ChallengeResponse,
  VerifyPasskeyRequest,
  JwtClaims
} from '../models/auth.model';

/** Estado de autenticacion con metadata de error */
export interface EstadoAuth {
  cargando: boolean;
  error: string | null;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);

  // -------------------------------------------------------------------------
  // Signals de estado
  // -------------------------------------------------------------------------

  /** Usuario autenticado actualmente (null = no autenticado) */
  readonly currentUser = signal<Agricultor | null>(null);

  /** Estado del proceso de autenticacion */
  readonly estadoAuth = signal<EstadoAuth>({ cargando: false, error: null });

  /** Computed: true si hay un usuario autenticado */
  readonly isAuthenticated = computed(() => !!this.currentUser());

  /** Computed: true si WebAuthn esta disponible en este dispositivo/navegador */
  readonly passkeyDisponible = computed(() =>
    typeof window !== 'undefined' &&
    !!window.PublicKeyCredential &&
    !!navigator.credentials
  );

  /** Token JWT en memoria (NO en localStorage para evitar XSS) */
  private accessToken: string | null = null;
  private refreshToken: string | null = null;

  constructor() {
    // Intentar restaurar sesion desde sessionStorage al cargar la app
    this.restaurarSesion();
  }

  // -------------------------------------------------------------------------
  // API publica
  // -------------------------------------------------------------------------

  /**
   * Registra una nueva Passkey para el usuario.
   * Flujo: POST /auth/challenge (REGISTRO) -> WebAuthn -> POST /auth/verify
   */
  async registrarPasskey(email: string): Promise<void> {
    this.estadoAuth.set({ cargando: true, error: null });

    try {
      // 1. Obtener challenge del servidor
      const challengeResp = await firstValueFrom(
        this.api.post<ChallengeRequest, ChallengeResponse>('/api/auth/challenge', {
          email,
          tipo: 'REGISTRO'
        })
      );
      const challenge = challengeResp.data;

      if (!challenge.publicKeyCredentialCreationOptions) {
        throw new Error('El servidor no retorno opciones de creacion de credencial');
      }

      // 2. Invocar la WebAuthn API del navegador para crear la credencial
      const opts = challenge.publicKeyCredentialCreationOptions;
      const credential = await navigator.credentials.create({
        publicKey: {
          challenge: this.base64UrlToBuffer(opts.challenge),
          rp: opts.rp,
          user: {
            id: this.base64UrlToBuffer(opts.user.id),
            name: opts.user.name,
            displayName: opts.user.displayName
          },
          pubKeyCredParams: opts.pubKeyCredParams,
          timeout: opts.timeout,
          authenticatorSelection: {
            authenticatorAttachment: 'platform',
            residentKey: 'required',
            userVerification: 'required'
          },
          attestation: 'none'
        }
      }) as PublicKeyCredential | null;

      if (!credential) {
        throw new Error('El usuario cancelo el registro de Passkey');
      }

      // 3. Verificar con el servidor
      const authResp = credential.response as AuthenticatorAttestationResponse;
      const verifyRequest: VerifyPasskeyRequest = {
        challengeId: challenge.challengeId,
        tipo: 'REGISTRO',
        credential: {
          id: credential.id,
          rawId: this.bufferToBase64Url(credential.rawId),
          type: 'public-key',
          response: {
            clientDataJSON: this.bufferToBase64Url(authResp.clientDataJSON),
            attestationObject: this.bufferToBase64Url(authResp.attestationObject)
          }
        },
        deviceInfo: this.obtenerDeviceInfo()
      };

      const authResult = await firstValueFrom(
        this.api.post<VerifyPasskeyRequest, AuthResult>('/api/auth/verify', verifyRequest)
      );

      this.guardarSesion(authResult.data);
      this.estadoAuth.set({ cargando: false, error: null });
    } catch (error) {
      const mensaje = error instanceof Error ? error.message : 'Error al registrar Passkey';
      this.estadoAuth.set({ cargando: false, error: mensaje });
      throw error;
    }
  }

  /**
   * Autentica al usuario usando una Passkey existente.
   * Flujo: POST /auth/challenge (AUTENTICACION) -> WebAuthn -> POST /auth/verify
   */
  async loginConPasskey(email: string): Promise<AuthResult> {
    this.estadoAuth.set({ cargando: true, error: null });

    try {
      // 1. Challenge del servidor
      const challengeResp = await firstValueFrom(
        this.api.post<ChallengeRequest, ChallengeResponse>('/api/auth/challenge', {
          email,
          tipo: 'AUTENTICACION'
        })
      );
      const challenge = challengeResp.data;

      if (!challenge.publicKeyCredentialRequestOptions) {
        throw new Error('El servidor no retorno opciones de autenticacion');
      }

      // 2. Invocar WebAuthn para obtener la asercion
      const opts = challenge.publicKeyCredentialRequestOptions;
      const credential = await navigator.credentials.get({
        publicKey: {
          challenge: this.base64UrlToBuffer(opts.challenge),
          timeout: opts.timeout,
          rpId: opts.rpId,
          allowCredentials: opts.allowCredentials.map(c => ({
            id: this.base64UrlToBuffer(c.id),
            type: c.type as PublicKeyCredentialType,
            transports: c.transports as AuthenticatorTransport[]
          })),
          userVerification: opts.userVerification as UserVerificationRequirement
        }
      }) as PublicKeyCredential | null;

      if (!credential) {
        throw new Error('El usuario cancelo la autenticacion');
      }

      // 3. Verificar con el servidor
      const assertResponse = credential.response as AuthenticatorAssertionResponse;
      const verifyRequest: VerifyPasskeyRequest = {
        challengeId: challenge.challengeId,
        tipo: 'AUTENTICACION',
        credential: {
          id: credential.id,
          rawId: this.bufferToBase64Url(credential.rawId),
          type: 'public-key',
          response: {
            authenticatorData: this.bufferToBase64Url(assertResponse.authenticatorData),
            clientDataJSON: this.bufferToBase64Url(assertResponse.clientDataJSON),
            signature: this.bufferToBase64Url(assertResponse.signature)
          }
        },
        deviceInfo: this.obtenerDeviceInfo()
      };

      const authResult = await firstValueFrom(
        this.api.post<VerifyPasskeyRequest, AuthResult>('/api/auth/verify', verifyRequest)
      );

      this.guardarSesion(authResult.data);
      this.estadoAuth.set({ cargando: false, error: null });
      return authResult.data;
    } catch (error) {
      const mensaje = error instanceof Error ? error.message : 'Error al autenticar con Passkey';
      this.estadoAuth.set({ cargando: false, error: mensaje });
      throw error;
    }
  }

  /**
   * Login con email y contrasena (fallback para dispositivos sin Passkey).
   * Este endpoint no esta en el api-contract MVP, pero se provee como fallback.
   */
  async loginConPassword(credentials: Credentials): Promise<AuthResult> {
    this.estadoAuth.set({ cargando: true, error: null });

    try {
      const result = await firstValueFrom(
        this.api.post<Credentials, AuthResult>('/api/auth/login', credentials)
      );
      this.guardarSesion(result.data);
      this.estadoAuth.set({ cargando: false, error: null });
      return result.data;
    } catch (error) {
      const mensaje = error instanceof Error ? error.message : 'Credenciales incorrectas';
      this.estadoAuth.set({ cargando: false, error: mensaje });
      throw error;
    }
  }

  /** Cierra la sesion del usuario y limpia el estado */
  async logout(): Promise<void> {
    this.accessToken = null;
    this.refreshToken = null;
    this.currentUser.set(null);
    sessionStorage.removeItem('pitasmart_session');
    await this.router.navigate(['/auth/login']);
  }

  /** Retorna el access token actual para el interceptor HTTP */
  getAccessToken(): string | null {
    return this.accessToken;
  }

  // -------------------------------------------------------------------------
  // Metodos privados
  // -------------------------------------------------------------------------

  private guardarSesion(authResult: AuthResult): void {
    this.accessToken = authResult.accessToken;
    this.refreshToken = authResult.refreshToken;
    this.currentUser.set(authResult.usuario);

    // Persistir en sessionStorage (no localStorage) para restaurar en recarga
    // NOTA: sessionStorage es vulnerable a XSS igual que localStorage,
    // pero es aceptable para MVP. En produccion usar HttpOnly cookies.
    sessionStorage.setItem('pitasmart_session', JSON.stringify({
      usuario: authResult.usuario,
      accessToken: authResult.accessToken,
      refreshToken: authResult.refreshToken,
      expiresAt: Date.now() + authResult.expiresIn * 1_000
    }));
  }

  private restaurarSesion(): void {
    try {
      const raw = sessionStorage.getItem('pitasmart_session');
      if (!raw) return;

      const session = JSON.parse(raw) as {
        usuario: Agricultor;
        accessToken: string;
        refreshToken: string;
        expiresAt: number;
      };

      // Verificar que el token no haya expirado
      if (session.expiresAt > Date.now()) {
        this.accessToken = session.accessToken;
        this.refreshToken = session.refreshToken;
        this.currentUser.set(session.usuario);
      } else {
        sessionStorage.removeItem('pitasmart_session');
      }
    } catch {
      sessionStorage.removeItem('pitasmart_session');
    }
  }

  private obtenerDeviceInfo() {
    return {
      deviceId: localStorage.getItem(environment.deviceIdStorageKey) ?? 'unknown',
      platform: navigator.platform ?? 'Unknown',
      appVersion: environment.appVersion
    };
  }

  /** Convierte base64url a ArrayBuffer (necesario para WebAuthn API) */
  private base64UrlToBuffer(base64url: string): ArrayBuffer {
    const base64 = base64url.replace(/-/g, '+').replace(/_/g, '/');
    const binary = atob(base64);
    const buffer = new ArrayBuffer(binary.length);
    const view = new Uint8Array(buffer);
    for (let i = 0; i < binary.length; i++) {
      view[i] = binary.charCodeAt(i);
    }
    return buffer;
  }

  /** Convierte ArrayBuffer a base64url (para serializar credencial WebAuthn) */
  private bufferToBase64Url(buffer: ArrayBuffer): string {
    const bytes = new Uint8Array(buffer);
    let binary = '';
    for (const byte of bytes) {
      binary += String.fromCharCode(byte);
    }
    return btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=/g, '');
  }
}

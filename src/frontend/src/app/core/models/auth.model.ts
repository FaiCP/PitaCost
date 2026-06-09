// src/app/core/models/auth.model.ts
// Modelos del Bounded Context: Identidad
// Alineados con api-contract.md secciones 5 y 6

/** Roles de usuario del sistema */
export type RolUsuario = 'AGRICULTOR' | 'ADMINISTRADOR' | 'AUDITOR';

/** Resumen de finca incluido en el token JWT */
export interface FincaResumen {
  id: string;
  nombre: string;
  lotes: number;
}

/** Agricultor autenticado extraido del JWT */
export interface Agricultor {
  id: string;
  email: string;
  nombreCompleto: string;
  rol: RolUsuario;
  fincas: FincaResumen[];
}

/** Respuesta exitosa de autenticacion (POST /api/auth/verify) */
export interface AuthResult {
  accessToken: string;
  refreshToken: string;
  expiresIn: number;
  tokenType: 'Bearer';
  usuario: Agricultor;
  passkeyRegistrada?: boolean;
  credentialId?: string;
}

/** Credenciales de fallback para login con contrasena */
export interface Credentials {
  email: string;
  password: string;
}

/** Request para iniciar challenge WebAuthn (POST /api/auth/challenge) */
export interface ChallengeRequest {
  email: string;
  tipo: 'REGISTRO' | 'AUTENTICACION';
}

/** Opciones de autenticacion WebAuthn retornadas por el servidor */
export interface PublicKeyCredentialRequestOptions {
  challenge: string;
  timeout: number;
  rpId: string;
  allowCredentials: Array<{
    id: string;
    type: 'public-key';
    transports: string[];
  }>;
  userVerification: 'required' | 'preferred' | 'discouraged';
}

/** Opciones de creacion de credencial WebAuthn para registro */
export interface PublicKeyCredentialCreationOptions {
  challenge: string;
  rp: { name: string; id: string };
  user: { id: string; name: string; displayName: string };
  pubKeyCredParams: Array<{ alg: number; type: 'public-key' }>;
  timeout: number;
  authenticatorSelection: {
    authenticatorAttachment: string;
    residentKey: string;
    userVerification: string;
  };
  attestation: string;
}

/** Respuesta del endpoint POST /api/auth/challenge */
export interface ChallengeResponse {
  challengeId: string;
  publicKeyCredentialRequestOptions?: PublicKeyCredentialRequestOptions;
  publicKeyCredentialCreationOptions?: PublicKeyCredentialCreationOptions;
  expiresAt: string;
}

/** Request para verificar la respuesta del autenticador WebAuthn */
export interface VerifyPasskeyRequest {
  challengeId: string;
  tipo: 'REGISTRO' | 'AUTENTICACION';
  credential: {
    id: string;
    rawId: string;
    type: 'public-key';
    response: {
      authenticatorData?: string;
      clientDataJSON: string;
      signature?: string;
      attestationObject?: string;
    };
  };
  deviceInfo: {
    deviceId: string;
    platform: string;
    appVersion: string;
  };
}

/** Claims estandar del JWT de PitaSmart */
export interface JwtClaims {
  sub: string;
  email: string;
  name: string;
  role: RolUsuario;
  finca_ids: string[];
  iat: number;
  exp: number;
  iss: string;
  aud: string;
}

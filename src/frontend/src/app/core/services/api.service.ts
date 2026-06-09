// src/app/core/services/api.service.ts
// Wrapper sobre HttpClient con manejo estandarizado de errores, retry y tipado.
// Todos los feature services heredan la logica de comunicacion de aqui.

import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError, retry } from 'rxjs/operators';
import { environment } from '../../../environments/environment';

/** Estructura estandar de respuesta exitosa del API */
export interface ApiResponse<T> {
  success: true;
  data: T;
  timestamp: string;
}

/** Estructura estandar de error del API */
export interface ApiErrorResponse {
  success: false;
  error: {
    code: string;
    message: string;
    details?: Array<{ field: string; message: string }>;
  };
  timestamp: string;
}

/** Error tipado que propaga el ApiService */
export interface ApiError {
  code: string;
  message: string;
  httpStatus: number;
  details?: Array<{ field: string; message: string }>;
}

@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiBaseUrl;

  /** GET tipado con manejo de errores estandarizado */
  get<T>(
    path: string,
    params?: Record<string, string | number | boolean>
  ): Observable<ApiResponse<T>> {
    let httpParams = new HttpParams();
    if (params) {
      Object.entries(params).forEach(([key, value]) => {
        httpParams = httpParams.set(key, String(value));
      });
    }

    return this.http
      .get<ApiResponse<T>>(`${this.baseUrl}${path}`, { params: httpParams })
      .pipe(
        // Reintenta 2 veces en caso de error transitorio (503, timeout de red)
        retry({ count: 2, delay: 1_000 }),
        catchError(err => throwError(() => this.parsearError(err)))
      );
  }

  /** POST tipado */
  post<TBody, TResponse>(
    path: string,
    body: TBody
  ): Observable<ApiResponse<TResponse>> {
    return this.http
      .post<ApiResponse<TResponse>>(`${this.baseUrl}${path}`, body)
      .pipe(
        catchError(err => throwError(() => this.parsearError(err)))
      );
  }

  /** PUT tipado */
  put<TBody, TResponse>(
    path: string,
    body: TBody
  ): Observable<ApiResponse<TResponse>> {
    return this.http
      .put<ApiResponse<TResponse>>(`${this.baseUrl}${path}`, body)
      .pipe(
        catchError(err => throwError(() => this.parsearError(err)))
      );
  }

  /**
   * Parsea el error de HttpClient a un ApiError tipado.
   * Extrae el mensaje del cuerpo del error si el servidor lo envia en formato estandar.
   */
  private parsearError(err: unknown): ApiError {
    if (
      err != null &&
      typeof err === 'object' &&
      'status' in err &&
      'error' in err
    ) {
      const httpError = err as { status: number; error: unknown };
      const body = httpError.error;

      if (
        body != null &&
        typeof body === 'object' &&
        'error' in body
      ) {
        const apiError = body as ApiErrorResponse;
        return {
          code: apiError.error.code,
          message: apiError.error.message,
          httpStatus: httpError.status,
          details: apiError.error.details
        };
      }

      return {
        code: 'HTTP_ERROR',
        message: `Error HTTP ${httpError.status}`,
        httpStatus: httpError.status
      };
    }

    return {
      code: 'UNKNOWN_ERROR',
      message: 'Error desconocido al comunicarse con el servidor',
      httpStatus: 0
    };
  }
}

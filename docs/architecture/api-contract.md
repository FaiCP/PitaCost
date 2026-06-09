# PitaSmart -- Contratos de API REST

## Convenciones Generales

| Aspecto | Convencion |
|---------|-----------|
| Base URL | `https://api.pitasmart.ec/v1` |
| Formato | JSON (application/json) |
| Autenticacion | Bearer JWT en header `Authorization` |
| Paginacion | Query params `page` (1-based) y `pageSize` (default 20, max 100) |
| Fechas | ISO 8601 con timezone: `2026-03-24T10:30:00-05:00` (Ecuador = UTC-5) |
| IDs | GUID (UUID v4) generados en cliente para soporte offline |
| Moneda | USD (moneda oficial de Ecuador), 2 decimales |
| Idioma de errores | Espanol |
| Versionado | URL path: `/v1/` |
| Correlation ID | Header `X-Correlation-Id` (GUID) propagado desde cliente |

### Estructura de Respuesta Estandar

**Exito:**
```json
{
  "success": true,
  "data": { },
  "timestamp": "2026-03-24T10:30:00-05:00"
}
```

**Error:**
```json
{
  "success": false,
  "error": {
    "code": "PERIODO_CARENCIA_ACTIVO",
    "message": "No se puede registrar cosecha. Periodo de carencia activo hasta 2026-04-15.",
    "details": [
      {
        "field": "loteId",
        "message": "El lote tiene una aplicacion con periodo de carencia vigente."
      }
    ]
  },
  "timestamp": "2026-03-24T10:30:00-05:00"
}
```

### Codigos HTTP Utilizados

| Codigo | Uso |
|--------|-----|
| 200 | Consulta exitosa |
| 201 | Recurso creado |
| 400 | Validacion fallida (input invalido) |
| 401 | No autenticado |
| 403 | No autorizado (rol insuficiente) |
| 404 | Recurso no encontrado |
| 409 | Conflicto (RowVersion mismatch en sync) |
| 422 | Regla de negocio violada (ej: periodo de carencia activo) |
| 429 | Rate limit excedido |
| 500 | Error interno del servidor |

---

## 1. POST /api/aplicaciones -- Registrar Aplicacion de Quimico

Registra la aplicacion de un insumo quimico a un lote. Esta operacion puede originarse offline y llegar via sync.

### Request

```http
POST /v1/api/aplicaciones
Authorization: Bearer {jwt_token}
Content-Type: application/json
X-Correlation-Id: 550e8400-e29b-41d4-a716-446655440000
```

```json
{
  "id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "loteId": "b2c3d4e5-f6a7-8901-bcde-f12345678901",
  "insumoId": "c3d4e5f6-a7b8-9012-cdef-123456789012",
  "fechaAplicacion": "2026-03-20T08:30:00-05:00",
  "dosis": {
    "cantidad": 2.5,
    "unidad": "L_HA"
  },
  "areaAplicadaHa": 3.2,
  "metodoAplicacion": "FUMIGACION",
  "operadorNombre": "Juan Perez",
  "coordenadasGps": {
    "latitud": -1.831239,
    "longitud": -79.534820
  },
  "observaciones": "Aplicacion preventiva por presencia de sigatoka.",
  "costoTotal": 45.00,
  "creadoOffline": true,
  "clientTimestamp": "2026-03-20T08:35:00-05:00"
}
```

### Validaciones

| Campo | Tipo | Requerido | Reglas |
|-------|------|-----------|--------|
| `id` | UUID | Si | Generado en cliente. Unico. Si ya existe, se trata como idempotente (se ignora si payload identico, 409 si diferente). |
| `loteId` | UUID | Si | Debe existir en la tabla Lotes y pertenecer al usuario autenticado. |
| `insumoId` | UUID | Si | Debe existir en catalogo de Insumos. |
| `fechaAplicacion` | datetime | Si | No puede ser futura (max +1 hora por tolerancia GPS). No anterior a 30 dias. |
| `dosis.cantidad` | decimal(10,4) | Si | Mayor a 0. Menor o igual a dosis maxima permitida del insumo. |
| `dosis.unidad` | enum | Si | Valores: `L_HA`, `KG_HA`, `ML_HA`, `G_HA`, `CC_HA` |
| `areaAplicadaHa` | decimal(10,4) | Si | Mayor a 0. Menor o igual al area total del lote. |
| `metodoAplicacion` | enum | Si | Valores: `FUMIGACION`, `DRENCH`, `INYECCION`, `GRANULAR`, `OTRO` |
| `operadorNombre` | string(200) | Si | Min 2 caracteres. |
| `coordenadasGps` | object | No | Si presente, latitud [-5, 2] y longitud [-92, -75] (rango Ecuador continental + Galapagos). |
| `observaciones` | string(1000) | No | Texto libre. |
| `costoTotal` | decimal(12,2) | No | Mayor o igual a 0. Default 0. |
| `creadoOffline` | boolean | Si | Indica si fue creado sin conexion. |
| `clientTimestamp` | datetime | Si | Momento en que el cliente creo el registro. Usado para resolucion de conflictos. |

### Response 201 Created

```json
{
  "success": true,
  "data": {
    "id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
    "loteId": "b2c3d4e5-f6a7-8901-bcde-f12345678901",
    "loteNombre": "Lote Norte - Finca El Paraiso",
    "insumoId": "c3d4e5f6-a7b8-9012-cdef-123456789012",
    "insumoNombre": "Mancozeb 80% WP",
    "fechaAplicacion": "2026-03-20T08:30:00-05:00",
    "dosis": {
      "cantidad": 2.5,
      "unidad": "L_HA"
    },
    "areaAplicadaHa": 3.2,
    "metodoAplicacion": "FUMIGACION",
    "operadorNombre": "Juan Perez",
    "costoTotal": 45.00,
    "periodoCarencia": {
      "diasCarencia": 14,
      "fechaFinCarencia": "2026-04-03T08:30:00-05:00",
      "cosechaBloqueada": true
    },
    "rowVersion": "AAAAAAAAB9E=",
    "createdAt": "2026-03-24T10:30:00-05:00"
  },
  "timestamp": "2026-03-24T10:30:00-05:00"
}
```

### Reglas de Negocio

1. Al registrar una aplicacion, el sistema calcula automaticamente `fechaFinCarencia = fechaAplicacion + diasCarencia` del insumo.
2. Si hay una cosecha programada para el lote dentro del periodo de carencia, se genera el evento `CosechaBloqueadaEvent`.
3. Se registra automaticamente un costo asociado al lote si `costoTotal > 0`.

---

## 2. GET /api/lotes/{id}/rentabilidad -- Dashboard de Rentabilidad

Retorna el calculo de rentabilidad completo para un lote en un periodo determinado.

### Request

```http
GET /v1/api/lotes/b2c3d4e5-f6a7-8901-bcde-f12345678901/rentabilidad?desde=2026-01-01&hasta=2026-03-24
Authorization: Bearer {jwt_token}
X-Correlation-Id: 660e8400-e29b-41d4-a716-446655440001
```

### Query Parameters

| Parametro | Tipo | Requerido | Default | Reglas |
|-----------|------|-----------|---------|--------|
| `desde` | date (YYYY-MM-DD) | No | Inicio del ciclo de cultivo activo | No anterior a 2 anos. |
| `hasta` | date (YYYY-MM-DD) | No | Hoy | No puede ser anterior a `desde`. |

### Response 200 OK

```json
{
  "success": true,
  "data": {
    "loteId": "b2c3d4e5-f6a7-8901-bcde-f12345678901",
    "loteNombre": "Lote Norte - Finca El Paraiso",
    "cultivoActual": "Banano",
    "areaHa": 5.0,
    "periodo": {
      "desde": "2026-01-01",
      "hasta": "2026-03-24"
    },
    "ingresos": {
      "totalVentas": 12500.00,
      "precioPromedioKg": 0.25,
      "totalKgCosechados": 50000.0,
      "detalleVentas": [
        {
          "fecha": "2026-02-15",
          "comprador": "Exportadora ABC",
          "kgVendidos": 25000.0,
          "precioKg": 0.26,
          "totalVenta": 6500.00
        },
        {
          "fecha": "2026-03-10",
          "comprador": "Mercado Local",
          "kgVendidos": 25000.0,
          "precioKg": 0.24,
          "totalVenta": 6000.00
        }
      ]
    },
    "costos": {
      "totalCostos": 7800.00,
      "costoPorHa": 1560.00,
      "costoPorKg": 0.156,
      "desglose": [
        {
          "categoria": "INSUMOS_QUIMICOS",
          "total": 3200.00,
          "porcentaje": 41.03,
          "items": [
            {
              "descripcion": "Mancozeb 80% WP - 5 aplicaciones",
              "total": 1200.00
            },
            {
              "descripcion": "Fertilizante NPK 15-15-15",
              "total": 2000.00
            }
          ]
        },
        {
          "categoria": "MANO_DE_OBRA",
          "total": 2800.00,
          "porcentaje": 35.90,
          "items": []
        },
        {
          "categoria": "TRANSPORTE",
          "total": 800.00,
          "porcentaje": 10.26,
          "items": []
        },
        {
          "categoria": "OTROS",
          "total": 1000.00,
          "porcentaje": 12.82,
          "items": []
        }
      ]
    },
    "rentabilidad": {
      "utilidadBruta": 4700.00,
      "margenBruto": 37.60,
      "utilidadPorHa": 940.00,
      "utilidadPorKg": 0.094,
      "roi": 60.26
    },
    "alertas": [
      {
        "tipo": "PERIODO_CARENCIA",
        "mensaje": "Cosecha bloqueada hasta 2026-04-03 por aplicacion de Mancozeb.",
        "severidad": "CRITICA"
      }
    ]
  },
  "timestamp": "2026-03-24T10:30:00-05:00"
}
```

### Categorias de Costo Permitidas

| Codigo | Descripcion |
|--------|------------|
| `INSUMOS_QUIMICOS` | Agroquimicos, fertilizantes, semillas |
| `MANO_DE_OBRA` | Jornales, contratos de labor |
| `TRANSPORTE` | Flete, combustible |
| `RIEGO` | Agua, mantenimiento de sistema de riego |
| `MAQUINARIA` | Alquiler o depreciacion de maquinaria |
| `OTROS` | Otros costos no clasificados |

---

## 3. GET /api/insumos/{id}/periodo-carencia -- Consulta Periodo de Carencia

Consulta la informacion de periodo de carencia de un insumo especifico, incluyendo el estado actual respecto a aplicaciones activas.

### Request

```http
GET /v1/api/insumos/c3d4e5f6-a7b8-9012-cdef-123456789012/periodo-carencia?loteId=b2c3d4e5-f6a7-8901-bcde-f12345678901
Authorization: Bearer {jwt_token}
X-Correlation-Id: 770e8400-e29b-41d4-a716-446655440002
```

### Query Parameters

| Parametro | Tipo | Requerido | Reglas |
|-----------|------|-----------|--------|
| `loteId` | UUID | No | Si se proporciona, incluye estado de carencia activa para ese lote. |

### Response 200 OK

```json
{
  "success": true,
  "data": {
    "insumoId": "c3d4e5f6-a7b8-9012-cdef-123456789012",
    "nombreComercial": "Mancozeb 80% WP",
    "ingredienteActivo": "Mancozeb",
    "fabricante": "Dow AgroSciences",
    "registroAgrocalidad": "PF-2024-0012345",
    "concentracion": {
      "valor": 80.0,
      "unidad": "PORCENTAJE"
    },
    "tipoProducto": "FUNGICIDA",
    "categoriaToxico": "III",
    "periodoCarencia": {
      "dias": 14,
      "cultivos": [
        {
          "cultivo": "Banano",
          "diasEspecificos": 14
        },
        {
          "cultivo": "Cacao",
          "diasEspecificos": 21
        }
      ]
    },
    "dosisRecomendada": {
      "minima": 1.5,
      "maxima": 3.0,
      "unidad": "L_HA"
    },
    "estadoCarenciaLote": {
      "loteId": "b2c3d4e5-f6a7-8901-bcde-f12345678901",
      "loteNombre": "Lote Norte - Finca El Paraiso",
      "carenciaActiva": true,
      "ultimaAplicacion": "2026-03-20T08:30:00-05:00",
      "fechaFinCarencia": "2026-04-03T08:30:00-05:00",
      "diasRestantes": 10,
      "cosechaBloqueada": true
    }
  },
  "timestamp": "2026-03-24T10:30:00-05:00"
}
```

### Response 200 OK (sin loteId)

Si no se envia `loteId`, el campo `estadoCarenciaLote` es `null`.

---

## 4. POST /api/sync/push -- Sincronizacion desde Dispositivo Offline

Recibe un lote de operaciones pendientes del dispositivo y las procesa en orden. Es el endpoint central del mecanismo offline-first.

### Request

```http
POST /v1/api/sync/push
Authorization: Bearer {jwt_token}
Content-Type: application/json
X-Correlation-Id: 880e8400-e29b-41d4-a716-446655440003
X-Device-Id: device-abc-123
X-Client-Version: 1.2.0
```

```json
{
  "deviceId": "device-abc-123",
  "lastSyncTimestamp": "2026-03-20T06:00:00-05:00",
  "operaciones": [
    {
      "operacionId": "op-001-uuid",
      "tipo": "CREAR_APLICACION",
      "entidadId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
      "entidadTipo": "AplicacionQuimico",
      "payload": {
        "loteId": "b2c3d4e5-f6a7-8901-bcde-f12345678901",
        "insumoId": "c3d4e5f6-a7b8-9012-cdef-123456789012",
        "fechaAplicacion": "2026-03-20T08:30:00-05:00",
        "dosis": {
          "cantidad": 2.5,
          "unidad": "L_HA"
        },
        "areaAplicadaHa": 3.2,
        "metodoAplicacion": "FUMIGACION",
        "operadorNombre": "Juan Perez",
        "costoTotal": 45.00
      },
      "clientTimestamp": "2026-03-20T08:35:00-05:00",
      "rowVersionAnterior": null,
      "intentoNumero": 1
    },
    {
      "operacionId": "op-002-uuid",
      "tipo": "ACTUALIZAR_COSTO",
      "entidadId": "d4e5f6a7-b8c9-0123-def0-456789012345",
      "entidadTipo": "CostoLote",
      "payload": {
        "monto": 120.00,
        "categoria": "MANO_DE_OBRA",
        "descripcion": "Jornales semana 12"
      },
      "clientTimestamp": "2026-03-21T14:00:00-05:00",
      "rowVersionAnterior": "AAAAAAAAB8A=",
      "intentoNumero": 1
    }
  ]
}
```

### Tipos de Operacion

| Tipo | Descripcion | Entidad |
|------|------------|---------|
| `CREAR_APLICACION` | Nueva aplicacion de quimico | AplicacionQuimico |
| `CREAR_COSECHA` | Nuevo registro de cosecha | Cosecha |
| `CREAR_COSTO` | Nuevo costo al lote | CostoLote |
| `ACTUALIZAR_APLICACION` | Modificar aplicacion existente | AplicacionQuimico |
| `ACTUALIZAR_COSTO` | Modificar costo existente | CostoLote |
| `ELIMINAR_COSTO` | Eliminar costo (soft delete) | CostoLote |
| `CREAR_LOTE` | Nuevo lote | Lote |
| `ACTUALIZAR_LOTE` | Modificar lote existente | Lote |

### Validaciones del Request

| Campo | Tipo | Requerido | Reglas |
|-------|------|-----------|--------|
| `deviceId` | string(100) | Si | Identificador unico del dispositivo. |
| `lastSyncTimestamp` | datetime | Si | Ultima sincronizacion exitosa del dispositivo. |
| `operaciones` | array | Si | Minimo 1, maximo 100 operaciones por request. |
| `operaciones[].operacionId` | UUID | Si | Idempotency key. Si ya fue procesada, se omite. |
| `operaciones[].tipo` | enum | Si | Uno de los tipos listados arriba. |
| `operaciones[].entidadId` | UUID | Si | ID de la entidad afectada (generado en cliente para CREARs). |
| `operaciones[].entidadTipo` | string | Si | Nombre de la entidad de dominio. |
| `operaciones[].payload` | object | Si | Datos especificos segun el tipo de operacion. |
| `operaciones[].clientTimestamp` | datetime | Si | Momento de creacion en el cliente. |
| `operaciones[].rowVersionAnterior` | string | Condicional | Requerido para ACTUALIZAR y ELIMINAR. Base64 del RowVersion conocido. |
| `operaciones[].intentoNumero` | int | Si | Numero de intento de envio (1-based). Util para diagnostico. |

### Response 200 OK

Las operaciones se procesan en orden. Cada una tiene su resultado individual.

```json
{
  "success": true,
  "data": {
    "deviceId": "device-abc-123",
    "serverTimestamp": "2026-03-24T10:30:00-05:00",
    "resultados": [
      {
        "operacionId": "op-001-uuid",
        "estado": "APLICADA",
        "entidadId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
        "rowVersionNuevo": "AAAAAAAAB9E=",
        "error": null
      },
      {
        "operacionId": "op-002-uuid",
        "estado": "CONFLICTO",
        "entidadId": "d4e5f6a7-b8c9-0123-def0-456789012345",
        "rowVersionNuevo": null,
        "error": {
          "code": "ROWVERSION_MISMATCH",
          "message": "La entidad fue modificada por otro dispositivo. RowVersion esperado: AAAAAAAAB8A=, actual: AAAAAAAAB9A=.",
          "serverData": {
            "monto": 150.00,
            "categoria": "MANO_DE_OBRA",
            "descripcion": "Jornales semana 12 - actualizado por web",
            "rowVersion": "AAAAAAAAB9A=",
            "updatedAt": "2026-03-22T09:00:00-05:00"
          }
        }
      }
    ],
    "datosActualizados": {
      "insumosModificados": 0,
      "preciosMercadoActualizados": true,
      "ultimoPrecio": {
        "cultivo": "Banano",
        "precioKg": 0.27,
        "fecha": "2026-03-24"
      }
    }
  },
  "timestamp": "2026-03-24T10:30:00-05:00"
}
```

### Estados de Resultado por Operacion

| Estado | Descripcion |
|--------|------------|
| `APLICADA` | Operacion procesada exitosamente. |
| `DUPLICADA` | `operacionId` ya fue procesada anteriormente (idempotencia). Se ignora. |
| `CONFLICTO` | RowVersion mismatch. Incluye datos actuales del servidor en `serverData`. |
| `RECHAZADA` | Validacion de negocio fallida (ej: insumo no existe, lote no pertenece al usuario). |
| `ERROR` | Error inesperado procesando la operacion. |

---

## 5. POST /api/auth/challenge -- Inicio de Autenticacion Passkey

Genera un challenge WebAuthn para iniciar el flujo de autenticacion con Passkey.

### Request

```http
POST /v1/api/auth/challenge
Content-Type: application/json
```

```json
{
  "email": "juan.perez@correo.com",
  "tipo": "AUTENTICACION"
}
```

### Validaciones

| Campo | Tipo | Requerido | Reglas |
|-------|------|-----------|--------|
| `email` | string(254) | Si | Email valido. Debe existir si `tipo` es `AUTENTICACION`. |
| `tipo` | enum | Si | `REGISTRO` (primera vez) o `AUTENTICACION` (login). |

### Response 200 OK

```json
{
  "success": true,
  "data": {
    "challengeId": "ch-550e8400-e29b-41d4-a716-446655440099",
    "publicKeyCredentialRequestOptions": {
      "challenge": "dGVzdC1jaGFsbGVuZ2UtYmFzZTY0",
      "timeout": 60000,
      "rpId": "pitasmart.ec",
      "allowCredentials": [
        {
          "id": "Y3JlZGVudGlhbC1pZC0x",
          "type": "public-key",
          "transports": ["internal", "hybrid"]
        }
      ],
      "userVerification": "preferred"
    },
    "expiresAt": "2026-03-24T10:31:00-05:00"
  },
  "timestamp": "2026-03-24T10:30:00-05:00"
}
```

### Response para tipo REGISTRO

Cuando `tipo` es `REGISTRO`, retorna `publicKeyCredentialCreationOptions` en lugar de `publicKeyCredentialRequestOptions`:

```json
{
  "success": true,
  "data": {
    "challengeId": "ch-660e8400-e29b-41d4-a716-446655440100",
    "publicKeyCredentialCreationOptions": {
      "challenge": "cmVnaXN0cm8tY2hhbGxlbmdlLWJhc2U2NA==",
      "rp": {
        "name": "PitaSmart",
        "id": "pitasmart.ec"
      },
      "user": {
        "id": "dXNlci1pZC1iYXNlNjQ=",
        "name": "juan.perez@correo.com",
        "displayName": "Juan Perez"
      },
      "pubKeyCredParams": [
        { "alg": -7, "type": "public-key" },
        { "alg": -257, "type": "public-key" }
      ],
      "timeout": 60000,
      "authenticatorSelection": {
        "authenticatorAttachment": "platform",
        "residentKey": "required",
        "userVerification": "required"
      },
      "attestation": "none"
    },
    "expiresAt": "2026-03-24T10:31:00-05:00"
  },
  "timestamp": "2026-03-24T10:30:00-05:00"
}
```

---

## 6. POST /api/auth/verify -- Verificacion de Passkey

Verifica la respuesta del autenticador WebAuthn y emite un JWT si es valida.

### Request

```http
POST /v1/api/auth/verify
Content-Type: application/json
```

```json
{
  "challengeId": "ch-550e8400-e29b-41d4-a716-446655440099",
  "tipo": "AUTENTICACION",
  "credential": {
    "id": "Y3JlZGVudGlhbC1pZC0x",
    "rawId": "Y3JlZGVudGlhbC1pZC0x",
    "type": "public-key",
    "response": {
      "authenticatorData": "SZYN5YgOjGh0NBcPZHZgW4_krrmihjLHmVzzuoMdl2MFAAAAAQ==",
      "clientDataJSON": "eyJ0eXBlIjoid2ViYXV0aG4uZ2V0IiwiY2hhbGxlbmdlIjoiZEdWemRDMWphR0ZzYkdWdVoyVXRZbUZ6WlRZMCIsIm9yaWdpbiI6Imh0dHBzOi8vcGl0YXNtYXJ0LmVjIn0=",
      "signature": "MEQCIBx1D...(base64)..."
    }
  },
  "deviceInfo": {
    "deviceId": "device-abc-123",
    "platform": "Android",
    "appVersion": "1.2.0"
  }
}
```

### Validaciones

| Campo | Tipo | Requerido | Reglas |
|-------|------|-----------|--------|
| `challengeId` | string | Si | Debe coincidir con un challenge no expirado. |
| `tipo` | enum | Si | Debe coincidir con el tipo del challenge original. |
| `credential` | object | Si | Objeto `PublicKeyCredential` serializado del navegador. |
| `credential.id` | string | Si | ID de la credencial (base64url). |
| `credential.response` | object | Si | Contiene `authenticatorData`, `clientDataJSON`, `signature`. |
| `deviceInfo.deviceId` | string(100) | Si | Identificador del dispositivo para registro de sesiones. |
| `deviceInfo.platform` | string(50) | No | Sistema operativo. |
| `deviceInfo.appVersion` | string(20) | No | Version de la app. |

### Response 200 OK (Autenticacion exitosa)

```json
{
  "success": true,
  "data": {
    "accessToken": "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9...",
    "refreshToken": "rt-a1b2c3d4-e5f6-7890-abcd-ef1234567890",
    "expiresIn": 3600,
    "tokenType": "Bearer",
    "usuario": {
      "id": "u-123456",
      "email": "juan.perez@correo.com",
      "nombreCompleto": "Juan Perez",
      "rol": "AGRICULTOR",
      "fincas": [
        {
          "id": "f-001",
          "nombre": "Finca El Paraiso",
          "lotes": 4
        }
      ]
    }
  },
  "timestamp": "2026-03-24T10:30:05-05:00"
}
```

### Response 200 OK (Registro exitoso)

Cuando `tipo` es `REGISTRO`, adicionalmente crea la credencial Passkey asociada al usuario:

```json
{
  "success": true,
  "data": {
    "accessToken": "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9...",
    "refreshToken": "rt-b2c3d4e5-f6a7-8901-bcde-f12345678901",
    "expiresIn": 3600,
    "tokenType": "Bearer",
    "usuario": {
      "id": "u-789012",
      "email": "juan.perez@correo.com",
      "nombreCompleto": "Juan Perez",
      "rol": "AGRICULTOR",
      "fincas": []
    },
    "passkeyRegistrada": true,
    "credentialId": "Y3JlZGVudGlhbC1pZC0x"
  },
  "timestamp": "2026-03-24T10:30:05-05:00"
}
```

### JWT Claims

El `accessToken` contiene los siguientes claims:

| Claim | Tipo | Descripcion |
|-------|------|------------|
| `sub` | string | ID del usuario |
| `email` | string | Email del usuario |
| `name` | string | Nombre completo |
| `role` | string | Rol: `AGRICULTOR`, `ADMINISTRADOR`, `AUDITOR` |
| `finca_ids` | string[] | IDs de fincas a las que tiene acceso |
| `iat` | number | Issued at (Unix timestamp) |
| `exp` | number | Expiration (Unix timestamp, +1 hora) |
| `iss` | string | `https://api.pitasmart.ec` |
| `aud` | string | `pitasmart-app` |

### Errores Especificos de Auth

| Codigo HTTP | Error Code | Descripcion |
|-------------|-----------|------------|
| 400 | `CHALLENGE_EXPIRED` | El challenge expiro (mas de 60 segundos). |
| 400 | `CHALLENGE_NOT_FOUND` | El challengeId no existe. |
| 401 | `VERIFICATION_FAILED` | La firma del autenticador es invalida. |
| 401 | `CREDENTIAL_NOT_FOUND` | La credencial no esta registrada. |
| 429 | `TOO_MANY_ATTEMPTS` | Mas de 5 intentos fallidos en 15 minutos. Rate limit por IP + email. |

---

## Endpoints Adicionales (Referencia)

Los siguientes endpoints completan la API pero no se detallan en este documento MVP:

| Metodo | Endpoint | Descripcion |
|--------|----------|------------|
| GET | `/v1/api/lotes` | Listar lotes del usuario |
| POST | `/v1/api/lotes` | Crear nuevo lote |
| GET | `/v1/api/lotes/{id}` | Detalle de lote |
| GET | `/v1/api/aplicaciones?loteId={id}` | Listar aplicaciones de un lote |
| GET | `/v1/api/cosechas?loteId={id}` | Listar cosechas de un lote |
| POST | `/v1/api/cosechas` | Registrar cosecha (valida periodo de carencia) |
| POST | `/v1/api/costos` | Registrar costo a un lote |
| GET | `/v1/api/insumos?buscar={texto}` | Buscar insumos en catalogo |
| GET | `/v1/api/sync/pull?desde={timestamp}` | Pull de cambios del servidor al dispositivo |
| POST | `/v1/api/auth/refresh` | Renovar access token |
| POST | `/v1/api/reportes/trazabilidad` | Generar PDF de trazabilidad Agrocalidad |
| GET | `/v1/api/precios-mercado/{cultivo}` | Ultimo precio de mercado |

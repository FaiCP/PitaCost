# PitaSmart -- Flujo de Sincronizacion Offline-First

## Principio Fundamental

El dispositivo del agricultor es la fuente de verdad mientras esta offline. Toda operacion de escritura se ejecuta primero contra la base de datos local (RxDB/IndexedDB), se presenta inmediatamente al usuario, y se encola para sincronizacion posterior. El servidor es la fuente de verdad autoritativa una vez que la sincronizacion ocurre.

---

## Flujo Completo: Registro Offline a Sincronizacion

```
+-------------------------------------------------------------------+
|                    DISPOSITIVO (OFFLINE)                           |
+-------------------------------------------------------------------+
|                                                                   |
|  1. Agricultor registra aplicacion de quimico                     |
|     |                                                             |
|     v                                                             |
|  2. Angular genera UUID para la entidad                           |
|     |                                                             |
|     v                                                             |
|  3. Validacion LOCAL (dosis, periodo carencia, datos requeridos)  |
|     |                                                             |
|     +--- FALLA --> Muestra error al usuario, no encola            |
|     |                                                             |
|     v (PASA)                                                      |
|  4. Persiste en RxDB (IndexedDB)                                  |
|     |                                                             |
|     v                                                             |
|  5. Crea OperacionPendiente en cola offline                       |
|     {                                                             |
|       operacionId: UUID,                                          |
|       tipo: "CREAR_APLICACION",                                   |
|       entidadId: UUID,                                            |
|       payload: { ... },                                           |
|       clientTimestamp: now(),                                      |
|       estado: "PENDIENTE",                                        |
|       intentos: 0                                                 |
|     }                                                             |
|     |                                                             |
|     v                                                             |
|  6. UI muestra dato con badge "Pendiente de sync"                 |
|     |                                                             |
|     v                                                             |
|  7. SyncEngine detecta conectividad (Navigator.onLine + ping)     |
|     |                                                             |
|     +--- SIN CONEXION --> Queda en cola, reintenta periodicamente |
|     |                                                             |
|     v (CONEXION DETECTADA)                                        |
+-------------------------------------------------------------------+
         |
         | HTTPS POST /api/sync/push
         v
+-------------------------------------------------------------------+
|                       SERVIDOR (.NET)                              |
+-------------------------------------------------------------------+
|                                                                   |
|  8. Recibe batch de operaciones pendientes                        |
|     |                                                             |
|     v                                                             |
|  9. Por cada operacion (en orden de clientTimestamp):              |
|     |                                                             |
|     +-- a. Verifica idempotencia (operacionId ya procesada?)      |
|     |      SI --> Retorna DUPLICADA, salta a siguiente            |
|     |                                                             |
|     +-- b. Verifica RowVersion (para updates)                     |
|     |      MISMATCH --> Retorna CONFLICTO + datos actuales        |
|     |                                                             |
|     +-- c. Ejecuta validacion de dominio                          |
|     |      FALLA --> Retorna RECHAZADA + razon                    |
|     |                                                             |
|     +-- d. Persiste en SQL Server                                 |
|     |      ERROR --> Retorna ERROR, no afecta otras operaciones   |
|     |                                                             |
|     +-- e. Dispara eventos de dominio                             |
|     |      (ej: AplicacionRegistradaEvent --> recalcula carencia) |
|     |                                                             |
|     +-- f. Retorna APLICADA + nuevo RowVersion                    |
|     |                                                             |
|     v                                                             |
| 10. Retorna respuesta con resultado individual por operacion      |
|     Incluye datos actualizados (precios mercado, etc.)            |
|                                                                   |
+-------------------------------------------------------------------+
         |
         | Response JSON
         v
+-------------------------------------------------------------------+
|                    DISPOSITIVO (ONLINE)                            |
+-------------------------------------------------------------------+
|                                                                   |
| 11. SyncEngine procesa resultados:                                |
|     |                                                             |
|     +-- APLICADA:                                                 |
|     |   - Actualiza RowVersion en RxDB                            |
|     |   - Elimina operacion de cola                               |
|     |   - Actualiza badge a "Sincronizado"                        |
|     |                                                             |
|     +-- DUPLICADA:                                                |
|     |   - Elimina operacion de cola (ya fue procesada antes)      |
|     |                                                             |
|     +-- CONFLICTO:                                                |
|     |   - Ejecuta estrategia de resolucion (ver seccion abajo)   |
|     |   - Marca operacion como CONFLICTO en cola                  |
|     |                                                             |
|     +-- RECHAZADA:                                                |
|     |   - Marca operacion como RECHAZADA                          |
|     |   - Notifica al usuario con razon                           |
|     |   - El dato LOCAL se marca como invalido                    |
|     |                                                             |
|     +-- ERROR:                                                    |
|         - Incrementa intentos                                     |
|         - Si intentos < MAX_RETRIES (5): reencola con backoff     |
|         - Si intentos >= MAX_RETRIES: marca como FALLIDA          |
|                                                                   |
| 12. Ejecuta PULL de cambios del servidor (GET /api/sync/pull)     |
|     - Trae datos modificados desde lastSyncTimestamp              |
|     - Actualiza RxDB local con datos frescos                      |
|     - Actualiza lastSyncTimestamp                                 |
|                                                                   |
+-------------------------------------------------------------------+
```

---

## Estados de una Operacion en la Cola Offline

```
                    +-------------+
                    |  PENDIENTE  |  Estado inicial al crear la operacion
                    +------+------+
                           |
                    (sync push)
                           |
              +------------+------------+
              |            |            |
              v            v            v
        +---------+  +-----------+  +-----------+
        | APLICADA|  | CONFLICTO |  | RECHAZADA |
        +---------+  +-----+-----+  +-----------+
              |            |               |
              v            v               v
        (se elimina  (resolucion)   (notifica usuario,
         de la cola)       |         se elimina o
                           v         se corrige)
                    +------+------+
                    |  RESUELTA   |  Conflicto resuelto por usuario o auto
                    +------+------+
                           |
                    (re-sync push)
                           |
                           v
                    +------+------+
                    |  APLICADA   |
                    +-------------+


        +-----------+
        |  ENVIANDO |  Transicion efimera durante HTTP request
        +-----------+

        +-----------+
        |  FALLIDA  |  Despues de MAX_RETRIES (5) intentos fallidos
        +-----------+
              |
              v
        (notifica usuario,
         permite reintento manual)
```

### Tabla de Estados

| Estado | Descripcion | Transicion a | Accion del usuario |
|--------|-------------|-------------|-------------------|
| `PENDIENTE` | Creada, esperando sincronizacion | `ENVIANDO` | Ninguna (automatico) |
| `ENVIANDO` | En proceso de envio al servidor | `APLICADA`, `CONFLICTO`, `RECHAZADA`, `PENDIENTE` (retry) | Ninguna |
| `APLICADA` | Servidor la proceso exitosamente | (eliminada de cola) | Ninguna |
| `DUPLICADA` | Ya habia sido procesada antes | (eliminada de cola) | Ninguna |
| `CONFLICTO` | RowVersion mismatch detectado | `RESUELTA` | Revisar y elegir version (o auto-resuelto) |
| `RECHAZADA` | Validacion de negocio fallida en servidor | (eliminada, dato local revertido) | Corregir datos y reintentar |
| `RESUELTA` | Conflicto resuelto, lista para re-sync | `PENDIENTE` | Ninguna (automatico) |
| `FALLIDA` | Excedio intentos maximos | `PENDIENTE` (reintento manual) | Reintento manual o eliminar |

---

## Estrategia de Resolucion de Conflictos

### Mecanismo: Last-Write-Wins con RowVersion + Intervencion del Usuario

PitaSmart usa una estrategia de conflictos en dos niveles:

#### Nivel 1: Resolucion Automatica (Last-Write-Wins)

Para entidades donde la perdida de una version intermedia no es critica:

| Entidad | Estrategia | Razon |
|---------|-----------|-------|
| `CostoLote` | LWW por `clientTimestamp` | El ultimo monto ingresado es el correcto |
| `Lote` (datos basicos) | LWW por `clientTimestamp` | Nombre/area son datos simples |

**Mecanismo LWW:**
1. El servidor detecta que `rowVersionAnterior` del cliente no coincide con el `RowVersion` actual en SQL Server.
2. Compara `clientTimestamp` de la operacion con `updatedAt` del registro en servidor.
3. Si `clientTimestamp > updatedAt`: el cliente gana (dato mas reciente), se aplica la operacion con el RowVersion actual.
4. Si `clientTimestamp <= updatedAt`: el servidor gana, se retorna CONFLICTO con datos actuales para que el cliente actualice su copia local.

#### Nivel 2: Intervencion del Usuario (Merge Manual)

Para entidades donde la precision es critica y legalmente relevante:

| Entidad | Estrategia | Razon |
|---------|-----------|-------|
| `AplicacionQuimico` | Merge manual | Dato regulado por Agrocalidad; no se puede perder ni sobrescribir |
| `Cosecha` | Merge manual | Afecta bloqueo de periodo de carencia |

**Mecanismo de Merge Manual:**
1. El servidor retorna CONFLICTO con `serverData` (version actual del servidor).
2. El cliente muestra al usuario ambas versiones (local y servidor) lado a lado.
3. El usuario elige cual conservar, o combina campos manualmente.
4. La version elegida se encola como nueva operacion con el RowVersion actual del servidor.

### Diagrama de Decision de Conflictos

```
Operacion llega al servidor
         |
         v
   RowVersion coincide?
    /           \
  SI             NO
   |              |
   v              v
 Aplicar      Es entidad critica?
 normalmente   (Aplicacion, Cosecha)
               /           \
             SI             NO
              |              |
              v              v
         Retornar        clientTimestamp > updatedAt?
         CONFLICTO       /              \
         (merge         SI               NO
          manual)        |                |
                         v                v
                    Aplicar con       Retornar
                    RowVersion        CONFLICTO
                    actual            (servidor gana)
                    (cliente gana)
```

---

## RowVersion: Implementacion Tecnica

### SQL Server

```sql
-- Cada tabla sincronizable incluye:
ALTER TABLE Aplicaciones ADD RowVersion ROWVERSION NOT NULL;
ALTER TABLE Aplicaciones ADD SyncStatus NVARCHAR(20) DEFAULT 'SYNCED';
ALTER TABLE Aplicaciones ADD ClientTimestamp DATETIMEOFFSET NOT NULL;
ALTER TABLE Aplicaciones ADD DeviceId NVARCHAR(100) NULL;
```

`ROWVERSION` en SQL Server es un tipo de dato automatico que se incrementa con cada UPDATE. No necesita gestion manual.

### Cliente (RxDB)

```typescript
// Schema de RxDB para operacion pendiente
const operacionPendienteSchema = {
  title: 'operacion_pendiente',
  version: 0,
  type: 'object',
  primaryKey: 'operacionId',
  properties: {
    operacionId:       { type: 'string', maxLength: 36 },
    tipo:              { type: 'string' },
    entidadId:         { type: 'string', maxLength: 36 },
    entidadTipo:       { type: 'string' },
    payload:           { type: 'object' },
    clientTimestamp:    { type: 'string', format: 'date-time' },
    rowVersionAnterior:{ type: ['string', 'null'] },
    estado:            { type: 'string', enum: ['PENDIENTE','ENVIANDO','CONFLICTO','RECHAZADA','RESUELTA','FALLIDA'] },
    intentos:          { type: 'integer', default: 0 },
    ultimoIntento:     { type: ['string', 'null'], format: 'date-time' },
    errorDetalle:      { type: ['string', 'null'] },
    serverData:        { type: ['object', 'null'] }
  },
  required: ['operacionId', 'tipo', 'entidadId', 'entidadTipo', 'payload', 'clientTimestamp', 'estado']
};
```

---

## Mecanismo de Deteccion de Conectividad

```typescript
@Injectable({ providedIn: 'root' })
export class ConnectivityService {
  // Signal reactivo
  readonly isOnline = signal<boolean>(navigator.onLine);
  readonly connectionQuality = signal<'good' | 'slow' | 'offline'>('offline');

  constructor() {
    // Eventos del navegador
    window.addEventListener('online', () => this.checkRealConnectivity());
    window.addEventListener('offline', () => {
      this.isOnline.set(false);
      this.connectionQuality.set('offline');
    });

    // Ping periodico cada 30 segundos cuando "online"
    // navigator.onLine es poco confiable en Android
    interval(30_000).pipe(
      filter(() => navigator.onLine)
    ).subscribe(() => this.checkRealConnectivity());
  }

  private async checkRealConnectivity(): Promise<void> {
    try {
      const start = Date.now();
      await fetch('/api/health/ping', {
        method: 'HEAD',
        cache: 'no-cache',
        signal: AbortSignal.timeout(5000)
      });
      const latency = Date.now() - start;

      this.isOnline.set(true);
      this.connectionQuality.set(latency < 2000 ? 'good' : 'slow');
    } catch {
      this.isOnline.set(false);
      this.connectionQuality.set('offline');
    }
  }
}
```

**Justificacion**: `navigator.onLine` en Android solo detecta si hay conexion WiFi/datos activa, pero no verifica conectividad real con el servidor. Un agricultor puede tener "barras de senal" pero sin ruta al servidor. El ping real es imprescindible.

---

## Motor de Sincronizacion (SyncEngine)

### Ciclo de Sync

```
SyncEngine se activa cuando:
  1. ConnectivityService detecta reconexion (offline -> online)
  2. Timer periodico (cada 2 minutos si online)
  3. El usuario presiona "Sincronizar ahora"
  4. Se acumula un batch de 10+ operaciones pendientes

Ciclo:
  1. PUSH: Enviar operaciones pendientes al servidor
     - Obtener operaciones con estado PENDIENTE, ordenadas por clientTimestamp
     - Agrupar en batches de max 100
     - POST /api/sync/push
     - Procesar resultados (ver flujo arriba)

  2. PULL: Descargar cambios del servidor
     - GET /api/sync/pull?desde={lastSyncTimestamp}
     - Actualizar RxDB con datos recibidos
     - Actualizar lastSyncTimestamp

  3. NOTIFY: Actualizar UI
     - Emitir signal de syncStatus
     - Actualizar badges de estado
     - Si hubo conflictos: mostrar notificacion
```

### Retry con Exponential Backoff

```
Intento 1: inmediato
Intento 2: 5 segundos
Intento 3: 15 segundos
Intento 4: 60 segundos
Intento 5: 300 segundos (5 minutos)
Intento 6+: FALLIDA (requiere intervencion manual)
```

Formula: `delay = min(baseDelay * 3^(intento-2), 300) segundos` para intento >= 2.

---

## Idempotencia

Cada operacion tiene un `operacionId` (UUID) generado en el cliente. El servidor mantiene un registro de operaciones procesadas:

```sql
CREATE TABLE SyncOperacionesLog (
    OperacionId     UNIQUEIDENTIFIER PRIMARY KEY,
    DeviceId        NVARCHAR(100) NOT NULL,
    UsuarioId       UNIQUEIDENTIFIER NOT NULL,
    Tipo            NVARCHAR(50) NOT NULL,
    EntidadId       UNIQUEIDENTIFIER NOT NULL,
    Estado          NVARCHAR(20) NOT NULL,  -- APLICADA, RECHAZADA, etc.
    ProcesadoAt     DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
    INDEX IX_SyncLog_Device (DeviceId, ProcesadoAt)
);
```

Si un `operacionId` ya existe en `SyncOperacionesLog`:
- Se retorna `DUPLICADA` sin reprocesar.
- Este log se purga despues de 90 dias.

---

## Consideraciones de Seguridad en Sync

1. **Autorizacion por entidad**: Cada operacion verifica que el usuario autenticado es dueno del lote/finca referenciado. Un dispositivo comprometido no puede escribir en lotes ajenos.

2. **Tamano maximo de batch**: 100 operaciones por request para prevenir abuse.

3. **Rate limiting en sync**: Maximo 10 sync pushes por minuto por dispositivo.

4. **Validacion completa en servidor**: Aunque el cliente valida localmente, el servidor SIEMPRE re-ejecuta todas las validaciones de dominio. El cliente es "optimista" pero el servidor es "autoritativo".

5. **Encriptacion de cola local**: Los datos en IndexedDB se almacenan en texto plano (limitacion del navegador), pero el dispositivo debe tener bloqueo de pantalla activo. Se evalua IndexedDB encryption via Web Crypto API en version futura.

---

## Escenarios Criticos

### Escenario 1: Aplicacion offline + cosecha offline en periodo de carencia

```
1. Agricultor registra aplicacion de Mancozeb (14 dias carencia) - OFFLINE
2. Inmediatamente intenta registrar cosecha del mismo lote - OFFLINE
3. El cliente BLOQUEA la cosecha localmente:
   - periodo-carencia.service.ts consulta RxDB
   - Encuentra aplicacion con carencia activa
   - Muestra alerta: "Cosecha bloqueada hasta {fecha}"
4. Si de alguna forma la cosecha se registra (bug, manipulacion):
   - La operacion se encola
   - El servidor la RECHAZA con codigo PERIODO_CARENCIA_ACTIVO
   - El dato local se marca como invalido
```

### Escenario 2: Dos dispositivos modifican el mismo costo

```
1. Dispositivo A (offline): Actualiza costo a $100 a las 10:00
2. Dispositivo B (offline): Actualiza costo a $150 a las 10:05
3. Dispositivo A sincroniza primero: costo = $100, RowVersion = V2
4. Dispositivo B sincroniza despues:
   - Envia rowVersionAnterior = V1 (el que conocia)
   - Servidor tiene V2
   - CONFLICTO detectado
   - CostoLote usa LWW: clientTimestamp B (10:05) > updatedAt A (10:00)
   - Dispositivo B gana automaticamente: costo = $150
   - Dispositivo A recibe actualizacion en proximo PULL
```

### Escenario 3: Sync parcial (conexion se pierde a mitad del batch)

```
1. Dispositivo envia batch de 50 operaciones
2. Servidor procesa 30, conexion se corta
3. Dispositivo no recibe respuesta -> marca todas como PENDIENTE (retry)
4. Proximo intento: envia las 50 operaciones nuevamente
5. Las 30 ya procesadas retornan DUPLICADA (idempotencia)
6. Las 20 restantes se procesan normalmente
```

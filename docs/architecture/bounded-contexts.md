# PitaSmart -- Bounded Contexts (DDD)

## Mapa de Contextos

```
+---------------------+          +----------------------+
|                     |  evento  |                      |
|   AGROQUIMICOS      +--------->+   APLICACIONES       |
|   (Catalogo)        |          |   (Trazabilidad)     |
|                     |          |                      |
+---------------------+          +----------+-----------+
                                            |
                                   evento   |  AplicacionRegistradaEvent
                                            |
                                 +----------v-----------+
                                 |                      |
                                 |   COSECHA            |
                                 |   (Produccion)       |
                                 |                      |
                                 +----------+-----------+
                                            |
                                   evento   |  CosechaRegistradaEvent
                                            |
              +---------------------+       |       +----------------------+
              |                     |       |       |                      |
              |   IDENTIDAD         |       +------>+   COSTOS             |
              |   (Auth & Usuarios) |               |   (Rentabilidad)     |
              |                     |               |                      |
              +---------------------+               +----------------------+
                       ^                                       ^
                       |                                       |
                       |         +----------------------+      |
                       |         |                      |      |
                       +---------+   SINCRONIZACION     +------+
                                 |   (Offline Sync)     |
                                 |                      |
                                 +----------------------+
```

### Relaciones entre Contextos

| Upstream | Downstream | Tipo de Relacion | Mecanismo |
|----------|-----------|-----------------|-----------|
| Agroquimicos | Aplicaciones | Supplier/Customer | Aplicaciones consulta catalogo de insumos via interfaz `IInsumoRepository` |
| Aplicaciones | Cosecha | Event-Driven | `AplicacionRegistradaEvent` notifica a Cosecha para recalcular bloqueos |
| Aplicaciones | Costos | Event-Driven | `AplicacionRegistradaEvent` puede crear un `CostoLote` automaticamente |
| Cosecha | Costos | Event-Driven | `CosechaRegistradaEvent` alimenta calculo de ingresos |
| Identidad | Todos | Shared Kernel | Todos los contextos dependen de la identidad del usuario autenticado |
| Sincronizacion | Todos | Anti-Corruption Layer | Sync despacha operaciones a los contextos correctos sin acoplarlos |

---

## 1. Bounded Context: Agroquimicos

### Responsabilidad
Gestiona el catalogo de insumos agroquimicos, sus fichas tecnicas, periodos de carencia por cultivo, y datos regulatorios de Agrocalidad. Este contexto es principalmente de **lectura** -- los datos son mantenidos por administradores o importados de fuentes oficiales.

### Entidades

#### Insumo (Aggregate Root)

| Atributo | Tipo | Descripcion |
|----------|------|------------|
| `Id` | Guid | Identificador unico |
| `NombreComercial` | string(200) | Nombre comercial del producto |
| `IngredienteActivo` | string(200) | Principio activo quimico |
| `Fabricante` | string(200) | Empresa fabricante |
| `RegistroAgrocalidad` | string(50) | Numero de registro oficial ante Agrocalidad |
| `TipoProducto` | enum | FUNGICIDA, HERBICIDA, INSECTICIDA, FERTILIZANTE, NEMATICIDA, OTRO |
| `CategoriaToxico` | enum | I (Extremadamente toxico), II, III, IV (Ligeramente toxico) |
| `Concentracion` | ValueObject | Valor + Unidad (ej: 80% WP) |
| `DosisMinima` | decimal(10,4) | Dosis minima recomendada |
| `DosisMaxima` | decimal(10,4) | Dosis maxima permitida |
| `UnidadDosis` | enum | L_HA, KG_HA, ML_HA, G_HA, CC_HA |
| `Activo` | bool | Si esta disponible en catalogo |
| `RowVersion` | byte[] | Control de concurrencia |

#### PeriodoCarencia

| Atributo | Tipo | Descripcion |
|----------|------|------------|
| `Id` | Guid | Identificador unico |
| `InsumoId` | Guid | FK a Insumo |
| `Cultivo` | string(100) | Tipo de cultivo (Banano, Cacao, etc.) |
| `DiasCarencia` | int | Dias de espera obligatorios antes de cosechar |
| `FuenteRegulacion` | string(200) | Referencia normativa de Agrocalidad |

#### FichaTecnica

| Atributo | Tipo | Descripcion |
|----------|------|------------|
| `Id` | Guid | Identificador unico |
| `InsumoId` | Guid | FK a Insumo |
| `ContenidoHtml` | string(max) | Ficha tecnica en formato HTML |
| `UrlDocumento` | string(500) | Enlace al PDF original |
| `FechaActualizacion` | DateTimeOffset | Ultima actualizacion de la ficha |

### Invariantes de Dominio

1. Un insumo debe tener al menos un periodo de carencia definido.
2. `DiasCarencia` debe ser >= 0 (0 = sin periodo de carencia).
3. `DosisMaxima` >= `DosisMinima`.
4. `RegistroAgrocalidad` debe ser unico en el sistema.

---

## 2. Bounded Context: Aplicaciones

### Responsabilidad
Registra cada aplicacion de un insumo agroquimico a un lote. Es el nucleo de trazabilidad exigido por Agrocalidad. Cada aplicacion genera automaticamente el calculo de periodo de carencia y potencialmente un costo.

### Entidades

#### AplicacionQuimico (Aggregate Root)

| Atributo | Tipo | Descripcion |
|----------|------|------------|
| `Id` | Guid | UUID generado en cliente (soporte offline) |
| `LoteId` | Guid | FK a Lote (contexto Costos) |
| `InsumoId` | Guid | FK a Insumo (contexto Agroquimicos) |
| `FechaAplicacion` | DateTimeOffset | Fecha y hora de aplicacion en campo |
| `Dosis` | ValueObject(Dosis) | Cantidad + Unidad |
| `AreaAplicadaHa` | decimal(10,4) | Hectareas aplicadas |
| `MetodoAplicacion` | enum | FUMIGACION, DRENCH, INYECCION, GRANULAR, OTRO |
| `OperadorNombre` | string(200) | Persona que realizo la aplicacion |
| `CoordenadasGps` | ValueObject | Latitud + Longitud (nullable) |
| `Observaciones` | string(1000) | Notas del agricultor |
| `CostoTotal` | decimal(12,2) | Costo de esta aplicacion |
| `DiasCarenciaAplicables` | int | Dias de carencia del insumo para el cultivo del lote |
| `FechaFinCarencia` | DateTimeOffset | FechaAplicacion + DiasCarencia |
| `CreadoOffline` | bool | Si fue creado sin conexion |
| `ClientTimestamp` | DateTimeOffset | Timestamp del cliente al crear |
| `DeviceId` | string(100) | Dispositivo de origen |
| `RowVersion` | byte[] | Control de concurrencia |

#### DetalleAplicacion

| Atributo | Tipo | Descripcion |
|----------|------|------------|
| `Id` | Guid | Identificador unico |
| `AplicacionId` | Guid | FK a AplicacionQuimico |
| `Campo` | string(50) | Campo adicional (ej: condicion climatica) |
| `Valor` | string(500) | Valor del campo |

### Value Objects

#### Dosis
```
{ Cantidad: decimal, Unidad: UnidadMedida }
```
Invariante: Cantidad > 0.

#### CoordenadasGps
```
{ Latitud: double, Longitud: double }
```
Invariante: Latitud en [-5, 2], Longitud en [-92, -75] (rango Ecuador).

### Eventos de Dominio

#### AplicacionRegistradaEvent

Se emite cuando se registra una nueva aplicacion (tanto online como al procesar sync).

```csharp
public record AplicacionRegistradaEvent(
    Guid AplicacionId,
    Guid LoteId,
    Guid InsumoId,
    DateTimeOffset FechaAplicacion,
    int DiasCarencia,
    DateTimeOffset FechaFinCarencia,
    decimal CostoTotal
) : IDomainEvent;
```

**Suscriptores:**
- `Cosecha.EventHandlers.RecalcularBloqueoHandler` -- Verifica si hay cosechas afectadas.
- `Costos.EventHandlers.RegistrarCostoAplicacionHandler` -- Crea registro de costo si `CostoTotal > 0`.

#### AplicacionModificadaEvent

```csharp
public record AplicacionModificadaEvent(
    Guid AplicacionId,
    Guid LoteId,
    DateTimeOffset NuevaFechaFinCarencia
) : IDomainEvent;
```

### Invariantes de Dominio

1. `FechaAplicacion` no puede ser futura (tolerancia +1 hora).
2. `Dosis.Cantidad` no puede exceder `DosisMaxima` del insumo.
3. `AreaAplicadaHa` no puede exceder area total del lote.
4. `FechaFinCarencia` se calcula automaticamente: `FechaAplicacion + DiasCarencia`.
5. Una aplicacion no se puede eliminar (solo anular) por requisito de trazabilidad Agrocalidad.

---

## 3. Bounded Context: Cosecha

### Responsabilidad
Gestiona el registro de cosechas y el bloqueo por periodo de carencia. Este es el contexto donde se materializa la regla de negocio mas critica del sistema: **no se puede cosechar un lote que tiene un periodo de carencia activo**.

### Entidades

#### Cosecha (Aggregate Root)

| Atributo | Tipo | Descripcion |
|----------|------|------------|
| `Id` | Guid | UUID generado en cliente |
| `LoteId` | Guid | FK a Lote |
| `FechaCosecha` | DateTimeOffset | Fecha de la cosecha |
| `PesoTotalKg` | decimal(12,4) | Peso total cosechado en kilogramos |
| `CalidadGrado` | enum | PREMIUM, PRIMERA, SEGUNDA, RECHAZO |
| `Comprador` | string(200) | Nombre del comprador (nullable) |
| `PrecioVentaKg` | decimal(10,4) | Precio de venta por kg (nullable) |
| `IngresoTotal` | decimal(12,2) | PesoTotalKg * PrecioVentaKg (calculado) |
| `Observaciones` | string(1000) | Notas |
| `BloqueadaPorCarencia` | bool | Flag calculado: true si hay carencia activa al momento del registro |
| `CreadoOffline` | bool | Si fue creado sin conexion |
| `ClientTimestamp` | DateTimeOffset | Timestamp del cliente |
| `RowVersion` | byte[] | Control de concurrencia |

#### BloqueoCarencia

Registro que vincula una cosecha potencial con la aplicacion que la bloquea.

| Atributo | Tipo | Descripcion |
|----------|------|------------|
| `Id` | Guid | Identificador unico |
| `LoteId` | Guid | FK a Lote |
| `AplicacionId` | Guid | FK a AplicacionQuimico |
| `FechaFinCarencia` | DateTimeOffset | Fecha en que expira el bloqueo |
| `Activo` | bool | True si la fecha actual < FechaFinCarencia |

### Eventos de Dominio

#### CosechaRegistradaEvent

```csharp
public record CosechaRegistradaEvent(
    Guid CosechaId,
    Guid LoteId,
    DateTimeOffset FechaCosecha,
    decimal PesoTotalKg,
    decimal? PrecioVentaKg,
    decimal? IngresoTotal
) : IDomainEvent;
```

**Suscriptores:**
- `Costos.EventHandlers.RegistrarIngresoCosechaHandler` -- Registra el ingreso para calculo de rentabilidad.

#### CosechaBloqueadaEvent

```csharp
public record CosechaBloqueadaEvent(
    Guid LoteId,
    Guid AplicacionId,
    string InsumoNombre,
    DateTimeOffset FechaFinCarencia,
    int DiasRestantes
) : IDomainEvent;
```

**Suscriptores:**
- Logging/Auditoria -- Se registra cada intento de cosecha bloqueada.
- Notificacion al usuario (si esta online).

### Invariantes de Dominio (Regla Critica)

```
REGLA: PeriodoCarenciaRule

DADO un lote L
CUANDO se intenta registrar una cosecha con FechaCosecha = F
ENTONCES el sistema debe verificar:

  1. Obtener todas las AplicacionesQuimico del lote L
     WHERE FechaFinCarencia > F
     (aplicaciones cuyo periodo de carencia aun NO ha expirado a la fecha de cosecha)

  2. Si existe al menos una aplicacion con carencia activa:
     - BLOQUEAR la cosecha
     - Emitir CosechaBloqueadaEvent
     - Retornar error con codigo PERIODO_CARENCIA_ACTIVO
     - Incluir: nombre del insumo, fecha fin carencia, dias restantes

  3. Si NO existe ninguna aplicacion con carencia activa:
     - PERMITIR la cosecha
     - Registrar normalmente
```

**Esta regla se ejecuta en DOS lugares:**
1. **Cliente (offline)**: `periodo-carencia.service.ts` consulta RxDB para bloqueo inmediato.
2. **Servidor (sync)**: `PeriodoCarenciaRule.cs` valida autoritativamente contra SQL Server.

---

## 4. Bounded Context: Costos

### Responsabilidad
Gestiona todos los costos e ingresos asociados a un lote, calcula rentabilidad, y provee datos para el dashboard financiero. Tambien administra la entidad Lote como aggregate root.

### Entidades

#### Lote (Aggregate Root)

| Atributo | Tipo | Descripcion |
|----------|------|------------|
| `Id` | Guid | UUID |
| `FincaId` | Guid | FK a Finca |
| `Nombre` | string(200) | Nombre del lote |
| `Cultivo` | string(100) | Cultivo actual (Banano, Cacao, etc.) |
| `AreaHa` | decimal(10,4) | Area en hectareas |
| `Ubicacion` | ValueObject | Coordenadas GPS del centro del lote |
| `FechaInicioSiembra` | DateOnly | Inicio del ciclo de cultivo actual |
| `Activo` | bool | Si esta en uso |
| `RowVersion` | byte[] | Control de concurrencia |

#### Finca

| Atributo | Tipo | Descripcion |
|----------|------|------------|
| `Id` | Guid | UUID |
| `UsuarioId` | Guid | FK a Usuario (propietario) |
| `Nombre` | string(200) | Nombre de la finca |
| `Provincia` | string(100) | Provincia ecuatoriana |
| `Canton` | string(100) | Canton |
| `Parroquia` | string(100) | Parroquia |
| `AreaTotalHa` | decimal(10,4) | Area total de la finca |

#### CostoLote

| Atributo | Tipo | Descripcion |
|----------|------|------------|
| `Id` | Guid | UUID generado en cliente |
| `LoteId` | Guid | FK a Lote |
| `Fecha` | DateOnly | Fecha del costo |
| `Categoria` | enum | INSUMOS_QUIMICOS, MANO_DE_OBRA, TRANSPORTE, RIEGO, MAQUINARIA, OTROS |
| `Descripcion` | string(500) | Detalle del costo |
| `Monto` | ValueObject(Dinero) | Monto en USD |
| `AplicacionId` | Guid? | FK opcional a AplicacionQuimico (si el costo viene de una aplicacion) |
| `CosechaId` | Guid? | FK opcional a Cosecha (si es costo asociado a cosecha) |
| `CreadoOffline` | bool | Si fue creado sin conexion |
| `ClientTimestamp` | DateTimeOffset | Timestamp del cliente |
| `Eliminado` | bool | Soft delete |
| `RowVersion` | byte[] | Control de concurrencia |

#### IngresoLote

| Atributo | Tipo | Descripcion |
|----------|------|------------|
| `Id` | Guid | UUID |
| `LoteId` | Guid | FK a Lote |
| `CosechaId` | Guid | FK a Cosecha |
| `Fecha` | DateOnly | Fecha de la venta |
| `Comprador` | string(200) | Nombre del comprador |
| `KgVendidos` | decimal(12,4) | Kilogramos vendidos |
| `PrecioKg` | decimal(10,4) | Precio por kilogramo |
| `TotalVenta` | decimal(12,2) | Calculado: KgVendidos * PrecioKg |
| `RowVersion` | byte[] | Control de concurrencia |

#### PrecioMercado

| Atributo | Tipo | Descripcion |
|----------|------|------------|
| `Id` | Guid | UUID |
| `Cultivo` | string(100) | Tipo de cultivo |
| `PrecioKg` | decimal(10,4) | Precio de referencia |
| `Fuente` | string(200) | Fuente del precio (ej: MAG Ecuador) |
| `FechaPublicacion` | DateOnly | Fecha del precio |
| `Vigente` | bool | Si es el precio mas reciente |

### Value Objects

#### Dinero
```
{ Monto: decimal, Moneda: "USD" }
```
Invariante: Monto >= 0. Moneda siempre "USD" (moneda oficial de Ecuador).

### Eventos de Dominio

#### CostoRegistradoEvent

```csharp
public record CostoRegistradoEvent(
    Guid CostoId,
    Guid LoteId,
    string Categoria,
    decimal Monto
) : IDomainEvent;
```

#### PrecioMercadoActualizadoEvent

```csharp
public record PrecioMercadoActualizadoEvent(
    string Cultivo,
    decimal NuevoPrecioKg,
    DateOnly Fecha
) : IDomainEvent;
```

**Suscriptores:**
- `SignalR Hub` -- Notifica a clientes conectados via SignalR.

### Invariantes de Dominio

1. Un lote debe pertenecer a una finca del usuario autenticado.
2. `AreaHa` del lote no puede exceder `AreaTotalHa` de la finca.
3. La suma de areas de lotes de una finca no puede exceder `AreaTotalHa`.
4. `Monto` de un costo debe ser >= 0.
5. Un costo con `AplicacionId` se genera automaticamente y no puede ser eliminado sin anular la aplicacion.

### Calculo de Rentabilidad

```
Rentabilidad de un Lote L en periodo [desde, hasta]:

  TotalIngresos   = SUM(IngresoLote.TotalVenta) WHERE LoteId = L AND Fecha BETWEEN desde AND hasta
  TotalCostos     = SUM(CostoLote.Monto) WHERE LoteId = L AND Fecha BETWEEN desde AND hasta AND Eliminado = false
  UtilidadBruta   = TotalIngresos - TotalCostos
  MargenBruto     = (UtilidadBruta / TotalIngresos) * 100    -- si TotalIngresos > 0
  UtilidadPorHa   = UtilidadBruta / Lote.AreaHa
  TotalKg         = SUM(Cosecha.PesoTotalKg) WHERE LoteId = L AND FechaCosecha BETWEEN desde AND hasta
  CostoPorKg      = TotalCostos / TotalKg                     -- si TotalKg > 0
  UtilidadPorKg   = UtilidadBruta / TotalKg                   -- si TotalKg > 0
  ROI             = (UtilidadBruta / TotalCostos) * 100       -- si TotalCostos > 0
```

---

## 5. Bounded Context: Identidad

### Responsabilidad
Gestiona autenticacion (Passkeys y JWT), autorizacion basada en roles, perfiles de usuario, y registro de dispositivos.

### Entidades

#### Usuario (Aggregate Root)

| Atributo | Tipo | Descripcion |
|----------|------|------------|
| `Id` | Guid | UUID |
| `Email` | ValueObject(Email) | Email unico del usuario |
| `NombreCompleto` | string(300) | Nombre y apellido |
| `Cedula` | ValueObject(Cedula) | Cedula de identidad ecuatoriana (10 digitos con validacion de modulo) |
| `Telefono` | string(15) | Numero de telefono |
| `Rol` | enum | AGRICULTOR, ADMINISTRADOR, AUDITOR |
| `Activo` | bool | Si puede iniciar sesion |
| `FechaRegistro` | DateTimeOffset | Fecha de creacion |
| `UltimoAcceso` | DateTimeOffset? | Ultimo login exitoso |

#### CredencialPasskey

| Atributo | Tipo | Descripcion |
|----------|------|------------|
| `Id` | Guid | UUID |
| `UsuarioId` | Guid | FK a Usuario |
| `CredentialId` | byte[] | ID de la credencial WebAuthn |
| `PublicKey` | byte[] | Clave publica COSE |
| `SignCount` | uint | Contador de firmas (anti-clone) |
| `AaGuid` | Guid | GUID del autenticador |
| `CredentialType` | string(50) | Tipo (ej: "public-key") |
| `FechaRegistro` | DateTimeOffset | Cuando se registro la passkey |
| `DispositivoNombre` | string(200) | Nombre del dispositivo (ej: "Samsung Galaxy A54") |
| `Activa` | bool | Si la credencial esta habilitada |

#### SesionDispositivo

| Atributo | Tipo | Descripcion |
|----------|------|------------|
| `Id` | Guid | UUID |
| `UsuarioId` | Guid | FK a Usuario |
| `DeviceId` | string(100) | Identificador del dispositivo |
| `RefreshToken` | string(500) | Token de refresco (hashed) |
| `Plataforma` | string(50) | Android, iOS, Web |
| `AppVersion` | string(20) | Version de la app |
| `FechaCreacion` | DateTimeOffset | Inicio de sesion |
| `FechaExpiracion` | DateTimeOffset | Expiracion del refresh token |
| `Activa` | bool | Si la sesion esta vigente |

### Value Objects

#### Email
```
{ Valor: string }
```
Invariante: formato RFC 5322 valido, max 254 caracteres.

#### Cedula
```
{ Numero: string }
```
Invariante: 10 digitos, validacion de modulo 10 (algoritmo de cedula ecuatoriana).

### Invariantes de Dominio

1. Email debe ser unico en el sistema.
2. Cedula debe ser unica en el sistema.
3. Un usuario puede tener multiples CredencialPasskey (multiples dispositivos).
4. El refresh token expira en 30 dias. El access token (JWT) expira en 1 hora.
5. Despues de 5 intentos fallidos de autenticacion en 15 minutos, la cuenta se bloquea temporalmente (30 minutos).

---

## 6. Bounded Context: Sincronizacion

### Responsabilidad
Orquesta la sincronizacion bidireccional entre dispositivos offline y el servidor. Actua como **Anti-Corruption Layer** (ACL): recibe operaciones genericas y las despacha al bounded context correspondiente sin que los otros contextos conozcan detalles de sincronizacion.

### Entidades

#### OperacionPendiente

| Atributo | Tipo | Descripcion |
|----------|------|------------|
| `OperacionId` | Guid | UUID generado en cliente (idempotency key) |
| `DeviceId` | string(100) | Dispositivo de origen |
| `UsuarioId` | Guid | FK a Usuario |
| `Tipo` | enum(TipoOperacion) | CREAR_APLICACION, ACTUALIZAR_COSTO, etc. |
| `EntidadId` | Guid | ID de la entidad afectada |
| `EntidadTipo` | string(50) | Nombre del tipo de entidad |
| `Payload` | string(max) | JSON serializado del payload |
| `ClientTimestamp` | DateTimeOffset | Timestamp del cliente |
| `RowVersionAnterior` | byte[]? | RowVersion conocido por el cliente (para updates) |
| `Estado` | enum(EstadoOperacion) | PENDIENTE, APLICADA, CONFLICTO, RECHAZADA, ERROR |
| `IntentoNumero` | int | Numero de intento |
| `ProcesadoAt` | DateTimeOffset? | Cuando se proceso en el servidor |
| `ErrorDetalle` | string(2000)? | Detalle del error si aplica |

#### ConflictoSync

| Atributo | Tipo | Descripcion |
|----------|------|------------|
| `Id` | Guid | UUID |
| `OperacionId` | Guid | FK a OperacionPendiente |
| `EntidadId` | Guid | ID de la entidad en conflicto |
| `DatosCliente` | string(max) | JSON de los datos que envio el cliente |
| `DatosServidor` | string(max) | JSON de los datos actuales en servidor |
| `RowVersionCliente` | byte[] | RowVersion que tenia el cliente |
| `RowVersionServidor` | byte[] | RowVersion actual en servidor |
| `Resolucion` | enum? | CLIENTE_GANA, SERVIDOR_GANA, MERGE_MANUAL, null (sin resolver) |
| `ResueltoPor` | string? | "AUTO" o ID del usuario que resolvio |
| `ResueltoAt` | DateTimeOffset? | Cuando se resolvio |

### Enums

#### TipoOperacion
```
CREAR_APLICACION
ACTUALIZAR_APLICACION
CREAR_COSECHA
CREAR_COSTO
ACTUALIZAR_COSTO
ELIMINAR_COSTO
CREAR_LOTE
ACTUALIZAR_LOTE
```

#### EstadoOperacion
```
PENDIENTE
APLICADA
DUPLICADA
CONFLICTO
RECHAZADA
ERROR
```

### Patron de Despacho (Operation Dispatcher)

El `OperationDispatcher` es el componente central que traduce operaciones genericas de sync a commands especificos de cada bounded context:

```
OperacionPendiente { tipo: "CREAR_APLICACION", payload: {...} }
    |
    v
OperationDispatcher
    |
    +--> Deserializa payload segun tipo
    +--> Construye el Command correspondiente:
    |      RegistrarAplicacionCommand { ... }
    +--> Envia via MediatR.Send()
    +--> Captura resultado (exito/error)
    +--> Registra en SyncOperacionesLog
    +--> Retorna estado de la operacion
```

Este patron garantiza que:
1. El contexto de Sincronizacion NO contiene logica de negocio.
2. Cada operacion pasa por el mismo pipeline de validacion que una operacion online directa.
3. Los bounded contexts no saben si una operacion viene de sync offline o de un request online directo.

### Invariantes de Dominio

1. Cada `OperacionId` se procesa exactamente una vez (idempotencia).
2. Las operaciones de un batch se procesan en orden de `ClientTimestamp`.
3. Un conflicto no resuelto bloquea re-sync de esa entidad especifica (no de otras).
4. Los registros de `SyncOperacionesLog` se retienen 90 dias y luego se purgan.

---

## Diagrama de Eventos entre Contextos

```
APLICACIONES                    COSECHA                      COSTOS
-----------                    -------                      ------

RegistrarAplicacion
       |
       +--> AplicacionRegistradaEvent
       |         |                                              |
       |         +-------> RecalcularBloqueoHandler             |
       |         |         (verifica si hay cosechas            |
       |         |          afectadas por nueva carencia)       |
       |         |                                              |
       |         +--------------------------------------------> RegistrarCostoAplicacionHandler
       |                                                        (crea CostoLote si costoTotal > 0)
       |
       |
                     RegistrarCosecha
                            |
                            +--> CosechaRegistradaEvent
                            |         |
                            |         +-----------------------> RegistrarIngresoCosechaHandler
                            |                                   (registra IngresoLote)
                            |
                     [Si bloqueada]
                            |
                            +--> CosechaBloqueadaEvent
                                      |
                                      +--> Log de auditoria
                                      +--> Notificacion al usuario


COSTOS
------
ActualizarPrecioMercado
       |
       +--> PrecioMercadoActualizadoEvent
                  |
                  +--> SignalR Hub (notifica a clientes conectados)
```

---

## Glosario del Dominio (Ubiquitous Language)

| Termino | Definicion |
|---------|-----------|
| **Aplicacion** | Acto de aplicar un insumo agroquimico a un lote. No confundir con "aplicacion de software". |
| **Periodo de Carencia** | Tiempo minimo obligatorio que debe transcurrir entre la ultima aplicacion de un quimico y la cosecha. Regulado por Agrocalidad. |
| **Lote** | Subdivision de una finca dedicada a un cultivo especifico. |
| **Finca** | Propiedad agricola completa de un agricultor. |
| **Insumo** | Producto agroquimico (fungicida, herbicida, fertilizante, etc.). |
| **Bloqueo de Cosecha** | Estado en que un lote no puede ser cosechado porque tiene un periodo de carencia activo. |
| **Trazabilidad** | Capacidad de rastrear todas las aplicaciones de quimicos en un lote, requisito regulatorio de Agrocalidad. |
| **Agrocalidad** | Agencia de Regulacion y Control Fito y Zoosanitario de Ecuador. |
| **RowVersion** | Valor de concurrencia optimista de SQL Server que se incrementa con cada actualizacion. |
| **Sync Push** | Envio de operaciones acumuladas offline desde el dispositivo al servidor. |
| **Sync Pull** | Descarga de datos actualizados desde el servidor al dispositivo. |

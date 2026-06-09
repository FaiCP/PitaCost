# PROJECT_CONTEXT.md — PitaSmart
> Archivo de contexto para reinicio de sesión. Actualizado el 2026-03-25.
> **Instrucción para agentes:** Leer este archivo PRIMERO antes de ejecutar cualquier tarea.

---

## 1. Qué es PitaSmart

SaaS **Offline-First** para agricultores ecuatorianos. Dos propósitos principales:

1. **Trazabilidad Agrocalidad**: Registrar aplicaciones de químicos y bloquear cosechas cuando no se ha cumplido el **Periodo de Carencia** (tiempo mínimo obligatorio entre última aplicación y cosecha). Regulado por Agrocalidad Ecuador.
2. **Rentabilidad**: Gestión de costos e ingresos por lote, con dashboard de rentabilidad en tiempo real.

**Usuarios objetivo:** Agricultores rurales con conectividad intermitente. Dispositivos móviles Android/iOS.

**Regla de negocio crítica:**
```
SI FechaFinCarencia = FechaAplicacion + DiasCarencia (del insumo)
Y FechaCosecha < FechaFinCarencia
ENTONCES bloquear cosecha → error PERIODO_CARENCIA_ACTIVO
```
Esta regla se valida DOS veces: en el cliente (RxDB, offline) y en el servidor (SQL Server, autoritativo).

---

## 2. Tech Stack Definitivo

| Capa | Tecnología |
|------|-----------|
| Frontend | Angular 18+ PWA, Standalone Components, Signals, RxDB 15+ (IndexedDB), Service Workers |
| Backend | .NET 9, Clean Architecture, CQRS + MediatR 12+, FluentValidation 11+ |
| Base de datos | SQL Server 2022, RowVersion (TIMESTAMP) para sync optimista |
| Auth | Passkeys (WebAuthn / Fido2NetLib) como primario, JWT fallback |
| Realtime | SignalR para precios de mercado en vivo |
| Sync | Offline-First: cola de operaciones en RxDB → POST /api/sync/push al servidor |
| Reportes | jsreport (PDF oficial Agrocalidad) |
| CSS | Angular Material (decisión del agente frontend) |

---

## 3. Arquitectura del Sistema

**Estilo:** Modular Monolith con CQRS (elegido sobre microservicios para MVP).

**Razón:** Deploy simple, transacciones ACID para validaciones Agrocalidad, equipo pequeño.

**Capas (Clean Architecture):**
```
Angular PWA + RxDB (dispositivo)
    ↕ HTTPS / REST + SignalR
ASP.NET Core API (Controllers, Middleware, SignalR Hubs)
    ↓
Application Layer (MediatR Handlers, FluentValidation, DTOs)
    ↓
Domain Layer (Entidades, Value Objects, Eventos, Interfaces)
    ↓
Infrastructure Layer (EF Core, Repositorios, jsreport, SignalR provider)
    ↓
SQL Server 2022
```

**Bounded Contexts (DDD):**

| Contexto | Responsabilidad | Aggregate Root |
|----------|----------------|---------------|
| **Agroquimicos** | Catálogo de insumos, periodos de carencia, fichas técnicas Agrocalidad | Insumo |
| **Aplicaciones** | Registro de aplicaciones de químicos, trazabilidad | AplicacionQuimico |
| **Cosecha** | Registro de cosechas, bloqueo por carencia | Cosecha |
| **Costos** | Costos/ingresos por lote, cálculo de rentabilidad | Lote |
| **Identidad** | Auth (Passkeys + JWT), roles, dispositivos | Usuario |
| **Sincronizacion** | Cola offline, resolución de conflictos (ACL) | OperacionPendiente |

**Eventos de dominio clave:**
- `AplicacionRegistradaEvent` → dispara `RecalcularBloqueoHandler` (Cosecha) + `RegistrarCostoAplicacionHandler` (Costos)
- `CosechaRegistradaEvent` → dispara `RegistrarIngresoCosechaHandler` (Costos)
- `CosechaBloqueadaEvent` → log de auditoría + notificación usuario
- `PrecioMercadoActualizadoEvent` → SignalR Hub (notifica clientes conectados)

**Flujo offline-first:**
```
1. Agricultor registra en campo (sin señal)
2. Angular PWA guarda en RxDB (IndexedDB)
3. Operación se encola en RxDB como OperacionPendiente
4. Dispositivo recupera conexión → OfflineInterceptor detecta
5. SyncService.syncPendingOperations() → POST /api/sync/push
6. Servidor procesa batch, valida por RowVersion, retorna resultados
7. Cliente actualiza RxDB con nuevos RowVersions del servidor
```

**Resolución de conflictos:**
- Last-Write-Wins automático para: costos, lotes
- Merge manual requerido para: aplicaciones, cosechas (entidades reguladas Agrocalidad)
- Detección: comparación de `RowVersion` (TIMESTAMP SQL Server, Base64 en API)

---

## 4. Contratos de API

**Base URL:** `https://api.pitasmart.ec/v1`

**Endpoints implementados (documentados en `docs/architecture/api-contract.md`):**

| Método | Endpoint | Descripción |
|--------|----------|------------|
| POST | `/api/aplicaciones` | Registrar aplicación de químico |
| GET | `/api/lotes/{id}/rentabilidad` | Dashboard rentabilidad |
| GET | `/api/insumos/{id}/periodo-carencia` | Consulta periodo de carencia |
| POST | `/api/sync/push` | Sincronización desde dispositivo offline |
| POST | `/api/auth/challenge` | Inicio autenticación Passkey (WebAuthn) |
| POST | `/api/auth/verify` | Verificación Passkey → emite JWT |
| POST | `/api/cosechas` | Registrar cosecha |
| POST | `/api/costos` | Registrar costo por lote |

**Estructura de respuesta estándar:**
```json
{ "success": true/false, "data": {}, "timestamp": "ISO8601-05:00" }
{ "success": false, "error": { "code": "CODIGO_ERROR", "message": "...", "details": [] } }
```

**IDs:** UUID v4 generados en cliente para soporte offline-first. Idempotencia por `operacionId`.

**Tipos de operación sync:**
`CREAR_APLICACION` | `ACTUALIZAR_APLICACION` | `CREAR_COSECHA` | `CREAR_COSTO` | `ACTUALIZAR_COSTO` | `ELIMINAR_COSTO` | `CREAR_LOTE` | `ACTUALIZAR_LOTE`

**Estados de resultado sync por operación:**
`APLICADA` | `DUPLICADA` | `CONFLICTO` | `RECHAZADA` | `ERROR`

---

## 5. Estructura de Archivos del Proyecto (Estado al 2026-03-25 — Ronda 4)

```
PitaCost/
├── PROJECT_CONTEXT.md          ← ESTE ARCHIVO
├── docs/
│   ├── architecture/           ← COMPLETO ✓
│   │   ├── README.md, solution-structure.md, api-contract.md
│   │   ├── offline-sync-flow.md, bounded-contexts.md
│   └── database/               ← COMPLETO ✓
│       ├── schema.sql          ← 21 tablas completas, 970 líneas
│       ├── indexes.sql         ← 497 líneas, índices optimizados
│       ├── stored-procedures.sql ← 1198 líneas (sp_ValidarPeriodoCarencia, sp_ProcessSyncBatch, sp_GetRentabilidadLote)
│       ├── seed-data.sql       ← Datos de prueba: 3 agricultores, 5 insumos reales Agrocalidad
│       └── README.md           ← ERD ASCII + decisiones de diseño
│
├── src/
│   ├── backend/                ← ~98% COMPLETO
│   │   ├── PitaSmart.sln       ← Solución con 4 proyectos ✓
│   │   ├── PitaSmart.Domain/
│   │   │   ├── PitaSmart.Domain.csproj ✓
│   │   │   ├── Common/         ← BaseEntity, IDomainEvent, ValueObject, Result ✓
│   │   │   ├── Exceptions/     ← DomainException, PeriodoCarenciaException ✓
│   │   │   ├── Agroquimicos/   ← Insumo, PeriodoCarencia, FichaTecnica + interfaces ✓
│   │   │   ├── Aplicaciones/   ← AplicacionQuimico, Dosis, CoordenadasGps, eventos ✓
│   │   │   ├── Cosecha/        ← Cosecha, ICosechaRepository, eventos (Registrada, Bloqueada) ✓
│   │   │   ├── Costos/         ← Lote, Finca, CostoLote, IngresoLote, PrecioMercado ✓
│   │   │   ├── Identidad/      ← Usuario, CredencialPasskey, SesionDispositivo ✓
│   │   │   └── Sincronizacion/ ← OperacionPendiente, TipoOperacion, EstadoOperacion ✓
│   │   │
│   │   ├── PitaSmart.Application/
│   │   │   ├── PitaSmart.Application.csproj ✓
│   │   │   ├── DependencyInjection.cs ← MediatR + FluentValidation + Behaviors ✓
│   │   │   ├── Common/         ← Interfaces, ApiResponse, SyncResult, Behaviors ✓
│   │   │   └── Features/
│   │   │       ├── Aplicaciones/Commands/RegistrarAplicacion/ ← Command+Handler+Validator ✓
│   │   │       ├── Cosechas/Commands/RegistrarCosecha/ ← Command+Handler+Validator ✓ (con PeriodoCarenciaRule)
│   │   │       ├── Cosechas/Queries/GetCosechasPorLote/ ← Query+Handler ✓
│   │   │       ├── Costos/Commands/RegistrarCosto/ ← Command+Handler+Validator ✓
│   │   │       ├── Sync/Commands/ProcessSyncBatch/ ← Command+Handler ✓
│   │   │       └── Lotes/Queries/GetRentabilidad/  ← Query+Handler ✓
│   │   │       ← NOTA: Auth NO usa MediatR — AuthController llama IFido2+PasskeyService directo ✓
│   │   │
│   │   ├── PitaSmart.Infrastructure/
│   │   │   ├── PitaSmart.Infrastructure.csproj ✓
│   │   │   ├── Persistence/    ← DbContext + Repos (Aplicacion, Insumo, Lote, Costo, Cosecha, Sync) ✓
│   │   │   ├── Identity/       ← JwtTokenService, CurrentUserService, DateTimeProvider, PasskeyService ✓
│   │   │   ├── Realtime/       ← PreciosMercadoHub (SignalR) ✓
│   │   │   └── DependencyInjection.cs ✓
│   │   │
│   │   └── PitaSmart.Api/
│   │       ├── PitaSmart.Api.csproj ✓
│   │       ├── Program.cs      ← MediatR, JWT, WebAuthn, SignalR, CORS, Serilog ✓
│   │       ├── appsettings.json + appsettings.Development.json ✓
│   │       ├── Controllers/    ← Aplicaciones, Lotes, Sync, Auth, Cosechas, Costos ✓
│   │       └── Middleware/     ← GlobalExceptionMiddleware, CorrelationIdMiddleware ✓
│   │
│   └── frontend/               ← ~100% COMPLETO (MVP)
│       ├── package.json, angular.json, ngsw-config.json, tsconfig.json ✓
│       └── src/
│           ├── main.ts         ← bootstrapApplication ✓
│           ├── index.html      ← meta PWA, theme-color verde ✓
│           └── app/
│               ├── app.config.ts   ← providers: Router, HttpClient+interceptores, SW, RxDB init ✓
│               ├── app.routes.ts   ← lazy routing con authGuard ✓
│               ├── app.component.ts/html/scss ← shell con bottom nav + indicador offline ✓
│               ├── core/
│               │   ├── models/     ← lote, insumo, aplicacion, sync, auth, rentabilidad ✓
│               │   ├── database/   ← rxdb-schemas.ts, rxdb.service.ts ✓
│               │   ├── services/   ← connectivity, sync, api, auth ✓
│               │   ├── interceptors/ ← auth, correlation-id, offline ✓
│               │   └── guards/     ← auth.guard.ts ✓
│               ├── shared/
│               │   └── components/ ← offline-banner, sync-status-badge ✓
│               └── features/
│                   ├── aplicaciones/ ← service, nueva-aplicacion (ts/html/scss), lista, routes ✓
│                   ├── dashboard/    ← component (ts/html/scss) + dashboard.service.ts ✓
│                   ├── insumos/      ← insumos.service.ts ✓
│                   ├── lotes/        ← lotes.service.ts ✓
│                   ├── auth/         ← login (ts/html/scss), auth.routes.ts ✓
│                   ├── cosechas/     ← nueva-cosecha, lista-cosechas (ts/html/scss), cosechas.routes.ts ✓
│                   └── costos/       ← nuevo-costo, lista-costos (ts/html/scss), costos.routes.ts ✓
```

---

## 6. Estado de Generación por Agente (actualizado 2026-03-25 — Ronda 4)

| Agente | Estado | Notas |
|--------|--------|-------|
| Senior Software Architect | **COMPLETO** ✓ | Todos los docs de arquitectura generados |
| Senior DBA Architect | **COMPLETO** ✓ | schema.sql (21 tablas, 970L), indexes.sql (497L), stored-procedures.sql (1198L), seed-data.sql, README.md |
| Dotnet Backend Architect | **~98% COMPLETO** ✓ | Auth es directo en controller (IFido2 + PasskeyService sin MediatR). RegistrarCosto Command+Handler+Validator generados. CostosController generado. CostoLote.Crear() factory añadida. ICostoRepository.ExistsByIdAsync/GetByIdAsync añadidos. |
| Angular Senior Frontend | **~100% COMPLETO** ✓ | Shell, Core, Dashboard+Service, Feature Aplicaciones, Feature Auth (login Passkey+fallback), Feature Cosechas (lista+nueva con validación carencia offline), Feature Costos (lista+nuevo con soporte offline), todas las rutas lazy. |

---

## 7. Decisiones de Diseño Importantes

### Auth — Sin MediatR Commands (DECISIÓN FIRME)
`AuthController` implementa WebAuthn directamente con `IFido2` y `PasskeyService`.
**NO** necesita `GenerarChallengeCommand` ni `VerificarPasskeyCommand` MediatR intermedios.
El flujo completo está en el controller: challenge → WebAuthn → JWT.

### Dashboard Service — Cache-Aside con RxDB
`DashboardService.getRentabilidad()` implementa el patrón Cache-Aside:
- Online: llama `/api/lotes/{id}/rentabilidad`, guarda resultado en RxDB (`__cache_rentabilidad__*`)
- Offline: intenta leer cache de RxDB; si no hay, calcula KPIs básicos desde costos locales

### Validación Periodo de Carencia — Doble validación
1. **Cliente (offline):** `NuevaCosechaComponent.verificarCarenciaActiva()` consulta RxDB, muestra advertencia visual (no bloqueo total)
2. **Servidor (autoritativo):** `RegistrarCosechaCommandHandler` lanza `PeriodoCarenciaException` si la carencia está activa

### Frontend Auth — JWT en sessionStorage (MVP)
El JWT se guarda en `sessionStorage` (no `localStorage`). En producción se recomienda HttpOnly cookies.
El `accessToken` vive en memoria (`AuthService.accessToken` privado) para minimizar exposición XSS.

---

## 8. Prioridades — Estado MVP (2026-03-25)

### COMPLETADO en Ronda 1-2:
- ✓ Toda la arquitectura y documentación
- ✓ Schema SQL (21 tablas), indexes, stored procedures, seed-data
- ✓ Backend completo: Domain, Application (CQRS features), Infrastructure, API
- ✓ Frontend shell, Core (models/services/interceptors/guards/database)
- ✓ Feature Aplicaciones completa

### COMPLETADO en Ronda 3 — Backend:
- ✓ RegistrarCosto Command + Validator + Handler
- ✓ CostosController (POST /api/costos)
- ✓ CostoLote.Crear() static factory
- ✓ ICostoRepository.ExistsByIdAsync + GetByIdAsync

### COMPLETADO en Ronda 4 — Frontend:
- ✓ `dashboard.service.ts` — getRentabilidad con Cache-Aside y fallback KPIs offline
- ✓ Feature Auth Angular — login Passkey (WebAuthn) + formulario email+password fallback
- ✓ Feature Cosechas Angular — nueva-cosecha con validación carencia offline + lista-cosechas
- ✓ Feature Costos Angular — nuevo-costo con soporte offline + lista-costos con desglose por categoría

### MVP COMPLETO — Solo falta (Nice to Have / Post-MVP):
1. Generación PDF Agrocalidad con jsreport
2. Tests unitarios (Domain) y de integración (API)
3. CI/CD pipeline (GitHub Actions o Azure DevOps)
4. Pantalla de Configuración de dispositivo (gestión de Passkeys registradas)
5. Feature Lotes — CRUD de lotes desde la app (actualmente solo lectura/servicio)
6. Feature Insumos — catálogo navegable desde la app

---

## 9. Entidades de Dominio y Sus Campos Clave

### AplicacionQuimico
```
Id (UUID cliente), LoteId, InsumoId, FechaAplicacion, Dosis {Cantidad, Unidad},
 AreaAplicadaHa, MetodoAplicacion, OperadorNombre, CoordenadasGps?,
Observaciones?, CostoTotal, DiasCarenciaAplicables, FechaFinCarencia (calculado),
CreadoOffline, ClientTimestamp, DeviceId, RowVersion
```

### Insumo
```
Id, NombreComercial, IngredienteActivo, Fabricante, RegistroAgrocalidad,
TipoProducto (FUNGICIDA|HERBICIDA|INSECTICIDA|FERTILIZANTE|NEMATICIDA|OTRO),
CategoriaToxico (I|II|III|IV), Concentracion, DosisMinima, DosisMaxima,
UnidadDosis (L_HA|KG_HA|ML_HA|G_HA|CC_HA), Activo, RowVersion
→ tiene colección: PeriodosCarencia [{Cultivo, DiasCarencia, FuenteRegulacion}]
```

### Lote
```
Id, FincaId, Nombre, Cultivo, AreaHa, Ubicacion (GPS), FechaInicioSiembra,
Activo, RowVersion
```

### Cosecha
```
Id (UUID cliente), LoteId, FechaCosecha, PesoTotalKg, CalidadGrado,
Comprador?, PrecioVentaKg?, IngresoTotal (calculado), Observaciones?,
BloqueadaPorCarencia (flag), CreadoOffline, ClientTimestamp, RowVersion
```

### CostoLote
```
Id (UUID cliente), LoteId, Fecha, Categoria (CategoriaCosto enum),
Monto, Descripcion, AplicacionId? (auto-generado por AplicacionRegistradaEvent),
CosechaId?, CreadoOffline, ClientTimestamp, Eliminado, RowVersion
```

### OperacionPendiente (tabla de sync)
```
OperacionId (UUID cliente = idempotency key), DeviceId, UsuarioId,
Tipo (TipoOperacion enum), EntidadId, EntidadTipo, Payload (JSON),
ClientTimestamp, RowVersionAnterior?, Estado (EstadoOperacion),
IntentoNumero, ProcesadoAt?, ErrorDetalle?
```

---

## 10. Convenciones de Código

### .NET
- Namespace: `PitaSmart.[Capa].[BoundedContext].[SubCarpeta]`
- Commands/Queries: `record` inmutables
- Handlers: `async/await`, retornan `Result<T>` o `ApiResponse<T>`
- Validadores: FluentValidation, mensajes en español
- Entidades de dominio: heredan de `BaseEntity` (con `Id`, `RowVersion`, `CreatedAt`, `LastModified`)
- Excepciones de dominio: heredan de `DomainException`

### Angular
- Componentes: standalone (sin NgModules)
- Estado: Signals + computed() (NO RxJS Subject para estado)
- Servicios: `Injectable({ providedIn: 'root' })`
- Comentarios: en español
- CSS: Angular Material
- Rutas: lazy loading por feature

### SQL
- Collation: `Modern_Spanish_CI_AS` (case-insensitive, accent-sensitive)
- IDs: `UNIQUEIDENTIFIER` con `DEFAULT NEWSEQUENTIALID()`
- RowVersion: `ROWVERSION NOT NULL` (tipo TIMESTAMP de SQL Server)
- Soft delete: columna `IsDeleted BIT NOT NULL DEFAULT 0`
- Toda tabla sincronizable: `RowVersion`, `LastModified`, `CreatedAt`, `IsDeleted`, `ClientId`

---

## 11. Cómo Continuar el Trabajo

### Para el Dotnet Backend Architect (si se necesita completar):
```
Lee PROJECT_CONTEXT.md y docs/architecture/api-contract.md.
El código existente está en src/backend/.
Auth NO necesita MediatR Commands — AuthController usa IFido2 + PasskeyService directamente.
Pendiente MVP: ninguno. Post-MVP: jsreport para PDF Agrocalidad, tests.
```

### Para el Angular Senior Frontend (si se necesita completar):
```
Lee PROJECT_CONTEXT.md y docs/architecture/api-contract.md.
El código existente está en src/frontend/.
MVP completado. Post-MVP posible: feature/lotes CRUD, feature/insumos catálogo,
pantalla de configuración de Passkeys, integración SignalR para precios de mercado.
```

### Para el Senior Software Architect (si se necesita revisión):
```
Lee PROJECT_CONTEXT.md. Toda la documentación de arquitectura está en
docs/architecture/. Puede revisar consistencia o agregar decisiones que
surgieron durante la implementación.
```

### Para el Senior DBA Architect (si se necesita completar):
```
Lee PROJECT_CONTEXT.md y docs/database/schema.sql.
Base de datos completamente implementada. Post-MVP: optimizaciones de índices,
particionado de tabla OperacionesPendientes por fecha.
```

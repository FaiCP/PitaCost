# PitaSmart -- Estructura de la Solucion

## Estructura del Backend (.NET 9)

### Solucion Visual Studio

```
PitaSmart.sln
|
|-- src/
|   |
|   |-- PitaSmart.Domain/                      # Capa de Dominio (sin dependencias externas)
|   |   |-- Common/
|   |   |   |-- BaseEntity.cs                  # Id, CreatedAt, UpdatedAt, RowVersion
|   |   |   |-- IAuditableEntity.cs
|   |   |   |-- IDomainEvent.cs
|   |   |   |-- ValueObject.cs
|   |   |   |-- Result.cs                      # Result pattern para errores de dominio
|   |   |
|   |   |-- Agroquimicos/
|   |   |   |-- Entities/
|   |   |   |   |-- Insumo.cs
|   |   |   |   |-- PeriodoCarencia.cs
|   |   |   |   |-- FichaTecnica.cs
|   |   |   |-- ValueObjects/
|   |   |   |   |-- Concentracion.cs
|   |   |   |   |-- UnidadMedida.cs
|   |   |   |-- Interfaces/
|   |   |   |   |-- IInsumoRepository.cs
|   |   |
|   |   |-- Aplicaciones/
|   |   |   |-- Entities/
|   |   |   |   |-- AplicacionQuimico.cs
|   |   |   |   |-- DetalleAplicacion.cs
|   |   |   |-- ValueObjects/
|   |   |   |   |-- Dosis.cs
|   |   |   |   |-- CoordenadasGps.cs
|   |   |   |-- Events/
|   |   |   |   |-- AplicacionRegistradaEvent.cs
|   |   |   |-- Interfaces/
|   |   |   |   |-- IAplicacionRepository.cs
|   |   |   |-- Rules/
|   |   |   |   |-- AplicacionRules.cs
|   |   |
|   |   |-- Cosecha/
|   |   |   |-- Entities/
|   |   |   |   |-- Cosecha.cs
|   |   |   |   |-- LoteCosecha.cs
|   |   |   |-- ValueObjects/
|   |   |   |   |-- PesoKg.cs
|   |   |   |-- Events/
|   |   |   |   |-- CosechaRegistradaEvent.cs
|   |   |   |   |-- CosechaBloqueadaEvent.cs
|   |   |   |-- Interfaces/
|   |   |   |   |-- ICosechaRepository.cs
|   |   |   |-- Rules/
|   |   |   |   |-- PeriodoCarenciaRule.cs       # Regla de bloqueo de cosecha
|   |   |
|   |   |-- Costos/
|   |   |   |-- Entities/
|   |   |   |   |-- CostoLote.cs
|   |   |   |   |-- DetalleCosto.cs
|   |   |   |   |-- Lote.cs
|   |   |   |-- ValueObjects/
|   |   |   |   |-- Dinero.cs                    # Value Object con monto + moneda (USD)
|   |   |   |-- Interfaces/
|   |   |   |   |-- ICostoRepository.cs
|   |   |   |   |-- ILoteRepository.cs
|   |   |
|   |   |-- Identidad/
|   |   |   |-- Entities/
|   |   |   |   |-- Usuario.cs
|   |   |   |   |-- CredencialPasskey.cs
|   |   |   |-- ValueObjects/
|   |   |   |   |-- Email.cs
|   |   |   |   |-- Cedula.cs
|   |   |   |-- Interfaces/
|   |   |   |   |-- IUsuarioRepository.cs
|   |   |
|   |   |-- Sincronizacion/
|   |       |-- Entities/
|   |       |   |-- OperacionPendiente.cs
|   |       |   |-- ConflictoSync.cs
|   |       |-- Enums/
|   |       |   |-- EstadoOperacion.cs
|   |       |   |-- TipoOperacion.cs
|   |       |-- Interfaces/
|   |           |-- ISyncRepository.cs
|   |
|   |-- PitaSmart.Application/                 # Capa de Aplicacion (Use Cases)
|   |   |-- Common/
|   |   |   |-- Behaviors/
|   |   |   |   |-- ValidationBehavior.cs       # Pipeline MediatR + FluentValidation
|   |   |   |   |-- LoggingBehavior.cs
|   |   |   |   |-- TransactionBehavior.cs
|   |   |   |-- Interfaces/
|   |   |   |   |-- IApplicationDbContext.cs
|   |   |   |   |-- ICurrentUserService.cs
|   |   |   |   |-- IDateTimeProvider.cs
|   |   |   |-- Mappings/
|   |   |   |   |-- MappingProfile.cs           # AutoMapper o Mapster profiles
|   |   |   |-- Models/
|   |   |       |-- PagedResult.cs
|   |   |       |-- SyncResult.cs
|   |   |
|   |   |-- Agroquimicos/
|   |   |   |-- Queries/
|   |   |   |   |-- GetPeriodoCarencia/
|   |   |   |   |   |-- GetPeriodoCarenciaQuery.cs
|   |   |   |   |   |-- GetPeriodoCarenciaHandler.cs
|   |   |   |   |   |-- PeriodoCarenciaDto.cs
|   |   |   |-- Commands/
|   |   |       |-- CrearInsumo/
|   |   |           |-- CrearInsumoCommand.cs
|   |   |           |-- CrearInsumoHandler.cs
|   |   |           |-- CrearInsumoValidator.cs
|   |   |
|   |   |-- Aplicaciones/
|   |   |   |-- Commands/
|   |   |   |   |-- RegistrarAplicacion/
|   |   |   |       |-- RegistrarAplicacionCommand.cs
|   |   |   |       |-- RegistrarAplicacionHandler.cs
|   |   |   |       |-- RegistrarAplicacionValidator.cs
|   |   |   |-- Queries/
|   |   |       |-- GetAplicacionesPorLote/
|   |   |           |-- GetAplicacionesPorLoteQuery.cs
|   |   |           |-- GetAplicacionesPorLoteHandler.cs
|   |   |           |-- AplicacionDto.cs
|   |   |
|   |   |-- Cosecha/
|   |   |   |-- Commands/
|   |   |   |   |-- RegistrarCosecha/
|   |   |   |       |-- RegistrarCosechaCommand.cs
|   |   |   |       |-- RegistrarCosechaHandler.cs
|   |   |   |       |-- RegistrarCosechaValidator.cs
|   |   |   |-- EventHandlers/
|   |   |       |-- AplicacionRegistradaEventHandler.cs  # Recalcula bloqueos
|   |   |
|   |   |-- Costos/
|   |   |   |-- Queries/
|   |   |   |   |-- GetRentabilidadLote/
|   |   |   |       |-- GetRentabilidadLoteQuery.cs
|   |   |   |       |-- GetRentabilidadLoteHandler.cs
|   |   |   |       |-- RentabilidadDto.cs
|   |   |   |-- Commands/
|   |   |       |-- RegistrarCosto/
|   |   |           |-- RegistrarCostoCommand.cs
|   |   |           |-- RegistrarCostoHandler.cs
|   |   |           |-- RegistrarCostoValidator.cs
|   |   |
|   |   |-- Identidad/
|   |   |   |-- Commands/
|   |   |   |   |-- IniciarChallenge/
|   |   |   |   |   |-- IniciarChallengeCommand.cs
|   |   |   |   |   |-- IniciarChallengeHandler.cs
|   |   |   |   |-- VerificarPasskey/
|   |   |   |       |-- VerificarPasskeyCommand.cs
|   |   |   |       |-- VerificarPasskeyHandler.cs
|   |   |   |       |-- VerificarPasskeyValidator.cs
|   |   |   |-- Models/
|   |   |       |-- AuthTokenDto.cs
|   |   |
|   |   |-- Sincronizacion/
|   |       |-- Commands/
|   |       |   |-- ProcesarSyncPush/
|   |       |       |-- ProcesarSyncPushCommand.cs
|   |       |       |-- ProcesarSyncPushHandler.cs
|   |       |       |-- ProcesarSyncPushValidator.cs
|   |       |-- Services/
|   |           |-- ConflictResolver.cs
|   |           |-- OperationDispatcher.cs       # Despacha operaciones a handlers correctos
|   |
|   |-- PitaSmart.Infrastructure/              # Capa de Infraestructura
|   |   |-- Persistence/
|   |   |   |-- PitaSmartDbContext.cs
|   |   |   |-- Configurations/                # EF Core Fluent API configs
|   |   |   |   |-- AplicacionQuimicoConfiguration.cs
|   |   |   |   |-- InsumoConfiguration.cs
|   |   |   |   |-- CosechaConfiguration.cs
|   |   |   |   |-- CostoLoteConfiguration.cs
|   |   |   |   |-- LoteConfiguration.cs
|   |   |   |   |-- UsuarioConfiguration.cs
|   |   |   |   |-- OperacionPendienteConfiguration.cs
|   |   |   |-- Repositories/
|   |   |   |   |-- AplicacionRepository.cs
|   |   |   |   |-- InsumoRepository.cs
|   |   |   |   |-- CosechaRepository.cs
|   |   |   |   |-- CostoRepository.cs
|   |   |   |   |-- LoteRepository.cs
|   |   |   |   |-- UsuarioRepository.cs
|   |   |   |   |-- SyncRepository.cs
|   |   |   |-- Migrations/
|   |   |   |-- Interceptors/
|   |   |       |-- AuditableEntityInterceptor.cs
|   |   |       |-- DomainEventDispatcherInterceptor.cs
|   |   |
|   |   |-- Identity/
|   |   |   |-- PasskeyService.cs               # FIDO2/WebAuthn server-side
|   |   |   |-- JwtTokenService.cs
|   |   |   |-- CurrentUserService.cs
|   |   |
|   |   |-- Reporting/
|   |   |   |-- JsReportService.cs              # Generacion de PDFs Agrocalidad
|   |   |   |-- Templates/
|   |   |       |-- reporte-trazabilidad.jsrep
|   |   |
|   |   |-- Realtime/
|   |   |   |-- PreciosMercadoHub.cs            # SignalR Hub
|   |   |
|   |   |-- DependencyInjection.cs              # Registro de servicios de infraestructura
|   |
|   |-- PitaSmart.Api/                         # Capa de Presentacion (Host)
|       |-- Controllers/
|       |   |-- AplicacionesController.cs
|       |   |-- LotesController.cs
|       |   |-- InsumosController.cs
|       |   |-- CosechasController.cs
|       |   |-- SyncController.cs
|       |   |-- AuthController.cs
|       |   |-- ReportesController.cs
|       |
|       |-- Middleware/
|       |   |-- ExceptionHandlingMiddleware.cs
|       |   |-- CorrelationIdMiddleware.cs
|       |   |-- RequestLoggingMiddleware.cs
|       |
|       |-- Filters/
|       |   |-- ApiExceptionFilterAttribute.cs
|       |
|       |-- Program.cs
|       |-- appsettings.json
|       |-- appsettings.Development.json
|       |-- Dockerfile
|
|-- tests/
|   |-- PitaSmart.Domain.Tests/
|   |   |-- Cosecha/
|   |   |   |-- PeriodoCarenciaRuleTests.cs     # Tests criticos de regla de negocio
|   |   |-- Aplicaciones/
|   |       |-- AplicacionQuimicoTests.cs
|   |
|   |-- PitaSmart.Application.Tests/
|   |   |-- Aplicaciones/
|   |   |   |-- RegistrarAplicacionHandlerTests.cs
|   |   |-- Sincronizacion/
|   |       |-- ConflictResolverTests.cs
|   |
|   |-- PitaSmart.Infrastructure.Tests/
|   |   |-- Persistence/
|   |       |-- RepositoryTests.cs              # Tests de integracion con SQL Server
|   |
|   |-- PitaSmart.Api.Tests/
|       |-- Controllers/
|           |-- SyncControllerTests.cs
|           |-- AplicacionesControllerTests.cs
|
|-- docker-compose.yml                          # SQL Server + API + jsreport
|-- .editorconfig
|-- Directory.Build.props                       # Versiones centralizadas de paquetes
```

### Convenciones de Namespace

```
PitaSmart.Domain.{BoundedContext}.{SubCapa}
PitaSmart.Application.{BoundedContext}.{Commands|Queries|EventHandlers}
PitaSmart.Infrastructure.{Responsabilidad}
PitaSmart.Api.Controllers
```

Ejemplos:
- `PitaSmart.Domain.Aplicaciones.Entities.AplicacionQuimico`
- `PitaSmart.Application.Aplicaciones.Commands.RegistrarAplicacion.RegistrarAplicacionCommand`
- `PitaSmart.Infrastructure.Persistence.Repositories.AplicacionRepository`

### Dependencias entre Proyectos

```
PitaSmart.Api
  --> PitaSmart.Application
  --> PitaSmart.Infrastructure

PitaSmart.Application
  --> PitaSmart.Domain

PitaSmart.Infrastructure
  --> PitaSmart.Application
  --> PitaSmart.Domain

PitaSmart.Domain
  --> (ninguna dependencia de proyecto)
```

**Regla critica**: `PitaSmart.Domain` NO tiene dependencia de ningun otro proyecto ni de paquetes NuGet de infraestructura (ni EF Core, ni MediatR, ni nada externo). Solo define interfaces que la infraestructura implementa.

### Paquetes NuGet Clave por Proyecto

| Proyecto | Paquete | Version | Proposito |
|----------|---------|---------|-----------|
| Domain | -- | -- | Sin dependencias externas |
| Application | MediatR | 12.x | CQRS pipeline |
| Application | FluentValidation | 11.x | Validacion de commands |
| Application | Mapster | 7.x | Mapping DTO <-> Entity |
| Infrastructure | Microsoft.EntityFrameworkCore.SqlServer | 9.x | ORM + SQL Server |
| Infrastructure | Fido2.AspNet | 3.x | WebAuthn server |
| Infrastructure | Serilog.AspNetCore | 8.x | Structured logging |
| Infrastructure | jsreport.Client | 4.x | Generacion PDF |
| Api | Swashbuckle.AspNetCore | 6.x | Swagger/OpenAPI |
| Api | Microsoft.AspNetCore.SignalR | 9.x | Realtime |

---

## Estructura del Frontend (Angular 18+ PWA)

```
pitasmart-app/
|
|-- src/
|   |-- app/
|   |   |
|   |   |-- core/                              # Servicios singleton, guards, interceptors
|   |   |   |-- auth/
|   |   |   |   |-- passkey.service.ts          # WebAuthn API del navegador
|   |   |   |   |-- jwt.service.ts
|   |   |   |   |-- auth.guard.ts
|   |   |   |   |-- auth.interceptor.ts         # Adjunta JWT a requests
|   |   |   |
|   |   |   |-- sync/
|   |   |   |   |-- offline-queue.service.ts    # Cola de operaciones pendientes
|   |   |   |   |-- sync-engine.service.ts      # Orquesta push/pull con servidor
|   |   |   |   |-- conflict-resolver.service.ts
|   |   |   |   |-- connectivity.service.ts     # Detecta estado de red
|   |   |   |
|   |   |   |-- database/
|   |   |   |   |-- rxdb.service.ts             # Inicializacion y schemas RxDB
|   |   |   |   |-- rxdb-collections.ts         # Definicion de colecciones
|   |   |   |
|   |   |   |-- realtime/
|   |   |   |   |-- signalr.service.ts          # Conexion SignalR para precios
|   |   |   |
|   |   |   |-- http/
|   |   |       |-- api.service.ts              # Wrapper HTTP con retry logic
|   |   |       |-- error.interceptor.ts
|   |   |       |-- correlation-id.interceptor.ts
|   |   |
|   |   |-- features/                          # Modulos lazy-loaded por feature
|   |   |   |
|   |   |   |-- aplicaciones/
|   |   |   |   |-- pages/
|   |   |   |   |   |-- registrar-aplicacion/
|   |   |   |   |   |   |-- registrar-aplicacion.component.ts
|   |   |   |   |   |   |-- registrar-aplicacion.component.html
|   |   |   |   |   |-- listado-aplicaciones/
|   |   |   |   |       |-- listado-aplicaciones.component.ts
|   |   |   |   |       |-- listado-aplicaciones.component.html
|   |   |   |   |-- services/
|   |   |   |   |   |-- aplicaciones.service.ts  # Lee de RxDB, encola en sync
|   |   |   |   |-- models/
|   |   |   |   |   |-- aplicacion.model.ts
|   |   |   |   |-- aplicaciones.routes.ts
|   |   |   |
|   |   |   |-- cosecha/
|   |   |   |   |-- pages/
|   |   |   |   |   |-- registrar-cosecha/
|   |   |   |   |   |   |-- registrar-cosecha.component.ts
|   |   |   |   |   |   |-- registrar-cosecha.component.html
|   |   |   |   |-- services/
|   |   |   |   |   |-- cosecha.service.ts
|   |   |   |   |   |-- periodo-carencia.service.ts  # Validacion LOCAL de carencia
|   |   |   |   |-- models/
|   |   |   |   |   |-- cosecha.model.ts
|   |   |   |   |-- cosecha.routes.ts
|   |   |   |
|   |   |   |-- costos/
|   |   |   |   |-- pages/
|   |   |   |   |   |-- dashboard-rentabilidad/
|   |   |   |   |   |   |-- dashboard-rentabilidad.component.ts
|   |   |   |   |   |   |-- dashboard-rentabilidad.component.html
|   |   |   |   |   |-- registrar-costo/
|   |   |   |   |       |-- registrar-costo.component.ts
|   |   |   |   |       |-- registrar-costo.component.html
|   |   |   |   |-- services/
|   |   |   |   |   |-- costos.service.ts
|   |   |   |   |   |-- rentabilidad.service.ts  # Calculo local de rentabilidad
|   |   |   |   |-- models/
|   |   |   |   |   |-- costo.model.ts
|   |   |   |   |   |-- rentabilidad.model.ts
|   |   |   |   |-- costos.routes.ts
|   |   |   |
|   |   |   |-- insumos/
|   |   |   |   |-- pages/
|   |   |   |   |   |-- catalogo-insumos/
|   |   |   |   |       |-- catalogo-insumos.component.ts
|   |   |   |   |       |-- catalogo-insumos.component.html
|   |   |   |   |-- services/
|   |   |   |   |   |-- insumos.service.ts
|   |   |   |   |-- models/
|   |   |   |   |   |-- insumo.model.ts
|   |   |   |   |-- insumos.routes.ts
|   |   |   |
|   |   |   |-- lotes/
|   |   |   |   |-- pages/
|   |   |   |   |   |-- gestion-lotes/
|   |   |   |   |       |-- gestion-lotes.component.ts
|   |   |   |   |       |-- gestion-lotes.component.html
|   |   |   |   |-- services/
|   |   |   |   |   |-- lotes.service.ts
|   |   |   |   |-- models/
|   |   |   |   |   |-- lote.model.ts
|   |   |   |   |-- lotes.routes.ts
|   |   |   |
|   |   |   |-- auth/
|   |   |       |-- pages/
|   |   |       |   |-- login/
|   |   |       |       |-- login.component.ts
|   |   |       |       |-- login.component.html
|   |   |       |-- auth.routes.ts
|   |   |
|   |   |-- shared/                            # Componentes y pipes reutilizables
|   |   |   |-- components/
|   |   |   |   |-- sync-status-badge/          # Indicador de estado de sync
|   |   |   |   |-- offline-banner/             # Banner "Sin conexion"
|   |   |   |   |-- periodo-carencia-alert/     # Alerta de bloqueo de cosecha
|   |   |   |   |-- loading-spinner/
|   |   |   |-- pipes/
|   |   |   |   |-- currency.pipe.ts            # Formato USD ecuatoriano
|   |   |   |   |-- fecha-relativa.pipe.ts
|   |   |   |-- directives/
|   |   |   |   |-- offline-only.directive.ts   # Muestra elemento solo si offline
|   |   |   |-- validators/
|   |   |       |-- dosis.validator.ts
|   |   |       |-- cedula-ec.validator.ts       # Validacion de cedula ecuatoriana
|   |   |
|   |   |-- app.component.ts
|   |   |-- app.routes.ts
|   |   |-- app.config.ts
|   |
|   |-- assets/
|   |   |-- icons/                              # Iconos PWA
|   |   |-- i18n/
|   |       |-- es-EC.json                      # Internacionalizacion espanol Ecuador
|   |
|   |-- environments/
|   |   |-- environment.ts
|   |   |-- environment.prod.ts
|   |
|   |-- index.html
|   |-- manifest.webmanifest                    # PWA manifest
|   |-- ngsw-config.json                        # Angular Service Worker config
|   |-- styles.scss
|
|-- angular.json
|-- package.json
|-- tsconfig.json
|-- tsconfig.app.json
```

### Convenciones Angular

| Aspecto | Convencion |
|---------|-----------|
| State Management | Angular Signals para estado reactivo local; RxDB observables para datos persistidos |
| Routing | Lazy-loaded routes por feature module usando `loadChildren` |
| Componentes | Standalone components (Angular 18+), sin NgModules |
| HTTP | `HttpClient` con interceptors para auth, correlation-id, y error handling |
| Formularios | Reactive Forms con validadores custom |
| Estilos | SCSS con BEM naming; Angular Material como libreria de componentes |
| Testing | Jest para unit tests; Cypress para E2E |

### Estrategia de Signals

```typescript
// Ejemplo: estado de sincronizacion como signal
export class SyncEngineService {
  // Signal reactivo para estado de sync
  readonly syncStatus = signal<'idle' | 'syncing' | 'error' | 'offline'>('idle');
  readonly pendingOperations = signal<number>(0);
  readonly lastSyncTime = signal<Date | null>(null);

  // Computed signal para UI
  readonly hasPendingChanges = computed(() => this.pendingOperations() > 0);
}
```

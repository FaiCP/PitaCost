# PitaSmart -- Arquitectura del Sistema

## Vision General

PitaSmart es un SaaS **Offline-First** disenado para agricultores ecuatorianos que necesitan:

1. **Registrar aplicaciones de quimicos** en campo sin conectividad.
2. **Cumplir regulaciones de Agrocalidad** bloqueando cosechas cuando no se ha cumplido el Periodo de Carencia.
3. **Gestionar costos e insumos** por lote agricola.
4. **Consultar rentabilidad** en tiempo real cuando hay conectividad.

El sistema prioriza la operacion offline como caso principal (no como fallback), sincronizando datos con el servidor cuando el dispositivo recupera conectividad.

---

## Estilo Arquitectonico: Modular Monolith con CQRS

### Alternativas evaluadas

| Estilo | Ventajas | Desventajas | Veredicto |
|--------|----------|-------------|-----------|
| **Modular Monolith + CQRS** | Deploy simple, bajo costo operativo, separacion logica clara, equipo pequeno puede mantenerlo | Escalado vertical inicialmente | **ELEGIDO** |
| Microservicios | Escalado independiente por servicio, despliegue independiente | Complejidad operativa excesiva para MVP, requiere equipo DevOps dedicado | Descartado para MVP |
| Serverless (Azure Functions) | Pago por uso, auto-escalado | Cold starts afectan UX, complejidad en orquestacion offline-sync, vendor lock-in | Descartado |

### Justificacion

Para un MVP con un equipo pequeno y usuarios con conectividad intermitente, un Modular Monolith ofrece:

- **Simplicidad de despliegue**: un solo artefacto a desplegar.
- **Transacciones ACID locales**: critico para validaciones de Periodo de Carencia.
- **CQRS sin Event Sourcing**: separamos lecturas (dashboards de rentabilidad) de escrituras (registro de aplicaciones) sin la complejidad de un event store. MediatR orquesta commands y queries.
- **Camino de evolucion claro**: los modulos internos (Bounded Contexts) tienen fronteras bien definidas que permiten extraerlos a servicios independientes cuando el volumen lo justifique.

---

## Diagrama de Capas -- Clean Architecture

```
+------------------------------------------------------------------+
|                        INFRASTRUCTURE                            |
|  Angular PWA + RxDB + Service Workers (Dispositivo del usuario)  |
+------------------------------------------------------------------+
         |  HTTPS / REST + SignalR (cuando hay conectividad)
         v
+------------------------------------------------------------------+
|                      PRESENTATION LAYER                          |
|  ASP.NET Controllers, SignalR Hubs, Middleware Auth (JWT/Passkey) |
+------------------------------------------------------------------+
         |
         v
+------------------------------------------------------------------+
|                      APPLICATION LAYER                           |
|  MediatR Handlers (Commands + Queries), FluentValidation,        |
|  DTOs, Interfaces de Servicios, Mappings                         |
+------------------------------------------------------------------+
         |
         v
+------------------------------------------------------------------+
|                        DOMAIN LAYER                              |
|  Entidades, Value Objects, Eventos de Dominio,                   |
|  Reglas de Negocio (Periodo de Carencia, Rentabilidad),          |
|  Interfaces de Repositorios                                      |
+------------------------------------------------------------------+
         |
         v
+------------------------------------------------------------------+
|                     INFRASTRUCTURE LAYER                         |
|  EF Core (SQL Server), Repositorios, jsreport,                   |
|  SignalR Provider, Servicios Externos                            |
+------------------------------------------------------------------+
         |
         v
+------------------------------------------------------------------+
|                        DATA STORE                                |
|  SQL Server 2022 (RowVersion, Temporal Tables)                   |
+------------------------------------------------------------------+
```

---

## Principios de Diseno

### 1. Offline-First como Ciudadano de Primera Clase

El dispositivo del agricultor es la fuente de verdad mientras esta offline. Toda operacion critica (registrar aplicacion, registrar cosecha, registrar costo) se ejecuta localmente primero y se encola para sincronizacion.

- **RxDB sobre IndexedDB** almacena datos locales con esquemas tipados.
- **Cola de operaciones pendientes** (Operation Queue) registra cada mutacion con timestamp, tipo de operacion y payload.
- **Service Worker** intercepta requests y sirve respuestas cacheadas cuando no hay red.
- **La validacion de Periodo de Carencia se ejecuta TANTO en cliente como en servidor** para garantizar bloqueo inmediato sin esperar sync.

### 2. Domain-Driven Design -- Bounded Contexts

El dominio se divide en contextos acotados con responsabilidades claras:

| Bounded Context | Responsabilidad |
|-----------------|----------------|
| **Agroquimicos** | Catalogo de insumos, periodos de carencia, fichas tecnicas |
| **Aplicaciones** | Registro de aplicaciones de quimicos a lotes, trazabilidad |
| **Cosecha** | Registro de cosechas, bloqueo por periodo de carencia |
| **Costos** | Gestion de costos por lote, calculo de rentabilidad |
| **Identidad** | Autenticacion (Passkeys/JWT), autorizacion, perfil de usuario |
| **Sincronizacion** | Cola offline, resolucion de conflictos, reconciliacion |

### 3. CQRS con MediatR

- **Commands**: operaciones de escritura que mutan estado (RegistrarAplicacionCommand, RegistrarCosechaCommand).
- **Queries**: operaciones de lectura que NO mutan estado (ObtenerRentabilidadQuery, ConsultarPeriodoCarenciaQuery).
- **Pipeline Behaviors**: validacion (FluentValidation), logging, transaccionalidad.

### 4. Seguridad

- **Passkeys (WebAuthn)** como metodo primario de autenticacion -- no requiere recordar contrasenas, ideal para usuarios rurales.
- **JWT como fallback** para dispositivos que no soporten WebAuthn.
- **RBAC**: roles Agricultor, Administrador, Auditor (Agrocalidad).
- **Datos en transito**: TLS 1.3.
- **Datos en reposo**: Transparent Data Encryption (TDE) en SQL Server.

### 5. Observabilidad

- **Structured Logging** con Serilog + Seq/Application Insights.
- **Correlation IDs** propagados desde el cliente (generados offline) hasta el servidor.
- **Metricas RED** (Rate, Errors, Duration) en endpoints criticos.
- **Health Checks** para SQL Server, SignalR Hub, y cola de sincronizacion.

---

## Flujo de Datos de Alto Nivel

```
[Agricultor en campo]
       |
       | (Sin conexion)
       v
[Angular PWA + RxDB]  -->  Registro local + cola de operaciones
       |
       | (Conexion recuperada)
       v
[POST /api/sync/push]  -->  Envio de operaciones pendientes
       |
       v
[Backend .NET]  -->  Validacion + Persistencia + Eventos de Dominio
       |
       v
[SQL Server]  -->  Almacenamiento definitivo con RowVersion
       |
       v
[SignalR]  -->  Notificacion de precios de mercado actualizados
       |
       v
[Angular PWA]  -->  Actualizacion de dashboard de rentabilidad
```

---

## Riesgos y Trade-offs Clave

| Riesgo | Mitigacion |
|--------|-----------|
| Conflictos de datos en sincronizacion offline | Last-Write-Wins con RowVersion + log de conflictos para auditoria |
| Periodo de Carencia calculado offline puede divergir del servidor | Doble validacion (cliente + servidor); servidor es fuente de verdad autoritativa |
| Passkeys no soportadas en dispositivos Android antiguos | JWT como fallback; deteccion de capacidad en cliente |
| Modular Monolith puede convertirse en Big Ball of Mud | Fronteras estrictas entre modulos, tests de dependencia, ArchUnit |
| SQL Server como unica base de datos | Replicas de lectura para dashboards; backup automatizado; plan de DR |

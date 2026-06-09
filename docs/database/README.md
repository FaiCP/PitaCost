# PitaSmart -- Database Architecture

## Table of Contents

1. [Executive Summary](#executive-summary)
2. [Technology Stack Decision](#technology-stack-decision)
3. [ER Diagram](#er-diagram)
4. [Schema Design Decisions](#schema-design-decisions)
5. [Indexing Strategy](#indexing-strategy)
6. [Sync Strategy (RowVersion + LastModified)](#sync-strategy)
7. [Security and RBAC Model](#security-and-rbac-model)
8. [Scalability Roadmap](#scalability-roadmap)
9. [Backup and Recovery Plan](#backup-and-recovery-plan)
10. [Migration Strategy](#migration-strategy)
11. [File Reference](#file-reference)

---

## Executive Summary

PitaSmart uses a single SQL Server 2022 database as the authoritative source of truth for all bounded contexts defined in the DDD architecture. The schema is designed around two non-negotiable requirements:

1. **Offline-first sync** with conflict resolution using `ROWVERSION` (SQL Server's native optimistic concurrency token) and `LastModified` timestamps for Last-Write-Wins (LWW) resolution on non-critical entities.
2. **Periodo de Carencia enforcement** — the critical business rule that a harvest cannot be registered if any chemical application has an active quarantine period for that lot. This rule is enforced at both the client (RxDB) and the server (`sp_ValidarPeriodoCarencia` stored procedure).

The schema follows **Third Normal Form (3NF)** throughout, with two documented denormalization exceptions in `BloqueosCarencia` (InsumoNombre cached for alert queries) and `IngresosLote.TotalVenta` (computed column for report performance).

---

## Technology Stack Decision

**Selected: SQL Server 2022 (Developer/Standard/Enterprise)**

| Criterion | SQL Server 2022 | PostgreSQL 16 | MongoDB 7 |
|-----------|----------------|---------------|-----------|
| ROWVERSION native type | Native, auto-managed | Requires manual trigger | N/A |
| JSON support | JSON_VALUE, FOR JSON PATH | JSONB (superior) | Native |
| ACID compliance | Full | Full | Partial (multi-doc) |
| .NET ecosystem (.NET 8) | First-class (EF Core, SqlClient) | Good | Good |
| Licensing in Ecuador | Familiar to local DBAs | Free | Atlas pricing |
| Partitioning | Enterprise/Standard | All editions | Native |

**Why SQL Server over PostgreSQL**: The primary decision driver is `ROWVERSION` (the `TIMESTAMP` type in SQL Server). This is an automatically managed, monotonically increasing binary counter that increments on every row update — exactly what the offline sync protocol requires. Replicating this behavior in PostgreSQL requires a trigger-based approach with `xmin` system column or a manual `version` integer, both of which introduce complexity and race conditions that ROWVERSION avoids by design.

**Why not MongoDB**: PitaSmart data is highly relational (Finca -> Lote -> Aplicacion -> BloqueoCarencia chain). The periodo de carencia validation requires multi-table JOINs with aggregations. MongoDB's document model would either require heavy denormalization (creating consistency risk for the regulatorily-mandated traceability) or complex `$lookup` pipelines with inferior performance to SQL indexes.

---

## ER Diagram

```
BOUNDED CONTEXT: IDENTIDAD
==========================

Usuarios [PK: Id]
  |-- 1:N --> CredencialesPasskey [FK: UsuarioId]
  |-- 1:N --> SesionesDispositivo [FK: UsuarioId]
  |-- 1:N --> Fincas [FK: UsuarioId]

AuthChallenges (standalone, TTL 60s)
AuthIntentosFallidos (standalone, TTL 24h)


BOUNDED CONTEXT: COSTOS (incluye Fincas y Lotes)
==================================================

Usuarios
  |-- 1:N --> Fincas [FK: UsuarioId]
                |-- 1:N --> Lotes [FK: FincaId]
                              |-- 1:N --> CostosLote [FK: LoteId]
                              |-- 1:N --> IngresosLote [FK: LoteId]
                              |-- 1:N --> Aplicaciones [FK: LoteId]
                              |-- 1:N --> Cosechas [FK: LoteId]
                              |-- 1:N --> BloqueosCarencia [FK: LoteId]

PreciosMercado (standalone, alimentado por MAG Ecuador)


BOUNDED CONTEXT: AGROQUIMICOS
===============================

Insumos [PK: Id]
  |-- 1:N --> PeriodosCarencia [FK: InsumoId]  (* al menos 1 por insumo)
  |-- 0:1 --> FichasTecnicas [FK: InsumoId]
  |-- 1:N --> Aplicaciones [FK: InsumoId]


BOUNDED CONTEXT: APLICACIONES
===============================

Aplicaciones [PK: Id]
  |-- FK --> Lotes [LoteId]
  |-- FK --> Insumos [InsumoId]
  |-- 1:N --> DetallesAplicacion [FK: AplicacionId]
  |-- 0:N --> CostosLote [FK: AplicacionId]  (costo auto-generado)
  |-- 0:1 --> BloqueosCarencia [FK: AplicacionId]


BOUNDED CONTEXT: COSECHA
==========================

Cosechas [PK: Id]
  |-- FK --> Lotes [LoteId]
  |-- 0:N --> CostosLote [FK: CosechaId]
  |-- 1:1 --> IngresosLote [FK: CosechaId]


BOUNDED CONTEXT: SINCRONIZACION
=================================

OperacionesSyncPendientes [PK: OperacionId]
  |-- FK --> Usuarios [UsuarioId]
  |-- 0:1 --> ConflictosSync [FK: OperacionId]

SyncOperacionesLog [PK: OperacionId]  (idempotency log)
AuditoriaSyncEventos (event sourcing log)


FULL ERD (simplified ASCII)
============================

+------------+      +--------+      +-------+
| Usuarios   |1---N | Fincas |1---N | Lotes |
+------------+      +--------+      +---+---+
      |                                 |
      |              +------------------+------------------+
      |              |                  |                  |
      |         +----+------+    +------+----+    +--------+------+
      |         |Aplicaciones|   | Cosechas  |    | CostosLote    |
      |         +----+------+    +------+----+    +---------------+
      |              |                  |
      |         +----+------+    +------+----+
      |         |Insumos    |    | IngresosL.|
      |         +----+------+    +-----------+
      |              |
      |         +----+----------+
      |         | PeriodosC.    |
      |         +---------------+
      |
      |   +----------------------+    +------------------+
      +-->| CredencialesPasskey  |    | SesionesDisp.    |
          +----------------------+    +------------------+

      +-------------------------+    +-------------------+
      | OperacionesSyncPending  |--> | ConflictosSync    |
      +-------------------------+    +-------------------+
                |
      +--------------------+
      | SyncOperacionesLog |
      +--------------------+
```

---

## Schema Design Decisions

### 1. ClientId vs Id (Primary Key Strategy)

Every table has two identifiers:

- `Id UNIQUEIDENTIFIER DEFAULT NEWSEQUENTIALID()` — the server-side PK, clustered. Uses `NEWSEQUENTIALID()` instead of `NEWID()` to avoid page fragmentation (sequential GUIDs are B-tree friendly).
- `ClientId UNIQUEIDENTIFIER NOT NULL UNIQUE` — the UUID generated on the client device before the record is created. This is the "business identity" that devices use before a record reaches the server.

**Why**: In an offline-first system, devices must create records locally with a stable identity before syncing. If the PK were server-generated, devices would not know the ID until after sync — breaking local references between records (e.g., a CostoLote created offline that references an Aplicacion created offline on the same device). ClientId allows full consistency locally, and the server accepts it as the canonical identifier via the `UNIQUE` constraint.

### 2. ROWVERSION as Sync Concurrency Token

```sql
RowVersion ROWVERSION NOT NULL
```

SQL Server's `ROWVERSION` (alias `TIMESTAMP`) is an 8-byte binary counter that increments automatically on every INSERT or UPDATE to any row in the database. It is:

- **Monotonically increasing** — guaranteed never to repeat within a database lifetime.
- **Automatic** — no application code needed; the engine manages it.
- **Comparable** — `VARBINARY(8)` comparison works for both equality (conflict detection) and ordering (sync pull watermark).

The sync protocol uses it as follows:

1. **Conflict detection**: The client sends `rowVersionAnterior` (the ROWVERSION it knew when it last fetched the record). The server compares it with the current `CAST(RowVersion AS VARBINARY(8))`. If they differ, a concurrent modification occurred.
2. **Sync pull watermark**: `GET /api/sync/pull?desde={timestamp}` translates the timestamp to find all records with `LastModified >= @desde`. The client advances its watermark after each successful pull.

### 3. Soft Delete Pattern

All tables include `IsDeleted BIT NOT NULL DEFAULT 0`. Physical deletes are avoided for two reasons:

- **Sync pull correctness**: If a record is physically deleted on the server, devices that have it cached locally will never receive a "delete" signal. Soft deletes allow the pull response to include `IsDeleted = 1` records, which the client can then remove from RxDB.
- **Regulatory traceability**: Agrocalidad requires full audit trails. `Aplicaciones` goes further — it has an `INSTEAD OF DELETE` trigger that raises an error, forcing use of `EstadoAplicacion = 'ANULADA'` as the only valid "deletion" path.

### 4. BloqueosCarencia Denormalization (Documented Exception)

`BloqueosCarencia` stores `InsumoNombre NVARCHAR(200)` alongside the `InsumoId FK`. This violates 3NF because `InsumoNombre` is functionally dependent on `InsumoId`, not on the PK of `BloqueosCarencia`.

**Justification**: The dashboard alert query for blocked lots must be sub-100ms. Without denormalization, every alert query would JOIN to `Insumos` to get the name. Since `BloqueosCarencia` is a relatively small table (bounded by active applications) and `InsumoNombre` changes extremely rarely (it is a regulatory catalog), the denormalization risk is acceptable. A trigger on `Insumos UPDATE` would sync the name if it ever changes.

### 5. Value Objects as Flattened Columns

DDD Value Objects (Dosis, CoordenadasGps, Dinero, Concentracion) are stored as flattened columns rather than JSON blobs or separate tables:

```sql
-- Dosis Value Object:
DosisCantidad   DECIMAL(10,4)
DosisUnidad     NVARCHAR(10)

-- CoordenadasGps Value Object:
GpsLatitud      DECIMAL(10,7)
GpsLongitud     DECIMAL(10,7)
```

**Justification**: Flattened columns allow indexed lookups, CHECK constraints (Ecuador coordinate bounds), and aggregate queries (SUM, AVG). A JSON blob would require `JSON_VALUE()` on every query and cannot be indexed efficiently.

### 6. Aplicaciones: No Physical Delete + ANULADA State

Per the domain invariant "Una aplicacion no se puede eliminar (solo anular) por requisito de trazabilidad Agrocalidad", the `Aplicaciones` table has:

1. An `EstadoAplicacion` column: `ACTIVA | ANULADA`
2. An `INSTEAD OF DELETE` trigger that raises an error on any DELETE attempt
3. A cascade trigger that deactivates `BloqueosCarencia` when an application is annulled

This ensures the Agrocalidad audit trail is always complete.

### 7. Computed Column for IngresoTotal in IngresosLote

```sql
TotalVenta AS (KgVendidos * PrecioKg)
```

This is a non-persisted computed column. The formula is simple, deterministic, and aligns with the business rule `TotalVenta = KgVendidos * PrecioKg` from the domain spec. Non-persistence avoids storage overhead while keeping the formula canonical in the schema definition.

**Note**: `IngresoTotal` in `Cosechas` is NOT computed — it is stored explicitly. The reason is that sync operations need to compare the value as it existed at the time of creation (historical pricing), and a computed column would recalculate if `PrecioVentaKg` were ever corrected.

---

## Indexing Strategy

### Design Principles

1. **Covering indexes** eliminate Key Lookups. Every index on a search column includes the columns needed by the most frequent SELECT.
2. **Filtered indexes** on `IsDeleted = 0` reduce index size by 5-10% on average (assuming <5% deleted ratio), and more critically, exclude inactive rows from seek results.
3. **Column order rule**: equality predicates first, range predicates last. This maximizes prefix match selectivity.

### Critical Indexes by Query Pattern

| Query Pattern | Table | Index | Estimated Impact |
|--------------|-------|-------|-----------------|
| Login by email | Usuarios | `IX_Usuarios_Email_Login` | Full scan -> seek: ~1ms |
| Verify passkey credential | CredencialesPasskey | `IX_Passkey_CredId_Verificacion` | <1ms per auth |
| List lots by farm | Lotes | `IX_Lotes_FincaId` | 500ms -> <5ms with 50K lots |
| Validate periodo carencia | Aplicaciones | `IX_Aplicaciones_Carencia_Lookup` | Critical: must be sub-5ms |
| Rentabilidad calc (costs) | CostosLote | `IX_CostosLote_Rentabilidad` | 8s -> <100ms with 1M rows |
| Sync pull by timestamp | All tables | `IX_*_LastModified` | Prevents full scan on sync |
| Harvest block check | BloqueosCarencia | `IX_BloqueosCarencia_LoteActivo` | Used before every harvest |
| Process sync queue | OperacionesSyncPendientes | `IX_SyncPendientes_Cola` | Queue drain performance |

### The Most Critical Index

```sql
CREATE NONCLUSTERED INDEX IX_Aplicaciones_Carencia_Lookup
    ON Aplicaciones (LoteId, InsumoId, FechaFinCarencia DESC)
    INCLUDE (Id, FechaAplicacion, DiasCarenciaAplicables, EstadoAplicacion)
    WHERE IsDeleted = 0 AND EstadoAplicacion = 'ACTIVA';
```

This index serves `sp_ValidarPeriodoCarencia`, which is called on every harvest registration and on every `CREAR_COSECHA` sync operation. The filtered predicate `EstadoAplicacion = 'ACTIVA'` means annulled applications are not even in the index structure, and the `FechaFinCarencia DESC` ordering means `TOP 1` retrieves the worst-case (furthest) expiry with a single I/O.

---

## Sync Strategy

### How ROWVERSION + LastModified Work Together

These are two different mechanisms serving two different purposes:

| Field | Type | Purpose | Used By |
|-------|------|---------|---------|
| `RowVersion` | `ROWVERSION` (binary, auto) | Conflict detection in Push sync | `sp_ProcessSyncBatch` comparing `rowVersionAnterior` |
| `LastModified` | `DATETIME2(7)` | Watermark for Pull sync | `GET /api/sync/pull?desde={timestamp}` |

**Push flow (device -> server)**:

```
1. Device sends operation with rowVersionAnterior = {bytes it knew}
2. Server reads current RowVersion from the record
3. If equal: no concurrent modification -> apply operation
4. If different: conflict detected ->
   - For AplicacionQuimico/Cosecha: return CONFLICTO (merge manual required)
   - For CostoLote/Lote: apply Last-Write-Wins by comparing ClientTimestamp vs LastModified
```

**Pull flow (server -> device)**:

```
1. Device sends lastSyncTimestamp (stored locally in RxDB)
2. Server queries: WHERE LastModified >= @lastSyncTimestamp
3. Returns all changed/deleted records (IsDeleted = 1 signals device to remove from RxDB)
4. Device advances its lastSyncTimestamp to the server's current time
```

### Idempotency Guarantee

`SyncOperacionesLog` stores every processed `OperacionId`. Before processing any operation, `sp_ProcessSyncBatch` checks this table. If found, it returns `DUPLICADA` without re-executing. This handles Scenario 3 from `offline-sync-flow.md` (connection lost mid-batch, client retries all 50 operations, 30 already-processed ones return DUPLICADA).

### Conflict Resolution Summary

| Entity | Strategy | Rationale |
|--------|----------|-----------|
| `AplicacionQuimico` | Manual merge | Regulatory data; no version can be silently lost |
| `Cosecha` | Manual merge | Affects harvest block calculations |
| `CostoLote` | LWW by `ClientTimestamp` | Financial data where latest update is correct |
| `Lote` | LWW by `ClientTimestamp` | Name/area are simple fields |

---

## Security and RBAC Model

### Database Roles

```sql
-- Read-only role for application queries (SELECT only on domain tables)
CREATE ROLE pitasmart_app_readonly;
GRANT SELECT ON SCHEMA::dbo TO pitasmart_app_readonly;

-- Read-write role for API application service
CREATE ROLE pitasmart_app_readwrite;
GRANT SELECT, INSERT, UPDATE ON SCHEMA::dbo TO pitasmart_app_readwrite;
GRANT EXECUTE ON sp_ValidarPeriodoCarencia TO pitasmart_app_readwrite;
GRANT EXECUTE ON sp_ProcessSyncBatch TO pitasmart_app_readwrite;
GRANT EXECUTE ON sp_GetRentabilidadLote TO pitasmart_app_readwrite;
-- Never grant DDL permissions to app roles

-- Reporting role (BI/audit queries)
CREATE ROLE pitasmart_reporting;
GRANT SELECT ON Aplicaciones TO pitasmart_reporting;
GRANT SELECT ON Cosechas TO pitasmart_reporting;
GRANT SELECT ON CostosLote TO pitasmart_reporting;
GRANT SELECT ON IngresosLote TO pitasmart_reporting;
GRANT SELECT ON BloqueosCarencia TO pitasmart_reporting;
-- No access to: Usuarios, CredencialesPasskey, SesionesDispositivo, AuthChallenges

-- DBA admin role (for migrations and maintenance only)
CREATE ROLE pitasmart_dba;
GRANT CONTROL ON DATABASE::PitaSmart TO pitasmart_dba;
```

### Sensitive Column Protection

- `CredencialesPasskey.PublicKeyCose` — stored as `VARBINARY(8192)`. Never returned in API responses; only compared during WebAuthn verification in the application layer.
- `SesionesDispositivo.RefreshTokenHash` — stores SHA-256 hash of the refresh token. The raw token is never stored. Comparison is done in application layer.
- `Usuarios.Cedula` — consider **column-level encryption** with Always Encrypted (SQL Server) for GDPR/LGPD compliance. The application would need the column master key to query it. Currently stored as plain `NCHAR(10)` with masking recommended in non-production environments.
- `AuthIntentosFallidos.IpAddress` — PII. Purged every 24 hours by `sp_PurgarDatosAntiguos`.

### Row-Level Security (RLS) — Phase 2 Recommendation

For the current single-tenant-per-database architecture, RLS is not required. If PitaSmart moves to a **shared database multi-tenant** model (all farmers in one database), implement RLS on `Lotes`, `Aplicaciones`, `Cosechas`, and `CostosLote` filtering by `FincaId -> UsuarioId = SESSION_CONTEXT('user_id')`.

---

## Scalability Roadmap

### Short-Term (0-12 months, up to ~500K records per table)

Current schema handles this without modification. Single SQL Server 2022 instance (Standard Edition) on 4-core / 16GB RAM is sufficient.

Key monitoring metrics to track:
- `IX_Aplicaciones_Carencia_Lookup` seek efficiency (target: 0 logical reads > 100 per call)
- `sp_ProcessSyncBatch` average execution time (target: < 2s for 100-operation batch)
- `BloqueosCarencia` active row count (should not exceed 10x the number of active lots)

### Medium-Term (12-24 months, up to ~5M records per table)

1. **Read replica**: Add a secondary readable replica (SQL Server Always On AG). Route `sp_GetRentabilidadLote` and all GET endpoints to the secondary. Write operations (sync push, insert) remain on primary. Connection string routing handled by the .NET application via `ApplicationIntent=ReadOnly`.

2. **Table partitioning** for high-volume tables:
   ```sql
   -- Partition Aplicaciones by FechaAplicacion (monthly ranges)
   CREATE PARTITION FUNCTION PF_Mensual (DATETIME2)
   AS RANGE RIGHT FOR VALUES ('2025-01-01', '2025-02-01', ...);
   ```
   Target tables: `Aplicaciones`, `CostosLote`, `SyncOperacionesLog`, `AuthIntentosFallidos`.

3. **PgBouncer equivalent**: Use SQL Server's built-in connection pooling via `SqlClient` connection pool settings. For higher concurrency, add a dedicated connection proxy layer.

### Long-Term (24+ months, millions of users)

1. **Functional sharding by UsuarioId**: Separate schemas or databases per geographic region (Ecuador sierra, costa, Galapagos). Insumos catalog remains in a shared read-only database.
2. **CQRS materialized views**: Pre-compute `sp_GetRentabilidadLote` results into a `RentabilidadMaterializada` table, updated by events. Dashboard queries hit the materialized view instead of running the SP in real-time.
3. **Archival partitions**: Move Aplicaciones and CostosLote older than 3 years to compressed read-only filegroups.

---

## Backup and Recovery Plan

### RPO and RTO Targets

| Scenario | RPO (max data loss) | RTO (recovery time) |
|----------|--------------------|--------------------|
| Server crash | 15 minutes | 30 minutes |
| Disk failure | 15 minutes | 2 hours |
| Accidental data deletion | 1 hour | 4 hours |
| Regional datacenter failure | 1 hour | 6 hours |

### Backup Schedule

```
FULL backup:        Sunday 01:00 AM UTC-5     (retained 4 weeks)
DIFFERENTIAL:       Monday-Saturday 01:00 AM  (retained 1 week)
TRANSACTION LOG:    Every 15 minutes          (retained 72 hours)
```

This combination enables Point-in-Time Recovery (PITR) to any 15-minute window within the last 72 hours.

### Implementation

```sql
-- Full backup example (adjust path for actual server)
BACKUP DATABASE PitaSmart
TO DISK = 'E:\SQLBackups\PitaSmart_FULL_20260324.bak'
WITH COMPRESSION, CHECKSUM, STATS = 10;

-- Transaction log backup
BACKUP LOG PitaSmart
TO DISK = 'E:\SQLBackups\PitaSmart_LOG_20260324_1000.bak'
WITH COMPRESSION, CHECKSUM;

-- Point-in-time restore example
RESTORE DATABASE PitaSmart
FROM DISK = 'E:\SQLBackups\PitaSmart_FULL_20260324.bak'
WITH NORECOVERY;
RESTORE LOG PitaSmart
FROM DISK = 'E:\SQLBackups\PitaSmart_LOG_20260324_1000.bak'
WITH STOPAT = '2026-03-24T09:45:00', RECOVERY;
```

### Off-Site Replication

Backups must be copied to a geographically separated location within 1 hour of creation. Options for Ecuador:

1. **Azure Blob Storage** (South America region — Sao Paulo) using `BACKUP TO URL` syntax natively supported in SQL Server 2022.
2. **Secondary on-premise server** in a different city (Quito primary, Guayaquil secondary) using Always On Availability Groups.

### Restore Testing Cadence

- Monthly: Full restore to a sandbox environment; verify application connects and key queries return expected results.
- Quarterly: Full disaster recovery drill including restoring from off-site backup.

---

## Migration Strategy

### Tool Recommendation: Flyway

Flyway is recommended over Liquibase for this project because:

1. The team uses .NET; Flyway integrates cleanly with .NET CLI tooling and the CI/CD pipeline.
2. SQL-first migrations (raw T-SQL files) are more readable for a SQL Server specialist team than Liquibase XML/YAML.
3. Flyway's versioning model (`V1__baseline.sql`, `V2__add_index.sql`) maps directly to our delivery cadence.

### Migration File Naming Convention

```
docs/database/migrations/
  V1__baseline_schema.sql          <- schema.sql content (initial)
  V2__add_indexes.sql              <- indexes.sql content
  V3__seed_dev_data.sql            <- seed-data.sql (dev only, excluded from prod)
  V4__add_column_aplicaciones.sql  <- future migrations
```

### Zero-Downtime Migration Pattern (Expand-Contract)

For schema changes that affect columns used by the running application:

```
Phase 1 - EXPAND (deploy to prod immediately, app unchanged):
  ALTER TABLE Aplicaciones ADD NuevoCampo NVARCHAR(100) NULL;
  -- NULL allowed; old app writes ignore it

Phase 2 - MIGRATE data (background job):
  UPDATE Aplicaciones SET NuevoCampo = <computed value> WHERE NuevoCampo IS NULL;

Phase 3 - APPLICATION UPDATE (deploy new app version):
  -- New app reads/writes NuevoCampo

Phase 4 - CONTRACT (cleanup, weeks later):
  ALTER TABLE Aplicaciones ALTER COLUMN NuevoCampo NVARCHAR(100) NOT NULL;
  -- Or drop the old column if it was a rename
```

This pattern ensures no deployment window requires downtime.

### Rollback Strategy

Every migration file has a corresponding `U{version}__rollback_description.sql` undo script. Flyway Teams supports undo migrations; with Community edition, maintain rollback scripts manually and document their execution order.

---

## File Reference

| File | Purpose |
|------|---------|
| `schema.sql` | Complete T-SQL DDL for all tables, constraints, and triggers |
| `indexes.sql` | All non-clustered indexes with justification comments |
| `stored-procedures.sql` | `sp_ValidarPeriodoCarencia`, `sp_ProcessSyncBatch`, `sp_GetRentabilidadLote`, `sp_PurgarDatosAntiguos` |
| `seed-data.sql` | Development seed data: 3 users, 2 farms, 4 lots, 5 real Agrocalidad chemicals with real quarantine periods, application history, costs, and harvests |
| `README.md` | This document |

### Execution Order

```bash
# 1. Create schema (tables, constraints, triggers)
sqlcmd -S localhost -d master -i schema.sql

# 2. Create indexes
sqlcmd -S localhost -d PitaSmart -i indexes.sql

# 3. Create stored procedures
sqlcmd -S localhost -d PitaSmart -i stored-procedures.sql

# 4. Load seed data (development only)
sqlcmd -S localhost -d PitaSmart -i seed-data.sql
```

Or using Flyway:

```bash
flyway -url="jdbc:sqlserver://localhost;databaseName=PitaSmart" \
       -user=sa -password=<pwd> \
       -locations=filesystem:docs/database/migrations \
       migrate
```

---

## Known Limitations and Trade-offs

| Item | Current Decision | Trade-off | Mitigation |
|------|-----------------|-----------|------------|
| `IngresosLote.TotalVenta` computed column | Non-persisted computed | Cannot be indexed directly | INCLUDE columns in IX_IngresosLote_Rentabilidad cover the calc |
| `BloqueosCarencia.InsumoNombre` denormalized | Stored redundantly | Risk if InsumoNombre changes | InsumoNombre in catalog changes < once a year; add update trigger if needed |
| Cedula stored plaintext | `NCHAR(10)` | PII exposure risk | Implement Always Encrypted in Phase 2 |
| Full-text search on insumos | `LIKE '%term%'` prefix search | Cannot search mid-word | Add SQL Server Full-Text Index on NombreComercial + IngredienteActivo if catalog grows beyond 10K items |
| `sp_ProcessSyncBatch` uses single TVP | One SP processes all operation types | Large SP is harder to maintain | Consider splitting into type-specific internal SPs called by dispatcher; the interface stays the same |
| Sync log never purges conflicts | `ConflictosSync` grows indefinitely | Storage growth | Add partition or archival strategy for conflicts older than 1 year |

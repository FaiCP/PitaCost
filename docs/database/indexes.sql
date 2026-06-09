-- =============================================================================
-- PitaSmart -- Estrategia de Indices SQL Server 2022
-- Version: 1.0.0
-- Fecha: 2026-03-24
-- Autor: Senior DBA Architect
--
-- PRINCIPIOS APLICADOS:
--   1. Los indices cubren los patrones de acceso definidos en api-contract.md.
--   2. Se prefieren indices cubiertos (INCLUDE) para eliminar Key Lookups.
--   3. Los indices filtrados reducen tamano para predicados selectivos comunes.
--   4. Cada indice incluye justificacion de acceso y estimacion de impacto.
--   5. Las columnas en INDEX KEY siguen la regla: igualdad primero, rango al final.
-- =============================================================================

USE PitaSmart;
GO


-- =============================================================================
-- TABLA: Usuarios
-- Patrones de acceso:
--   - Login por Email (frecuente, debe ser sub-ms)
--   - Lookup por Cedula (admin, ocasional)
--   - Sync pull: cambios por LastModified (frecuente en reconexion)
-- =============================================================================

-- UQ_Usuarios_Email ya crea un indice unico que sirve de busqueda por email.
-- No se duplica; solo se agrega INCLUDE para evitar key lookup en login.
CREATE NONCLUSTERED INDEX IX_Usuarios_Email_Login
    ON Usuarios (Email)
    INCLUDE (Id, Rol, Activo, NombreCompleto, UltimoAcceso)
    WHERE IsDeleted = 0;
-- Estimacion: Login query pasa de scan a seek. Sub-1ms en tabla de 100K usuarios.

-- Indice para sync pull: detectar cambios de usuarios desde un timestamp
CREATE NONCLUSTERED INDEX IX_Usuarios_LastModified
    ON Usuarios (LastModified)
    INCLUDE (Id, ClientId, Email, NombreCompleto, Rol, Activo, IsDeleted);
-- Uso: GET /api/sync/pull?desde={timestamp} filtra por LastModified >= @desde


-- =============================================================================
-- TABLA: CredencialesPasskey
-- Patrones de acceso:
--   - Busqueda por CredentialIdBase64 al verificar firma WebAuthn (critica)
--   - Listar credenciales de un usuario
-- =============================================================================

-- UQ_CredencialesPasskey_CredId ya cubre busqueda por CredentialIdBase64.
-- Se agrega INCLUDE para no hacer key lookup en la verificacion.
CREATE NONCLUSTERED INDEX IX_Passkey_CredId_Verificacion
    ON CredencialesPasskey (CredentialIdBase64)
    INCLUDE (UsuarioId, PublicKeyCose, SignCount, Activa)
    WHERE IsDeleted = 0 AND Activa = 1;
-- Justificacion: La verificacion WebAuthn ocurre en cada login.
--               Sub-1ms es critico para UX de Passkeys.

CREATE NONCLUSTERED INDEX IX_Passkey_UsuarioId
    ON CredencialesPasskey (UsuarioId)
    INCLUDE (Id, CredentialIdBase64, DispositivoNombre, Activa, FechaRegistro)
    WHERE IsDeleted = 0;


-- =============================================================================
-- TABLA: SesionesDispositivo
-- Patrones de acceso:
--   - Validar refresh token por hash (frecuente: cada renovacion de JWT)
--   - Listar sesiones activas de un usuario
--   - Purga de sesiones expiradas (job nocturno)
-- =============================================================================

CREATE NONCLUSTERED INDEX IX_Sesiones_RefreshToken
    ON SesionesDispositivo (RefreshTokenHash)
    INCLUDE (UsuarioId, DeviceId, FechaExpiracion, Activa)
    WHERE IsDeleted = 0 AND Activa = 1;
-- Justificacion: Renovacion de token ocurre cada hora por dispositivo activo.

CREATE NONCLUSTERED INDEX IX_Sesiones_Usuario_Activas
    ON SesionesDispositivo (UsuarioId, Activa)
    INCLUDE (Id, DeviceId, Plataforma, AppVersion, FechaCreacion, FechaExpiracion)
    WHERE Activa = 1 AND IsDeleted = 0;

-- Indice para purga de sesiones expiradas
CREATE NONCLUSTERED INDEX IX_Sesiones_Expiracion
    ON SesionesDispositivo (FechaExpiracion)
    WHERE Activa = 1;


-- =============================================================================
-- TABLA: AuthChallenges
-- Patrones de acceso:
--   - Busqueda por ChallengeToken al verificar respuesta (muy frecuente, TTL 60s)
--   - Purga de challenges expirados
-- =============================================================================

-- UQ_AuthChallenges_Token ya cubre la busqueda principal.
CREATE NONCLUSTERED INDEX IX_AuthChallenges_Expiracion
    ON AuthChallenges (ExpiresAt)
    WHERE Usado = 0;
-- Job de purga usa este indice para DELETE WHERE ExpiresAt < SYSUTCDATETIME()

-- =============================================================================
-- TABLA: AuthIntentosFallidos
-- Patrones de acceso:
--   - Contar intentos de un email/IP en los ultimos 15 minutos (rate limit)
-- =============================================================================

CREATE NONCLUSTERED INDEX IX_AuthFallidos_Email_Tiempo
    ON AuthIntentosFallidos (Email, IntentoAt DESC)
    WHERE Email IS NOT NULL;

CREATE NONCLUSTERED INDEX IX_AuthFallidos_IP_Tiempo
    ON AuthIntentosFallidos (IpAddress, IntentoAt DESC);
-- Justificacion: Rate limit verifica COUNT(*) WHERE IntentoAt > DATEADD(MINUTE,-15, SYSUTCDATETIME())
--               El indice compuesto evita scan de tabla en picos de ataque.


-- =============================================================================
-- TABLA: Fincas
-- Patrones de acceso:
--   - Listar fincas de un usuario (pantalla home del agricultor)
--   - Sync pull por LastModified
-- =============================================================================

CREATE NONCLUSTERED INDEX IX_Fincas_UsuarioId
    ON Fincas (UsuarioId)
    INCLUDE (Id, ClientId, Nombre, Provincia, Canton, AreaTotalHa, IsDeleted)
    WHERE IsDeleted = 0;

CREATE NONCLUSTERED INDEX IX_Fincas_LastModified
    ON Fincas (LastModified)
    INCLUDE (Id, ClientId, UsuarioId, IsDeleted);


-- =============================================================================
-- TABLA: Lotes
-- Patrones de acceso:
--   - Listar lotes de una finca (muy frecuente)
--   - Filtrar lotes activos de un agricultor (dashboard)
--   - Sync pull por LastModified y AgricultorId (derivado de FincaId -> Finca.UsuarioId)
--   - Verificar pertenencia en validacion de operaciones sync
-- =============================================================================

-- Indice principal para listar lotes de una finca
CREATE NONCLUSTERED INDEX IX_Lotes_FincaId
    ON Lotes (FincaId)
    INCLUDE (Id, ClientId, Nombre, Cultivo, AreaHa, Activo, FechaInicioSiembra, IsDeleted)
    WHERE IsDeleted = 0;
-- Estimacion: Elimina table scan. Query tipica pasa de 500ms a <5ms con 50K lotes.

-- Indice para sync pull eficiente
CREATE NONCLUSTERED INDEX IX_Lotes_LastModified
    ON Lotes (LastModified)
    INCLUDE (Id, ClientId, FincaId, Cultivo, IsDeleted);

-- Indice cubierto para validacion de pertenencia en sync
CREATE NONCLUSTERED INDEX IX_Lotes_ClientId_FincaId
    ON Lotes (ClientId)
    INCLUDE (Id, FincaId, Activo, Cultivo, AreaHa)
    WHERE IsDeleted = 0;


-- =============================================================================
-- TABLA: Insumos
-- Patrones de acceso:
--   - Busqueda por texto (NombreComercial, IngredienteActivo) -- GET /api/insumos?buscar=
--   - Lookup por RegistroAgrocalidad (validacion regulatoria)
--   - Filtrar por TipoProducto
--   - Sync pull (catalogo se sincroniza en primer login)
-- =============================================================================

-- Indice para busqueda de texto en catalogo
CREATE NONCLUSTERED INDEX IX_Insumos_NombreComercial
    ON Insumos (NombreComercial)
    INCLUDE (Id, ClientId, IngredienteActivo, TipoProducto, CategoriaToxico, Activo)
    WHERE IsDeleted = 0 AND Activo = 1;
-- Uso: WHERE NombreComercial LIKE @buscar + '%' (busqueda por prefijo, soportada por indice)

CREATE NONCLUSTERED INDEX IX_Insumos_IngredienteActivo
    ON Insumos (IngredienteActivo)
    INCLUDE (Id, ClientId, NombreComercial, TipoProducto, Activo)
    WHERE IsDeleted = 0 AND Activo = 1;

CREATE NONCLUSTERED INDEX IX_Insumos_TipoProducto
    ON Insumos (TipoProducto, Activo)
    INCLUDE (Id, ClientId, NombreComercial, IngredienteActivo)
    WHERE IsDeleted = 0;

CREATE NONCLUSTERED INDEX IX_Insumos_LastModified
    ON Insumos (LastModified)
    INCLUDE (Id, ClientId, IsDeleted, Activo);


-- =============================================================================
-- TABLA: PeriodosCarencia
-- Patrones de acceso:
--   - sp_ValidarPeriodoCarencia: busca por InsumoId + Cultivo (critica, ejecutada
--     en cada registro de cosecha y en sp_ProcessSyncBatch)
--   - Listar periodos de carencia de un insumo
-- =============================================================================

-- UQ_PeriodosCarencia_Key (InsumoId, Cultivo) ya es un indice unico que sirve
-- al join de sp_ValidarPeriodoCarencia. Se agrega INCLUDE para cubrirlo.
CREATE NONCLUSTERED INDEX IX_PeriodosCarencia_Insumo_Cultivo
    ON PeriodosCarencia (InsumoId, Cultivo)
    INCLUDE (Id, DiasCarencia, FuenteRegulacion)
    WHERE IsDeleted = 0;
-- Justificacion: sp_ValidarPeriodoCarencia necesita DiasCarencia sin key lookup.
--               Esta query se ejecuta en cada registro de cosecha y en sync.


-- =============================================================================
-- TABLA: Aplicaciones
-- Patrones de acceso:
--   1. Listar aplicaciones de un lote con paginacion (GET /api/aplicaciones?loteId=)
--   2. Buscar ultima aplicacion activa de un insumo en un lote (sp_ValidarPeriodoCarencia)
--   3. Sync pull por AgricultorId + LastModified
--   4. Busqueda por ClientId para idempotencia en sync push
--   5. Trazabilidad Agrocalidad: todas las aplicaciones de un lote en un periodo
-- =============================================================================

-- Indice principal para listar aplicaciones de un lote
CREATE NONCLUSTERED INDEX IX_Aplicaciones_LoteId_Fecha
    ON Aplicaciones (LoteId, FechaAplicacion DESC)
    INCLUDE (
        Id, ClientId, InsumoId, DosisCantidad, DosisUnidad,
        AreaAplicadaHa, MetodoAplicacion, OperadorNombre,
        CostoTotal, DiasCarenciaAplicables, FechaFinCarencia,
        EstadoAplicacion, OrigenOffline
    )
    WHERE IsDeleted = 0;
-- Estimacion: Elimina scan de 500K+ aplicaciones. Query de listado: 500ms -> <10ms.

-- Indice CRITICO para sp_ValidarPeriodoCarencia:
-- busca la ultima aplicacion activa de un insumo en un lote cuya carencia no expiro
CREATE NONCLUSTERED INDEX IX_Aplicaciones_Carencia_Lookup
    ON Aplicaciones (LoteId, InsumoId, FechaFinCarencia DESC)
    INCLUDE (Id, FechaAplicacion, DiasCarenciaAplicables, EstadoAplicacion)
    WHERE IsDeleted = 0 AND EstadoAplicacion = 'ACTIVA';
-- Justificacion: Este indice es el mas critico del sistema. Se ejecuta en cada
--               validacion de cosecha (online y sync). Debe ser sub-ms.
--               Predicado filtrado elimina aplicaciones anuladas del indice.

-- Indice para sync pull por LastModified (cuando el servidor hace PULL de cambios)
CREATE NONCLUSTERED INDEX IX_Aplicaciones_LastModified
    ON Aplicaciones (LastModified)
    INCLUDE (Id, ClientId, LoteId, IsDeleted, EstadoAplicacion);

-- Indice para busqueda por InsumoId (reporte: "que lotes usaron este insumo?")
CREATE NONCLUSTERED INDEX IX_Aplicaciones_InsumoId
    ON Aplicaciones (InsumoId, FechaAplicacion DESC)
    INCLUDE (Id, LoteId, AreaAplicadaHa, CostoTotal, EstadoAplicacion)
    WHERE IsDeleted = 0;

-- Indice compuesto para reporte de trazabilidad Agrocalidad (lote + rango de fechas)
CREATE NONCLUSTERED INDEX IX_Aplicaciones_Trazabilidad
    ON Aplicaciones (LoteId, FechaAplicacion)
    INCLUDE (
        Id, InsumoId, DosisCantidad, DosisUnidad, AreaAplicadaHa,
        MetodoAplicacion, OperadorNombre, DiasCarenciaAplicables,
        FechaFinCarencia, EstadoAplicacion, GpsLatitud, GpsLongitud
    )
    WHERE IsDeleted = 0;


-- =============================================================================
-- TABLA: Cosechas
-- Patrones de acceso:
--   1. Listar cosechas de un lote (GET /api/cosechas?loteId=)
--   2. Calcular total kg cosechados en un periodo (sp_GetRentabilidadLote)
--   3. Sync pull por LastModified
-- =============================================================================

CREATE NONCLUSTERED INDEX IX_Cosechas_LoteId_Fecha
    ON Cosechas (LoteId, FechaCosecha DESC)
    INCLUDE (
        Id, ClientId, PesoTotalKg, CalidadGrado,
        Comprador, PrecioVentaKg, IngresoTotal,
        BloqueadaPorCarencia, OrigenOffline
    )
    WHERE IsDeleted = 0;

-- Indice para sp_GetRentabilidadLote: SUM de kg por lote y periodo
CREATE NONCLUSTERED INDEX IX_Cosechas_Rentabilidad
    ON Cosechas (LoteId, FechaCosecha)
    INCLUDE (PesoTotalKg, IngresoTotal, PrecioVentaKg, CalidadGrado)
    WHERE IsDeleted = 0;

CREATE NONCLUSTERED INDEX IX_Cosechas_LastModified
    ON Cosechas (LastModified)
    INCLUDE (Id, ClientId, LoteId, IsDeleted);


-- =============================================================================
-- TABLA: BloqueosCarencia
-- Patrones de acceso:
--   - Verificar si hay bloqueo activo en un lote (ANTES de registrar cosecha)
--   - Listar bloqueos activos para alertas en dashboard
--   - Job de actualizacion de bloqueos expirados
-- =============================================================================

-- Indice principal: hay bloqueo activo en este lote?
CREATE NONCLUSTERED INDEX IX_BloqueosCarencia_LoteActivo
    ON BloqueosCarencia (LoteId, Activo, FechaFinCarencia)
    INCLUDE (Id, AplicacionId, InsumoId, InsumoNombre, FechaAplicacion)
    WHERE Activo = 1;
-- Justificacion: Este indice se usa en cada pre-validacion de cosecha.
--               Consulta: WHERE LoteId = @id AND Activo = 1 AND FechaFinCarencia > SYSUTCDATETIME()

-- Indice para job que desactiva bloqueos expirados (ejecuta cada hora)
CREATE NONCLUSTERED INDEX IX_BloqueosCarencia_Expiracion
    ON BloqueosCarencia (FechaFinCarencia)
    INCLUDE (Id, LoteId)
    WHERE Activo = 1;


-- =============================================================================
-- TABLA: CostosLote
-- Patrones de acceso:
--   1. Listar costos de un lote en un periodo (GET /api/lotes/{id}/rentabilidad)
--   2. sp_GetRentabilidadLote: SUM por categoria y periodo
--   3. Sync pull por LastModified
--   4. Busqueda de costos originados por una aplicacion (trazabilidad)
-- =============================================================================

-- Indice principal para rentabilidad: lote + rango de fechas + categoria
CREATE NONCLUSTERED INDEX IX_CostosLote_Rentabilidad
    ON CostosLote (LoteId, Fecha)
    INCLUDE (Id, Categoria, Monto, Descripcion, AplicacionId, OrigenOffline)
    WHERE IsDeleted = 0;
-- Estimacion: sp_GetRentabilidadLote pasa de scan a seek+range.
--             Con 1M costos, tiempo estimado: de 8s a <100ms.

-- Indice cubriente para desglose por categoria
CREATE NONCLUSTERED INDEX IX_CostosLote_LoteCategoria
    ON CostosLote (LoteId, Categoria, Fecha)
    INCLUDE (Monto)
    WHERE IsDeleted = 0;

-- Indice para trazabilidad: costos generados por una aplicacion especifica
CREATE NONCLUSTERED INDEX IX_CostosLote_AplicacionId
    ON CostosLote (AplicacionId)
    INCLUDE (Id, LoteId, Fecha, Monto, Categoria)
    WHERE AplicacionId IS NOT NULL AND IsDeleted = 0;

CREATE NONCLUSTERED INDEX IX_CostosLote_LastModified
    ON CostosLote (LastModified)
    INCLUDE (Id, ClientId, LoteId, IsDeleted);


-- =============================================================================
-- TABLA: IngresosLote
-- Patrones de acceso:
--   - sp_GetRentabilidadLote: SUM de ingresos por lote y periodo
--   - Detalle de ventas en dashboard
-- =============================================================================

CREATE NONCLUSTERED INDEX IX_IngresosLote_Rentabilidad
    ON IngresosLote (LoteId, Fecha)
    INCLUDE (Id, CosechaId, KgVendidos, PrecioKg, Comprador)
    WHERE IsDeleted = 0;
-- TotalVenta es columna calculada; no se puede incluir en indice directamente.
-- La formula KgVendidos * PrecioKg se evalua sobre los valores del INCLUDE.


-- =============================================================================
-- TABLA: PreciosMercado
-- Patrones de acceso:
--   - Precio vigente de un cultivo (dashboard, sync push response)
-- =============================================================================

CREATE NONCLUSTERED INDEX IX_PreciosMercado_Cultivo_Vigente
    ON PreciosMercado (Cultivo, Vigente, FechaPublicacion DESC)
    INCLUDE (Id, PrecioKg, Fuente)
    WHERE IsDeleted = 0 AND Vigente = 1;


-- =============================================================================
-- TABLA: OperacionesSyncPendientes
-- Patrones de acceso:
--   1. sp_ProcessSyncBatch: procesar operaciones PENDIENTES de un usuario/dispositivo
--   2. Monitoreo: contar operaciones pendientes por estado
--   3. Verificar idempotencia (OperacionId ya existe?) -- cubierto por PK
-- =============================================================================

-- Indice principal para la cola de procesamiento del sync engine
CREATE NONCLUSTERED INDEX IX_SyncPendientes_Cola
    ON OperacionesSyncPendientes (UsuarioId, Estado, ClientTimestamp)
    INCLUDE (OperacionId, DeviceId, Tipo, EntidadId, EntidadTipo, IntentoNumero)
    WHERE Estado IN ('PENDIENTE', 'EN_PROCESO');
-- Justificacion: sp_ProcessSyncBatch selecciona operaciones pendientes de un usuario
--               y las ordena por ClientTimestamp. Este indice cubre completamente esa query.

-- Indice por DeviceId para monitoreo y debugging
CREATE NONCLUSTERED INDEX IX_SyncPendientes_Device
    ON OperacionesSyncPendientes (DeviceId, CreatedAt DESC)
    INCLUDE (OperacionId, Estado, Tipo, EntidadId, IntentoNumero);

-- Indice para limpiar operaciones procesadas (job de mantenimiento)
CREATE NONCLUSTERED INDEX IX_SyncPendientes_ProcesadoAt
    ON OperacionesSyncPendientes (ProcesadoAt)
    WHERE Estado IN ('APLICADA', 'DUPLICADA', 'RECHAZADA');


-- =============================================================================
-- TABLA: SyncOperacionesLog
-- Patron de acceso:
--   - Verificar idempotencia (PK ya lo cubre)
--   - Purga de registros mayores a 90 dias (job nocturno)
-- =============================================================================

CREATE NONCLUSTERED INDEX IX_SyncLog_Device
    ON SyncOperacionesLog (DeviceId, ProcesadoAt DESC)
    INCLUDE (OperacionId, Estado, Tipo);
-- Identico al indice mencionado en offline-sync-flow.md

CREATE NONCLUSTERED INDEX IX_SyncLog_Purga
    ON SyncOperacionesLog (ProcesadoAt)
    INCLUDE (OperacionId);
-- Job de purga: DELETE WHERE ProcesadoAt < DATEADD(DAY, -90, SYSUTCDATETIME())

CREATE NONCLUSTERED INDEX IX_SyncLog_UsuarioId
    ON SyncOperacionesLog (UsuarioId, ProcesadoAt DESC)
    INCLUDE (OperacionId, Tipo, Estado);


-- =============================================================================
-- TABLA: ConflictosSync
-- Patron de acceso:
--   - Listar conflictos sin resolver (panel de administracion)
--   - Buscar conflictos de una entidad especifica
-- =============================================================================

CREATE NONCLUSTERED INDEX IX_ConflictosSync_SinResolver
    ON ConflictosSync (Resolucion, CreadoAt DESC)
    INCLUDE (Id, OperacionId, EntidadId, EntidadTipo)
    WHERE Resolucion IS NULL;

CREATE NONCLUSTERED INDEX IX_ConflictosSync_EntidadId
    ON ConflictosSync (EntidadId)
    INCLUDE (Id, OperacionId, EntidadTipo, CreadoAt, Resolucion);


-- =============================================================================
-- ESTADISTICAS ADICIONALES Y MANTENIMIENTO
-- =============================================================================

-- Estadisticas manuales para columnas de alta cardinalidad usadas en WHERE
-- SQL Server las crea automaticamente, pero estas garantizan actualizacion
-- inmediata en columnas criticas.

-- Estadistica para columna Cultivo en Lotes (usada en JOIN con PeriodosCarencia)
CREATE STATISTICS STAT_Lotes_Cultivo ON Lotes (Cultivo, FincaId);

-- Estadistica para rango de fechas en CostosLote (sp_GetRentabilidadLote)
CREATE STATISTICS STAT_CostosLote_LoteFecha ON CostosLote (LoteId, Fecha);

-- Estadistica para FechaFinCarencia en Aplicaciones (sp_ValidarPeriodoCarencia)
CREATE STATISTICS STAT_Aplicaciones_Carencia ON Aplicaciones (LoteId, InsumoId, FechaFinCarencia);
GO


-- =============================================================================
-- NOTAS DE MANTENIMIENTO DE INDICES
--
-- SCHEDULE RECOMENDADO (SQL Server Agent Jobs):
--
--   1. Nocturnamente (2:00 AM UTC-5):
--      - REORGANIZE indices con fragmentacion entre 10% y 30%
--      - REBUILD indices con fragmentacion > 30%
--      - UPDATE STATISTICS en tablas con > 20% de filas modificadas desde
--        la ultima actualizacion
--      - Purga de AuthChallenges expirados
--      - Purga de SyncOperacionesLog > 90 dias
--      - Purga de AuthIntentosFallidos > 24 horas
--      - Desactivar BloqueosCarencia con FechaFinCarencia < SYSUTCDATETIME()
--
--   2. Query de verificacion de fragmentacion:
--      SELECT
--          OBJECT_NAME(ips.object_id) AS TableName,
--          i.name AS IndexName,
--          ips.avg_fragmentation_in_percent,
--          ips.page_count
--      FROM sys.dm_db_index_physical_stats(DB_ID(), NULL, NULL, NULL, 'LIMITED') ips
--      JOIN sys.indexes i ON ips.object_id = i.object_id AND ips.index_id = i.index_id
--      WHERE ips.avg_fragmentation_in_percent > 10
--        AND ips.page_count > 1000
--      ORDER BY ips.avg_fragmentation_in_percent DESC;
--
--   3. Tablas de alto volumen esperado (> 1M filas en 12 meses):
--      - Aplicaciones:            Particionar por FechaAplicacion (RANGE RIGHT por mes)
--      - CostosLote:              Particionar por Fecha (RANGE RIGHT por mes)
--      - SyncOperacionesLog:      Particionar por ProcesadoAt (RANGE RIGHT por mes)
--      - AuthIntentosFallidos:    Considerar tabla deslizante (TRUNCATE particion antigua)
--
--   Ver partitioning.sql (a crear en fase 2) para implementacion de particiones.
-- =============================================================================

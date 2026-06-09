-- =============================================================================
-- PitaSmart -- Stored Procedures Criticos SQL Server 2022
-- Version: 1.0.0
-- Fecha: 2026-03-24
-- Autor: Senior DBA Architect
--
-- PROCEDIMIENTOS:
--   1. sp_ValidarPeriodoCarencia   -- Valida si un lote puede ser cosechado
--   2. sp_ProcessSyncBatch         -- Procesa batch de operaciones offline
--   3. sp_GetRentabilidadLote      -- Calcula rentabilidad de un lote en un periodo
--
-- CONVENCIONES:
--   - SET NOCOUNT ON en todos los SPs para evitar mensajes de filas afectadas
--   - Manejo de errores con TRY/CATCH y THROW para propagacion al caller
--   - Parametros de salida documentados con @Out_ prefijo
--   - Transacciones explicitas solo cuando hay escritura de multiples filas
-- =============================================================================

USE PitaSmart;
GO


-- =============================================================================
-- SP 1: sp_ValidarPeriodoCarencia
--
-- PROPOSITO:
--   Verifica si un lote puede ser cosechado en una fecha dada, considerando
--   el periodo de carencia de TODAS las aplicaciones activas de ese lote.
--   Implementa la REGLA CRITICA del bounded context Cosecha.
--
-- CONTEXTO DE USO:
--   - Llamado por el endpoint POST /api/cosechas antes de insertar.
--   - Llamado por sp_ProcessSyncBatch al procesar operacion CREAR_COSECHA.
--   - Llamado por GET /api/insumos/{id}/periodo-carencia?loteId=...
--
-- INPUTS:
--   @LoteId           -- Lote que se desea cosechar
--   @FechaCosecha     -- Fecha propuesta para la cosecha (puede ser retroactiva)
--   @InsumoId         -- OPCIONAL: si se especifica, filtra solo ese insumo
--                        (usado en GET /api/insumos/{id}/periodo-carencia)
--
-- OUTPUTS:
--   @PuedeCortar      -- 1 si la cosecha es permitida, 0 si esta bloqueada
--   @DiasRestantes    -- Dias hasta que expire el proximo bloqueo (0 si puede cortar)
--   @UltimaAplicacion -- Fecha de la aplicacion que genera el bloqueo mas proximo
--   @InsumoNombreBloqueo  -- Nombre del insumo que genera el bloqueo
--   @FechaFinCarencia -- Fecha en que expira el bloqueo activo (NULL si puede cortar)
--
-- PERFORMANCE:
--   Usa IX_Aplicaciones_Carencia_Lookup para seek directo sin scan.
--   Tiempo esperado: < 5ms en tabla con 1M+ aplicaciones.
-- =============================================================================

CREATE OR ALTER PROCEDURE sp_ValidarPeriodoCarencia
    @LoteId               UNIQUEIDENTIFIER,
    @FechaCosecha         DATETIME2(7),
    @InsumoId             UNIQUEIDENTIFIER    = NULL,     -- Opcional: filtrar por insumo
    -- Parametros de salida
    @PuedeCortar          BIT                 OUTPUT,
    @DiasRestantes        INT                 OUTPUT,
    @UltimaAplicacion     DATETIME2(7)        OUTPUT,
    @InsumoNombreBloqueo  NVARCHAR(200)       OUTPUT,
    @FechaFinCarenciaOut  DATETIME2(7)        OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    -- Inicializar salidas a valores seguros (puede cortar por defecto)
    SET @PuedeCortar         = 1;
    SET @DiasRestantes       = 0;
    SET @UltimaAplicacion    = NULL;
    SET @InsumoNombreBloqueo = NULL;
    SET @FechaFinCarenciaOut = NULL;

    -- Verificar que el lote existe y no esta eliminado
    IF NOT EXISTS (
        SELECT 1 FROM Lotes WHERE Id = @LoteId AND IsDeleted = 0
    )
    BEGIN
        RAISERROR('El lote especificado no existe o ha sido eliminado.', 16, 1);
        RETURN;
    END;

    -- Buscar la aplicacion con carencia activa que expira mas tarde
    -- (worst case: el bloqueo que dura mas es el que importa para la fecha de cosecha)
    -- LOGICA: Una aplicacion bloquea la cosecha si FechaFinCarencia > @FechaCosecha
    --         Y el InsumoId coincide (si se especifico)
    SELECT TOP 1
        @PuedeCortar         = 0,
        @UltimaAplicacion    = a.FechaAplicacion,
        @InsumoNombreBloqueo = i.NombreComercial,
        @FechaFinCarenciaOut = a.FechaFinCarencia,
        -- DiasRestantes se calcula respecto a HOY (no a FechaCosecha)
        -- para dar informacion util al agricultor sobre cuanto falta
        @DiasRestantes       = DATEDIFF(
                                   DAY,
                                   CAST(SYSUTCDATETIME() AS DATE),
                                   CAST(a.FechaFinCarencia AS DATE)
                               )
    FROM Aplicaciones a
    INNER JOIN Insumos i ON a.InsumoId = i.Id
    WHERE
        a.LoteId            = @LoteId
        AND a.IsDeleted     = 0
        AND a.EstadoAplicacion = 'ACTIVA'
        -- La aplicacion bloquea si su carencia no ha expirado a la fecha de cosecha propuesta
        AND a.FechaFinCarencia > @FechaCosecha
        -- Filtro opcional por insumo especifico
        AND (@InsumoId IS NULL OR a.InsumoId = @InsumoId)
    -- Ordenar por la fecha de fin de carencia mas lejana (el bloqueo mas critico)
    ORDER BY a.FechaFinCarencia DESC;

    -- Si se encontro un bloqueo, PuedeCortar ya es 0.
    -- Si DiasRestantes es negativo (la carencia ya paso pero FechaFinCarencia > FechaCosecha retroactiva),
    -- se ajusta a 0 para no confundir al usuario.
    IF @DiasRestantes < 0
        SET @DiasRestantes = 0;

END;
GO


-- =============================================================================
-- SP 2: sp_ProcessSyncBatch
--
-- PROPOSITO:
--   Procesa un lote de operaciones enviadas desde un dispositivo offline.
--   Implementa el flujo definido en offline-sync-flow.md (pasos 8-10).
--
-- LOGICA POR OPERACION:
--   a) Idempotencia: si OperacionId ya esta en SyncOperacionesLog -> DUPLICADA
--   b) Verificacion RowVersion: para updates, compara RowVersionAnterior
--   c) Despacho por tipo: llama al handler especifico para cada TipoOperacion
--   d) Registro en SyncOperacionesLog para idempotencia futura
--   e) Devuelve resultado individual por operacion
--
-- ESTRATEGIA DE CONFLICTOS (segun offline-sync-flow.md):
--   - AplicacionQuimico y Cosecha: MERGE_MANUAL -> siempre retorna CONFLICTO
--   - CostoLote y Lote: Last-Write-Wins por ClientTimestamp
--
-- INPUTS:
--   @UsuarioId        -- Usuario autenticado (del JWT)
--   @DeviceId         -- Dispositivo de origen
--   @OperacionesTVP   -- Table-Valued Parameter con las operaciones del batch
--
-- OUTPUT:
--   Resultado set con una fila por operacion procesada.
--
-- NOTA: Este SP orquesta el proceso. La logica de negocio especifica
--       (calcular FechaFinCarencia, validar dosis, etc.) se ejecuta en
--       los SPs de insercion individuales llamados desde aqui.
--
-- AISLAMIENTO: READ COMMITTED (default). Cada operacion en su propia
--              transaccion para que un error no revierta todo el batch.
-- =============================================================================

-- Tipo de tabla para el batch de operaciones (TVP)
IF TYPE_ID('dbo.TipoOperacionSyncTVP') IS NULL
BEGIN
    EXEC('
    CREATE TYPE dbo.TipoOperacionSyncTVP AS TABLE (
        OperacionId         UNIQUEIDENTIFIER    NOT NULL,
        Tipo                NVARCHAR(30)        NOT NULL,
        EntidadId           UNIQUEIDENTIFIER    NOT NULL,
        EntidadTipo         NVARCHAR(50)        NOT NULL,
        Payload             NVARCHAR(MAX)       NOT NULL,
        ClientTimestamp     DATETIME2(7)        NOT NULL,
        RowVersionAnterior  VARBINARY(8)        NULL,
        IntentoNumero       INT                 NOT NULL    DEFAULT 1,
        -- Numero de orden para procesamiento secuencial por ClientTimestamp
        OrdenProcesamiento  INT                 NOT NULL    IDENTITY(1,1)
    )
    ');
END;
GO

CREATE OR ALTER PROCEDURE sp_ProcessSyncBatch
    @UsuarioId      UNIQUEIDENTIFIER,
    @DeviceId       NVARCHAR(100),
    @Operaciones    dbo.TipoOperacionSyncTVP READONLY
AS
BEGIN
    SET NOCOUNT ON;

    -- Tabla de resultados para el response al cliente
    DECLARE @Resultados TABLE (
        OperacionId         UNIQUEIDENTIFIER    NOT NULL,
        Estado              NVARCHAR(15)        NOT NULL,
        EntidadId           UNIQUEIDENTIFIER    NOT NULL,
        RowVersionNuevo     VARBINARY(8)        NULL,
        CodigoError         NVARCHAR(50)        NULL,
        MensajeError        NVARCHAR(2000)      NULL,
        DatosServidor       NVARCHAR(MAX)       NULL    -- JSON para conflictos
    );

    -- Variables para el loop
    DECLARE
        @OperacionId        UNIQUEIDENTIFIER,
        @Tipo               NVARCHAR(30),
        @EntidadId          UNIQUEIDENTIFIER,
        @EntidadTipo        NVARCHAR(50),
        @Payload            NVARCHAR(MAX),
        @ClientTimestamp    DATETIME2(7),
        @RowVersionAnterior VARBINARY(8),
        @IntentoNumero      INT,
        @OrdenProceso       INT,
        @MaxOrden           INT;

    -- Variables de resultado por operacion
    DECLARE
        @EstadoOp           NVARCHAR(15),
        @RowVersionNuevo    VARBINARY(8),
        @CodigoError        NVARCHAR(50),
        @MensajeError       NVARCHAR(2000),
        @DatosServidor      NVARCHAR(MAX);

    -- Variables para handlers especificos
    DECLARE
        @RowVersionActual   VARBINARY(8),
        @UpdatedAt          DATETIME2(7),
        @PuedeCortar        BIT,
        @DiasRestantes      INT,
        @UltimaAplicacion   DATETIME2(7),
        @InsumoNombreBloqueo NVARCHAR(200),
        @FechaFinCarenciaOut DATETIME2(7);

    SELECT @MaxOrden = MAX(OrdenProcesamiento) FROM @Operaciones;
    SET @OrdenProceso = 1;

    -- =========================================================================
    -- LOOP PRINCIPAL: Procesar cada operacion en orden de ClientTimestamp
    -- =========================================================================
    WHILE @OrdenProceso <= @MaxOrden
    BEGIN
        -- Leer la operacion actual
        SELECT
            @OperacionId        = OperacionId,
            @Tipo               = Tipo,
            @EntidadId          = EntidadId,
            @EntidadTipo        = EntidadTipo,
            @Payload            = Payload,
            @ClientTimestamp    = ClientTimestamp,
            @RowVersionAnterior = RowVersionAnterior,
            @IntentoNumero      = IntentoNumero
        FROM @Operaciones
        WHERE OrdenProcesamiento = @OrdenProceso;

        -- Resetear variables de resultado
        SET @EstadoOp       = NULL;
        SET @RowVersionNuevo = NULL;
        SET @CodigoError    = NULL;
        SET @MensajeError   = NULL;
        SET @DatosServidor  = NULL;

        BEGIN TRY

            -- =================================================================
            -- PASO A: Verificar idempotencia
            -- Si la operacion ya fue procesada, retornar DUPLICADA sin reprocesar
            -- =================================================================
            IF EXISTS (
                SELECT 1 FROM SyncOperacionesLog WHERE OperacionId = @OperacionId
            )
            BEGIN
                -- Recuperar el RowVersion resultante de la operacion original
                SELECT @RowVersionNuevo = RowVersionResultante
                FROM SyncOperacionesLog
                WHERE OperacionId = @OperacionId;

                SET @EstadoOp = 'DUPLICADA';
                GOTO RegistrarResultado;
            END;

            -- =================================================================
            -- PASO B: Verificar autorizacion (el usuario es dueno de la entidad)
            -- Se verifica que el LoteId/FincaId referenciado pertenece al usuario
            -- =================================================================
            -- La verificacion especifica se hace dentro de cada handler,
            -- pero hay una verificacion general de UsuarioId activo aqui.
            IF NOT EXISTS (
                SELECT 1 FROM Usuarios WHERE Id = @UsuarioId AND Activo = 1 AND IsDeleted = 0
            )
            BEGIN
                SET @EstadoOp   = 'RECHAZADA';
                SET @CodigoError = 'USUARIO_INACTIVO';
                SET @MensajeError = 'El usuario no esta activo en el sistema.';
                GOTO RegistrarResultado;
            END;

            -- =================================================================
            -- PASO C: Despacho por tipo de operacion
            -- =================================================================

            -- ---- CREAR_APLICACION ----------------------------------------
            IF @Tipo = 'CREAR_APLICACION'
            BEGIN
                BEGIN TRANSACTION;

                -- Verificar que la entidad no existe ya (si existe, tratar como DUPLICADA)
                IF EXISTS (SELECT 1 FROM Aplicaciones WHERE ClientId = @EntidadId)
                BEGIN
                    IF @@TRANCOUNT > 0 ROLLBACK;

                    SELECT @RowVersionNuevo = CAST(RowVersion AS VARBINARY(8))
                    FROM Aplicaciones WHERE ClientId = @EntidadId;

                    SET @EstadoOp = 'DUPLICADA';
                    GOTO RegistrarResultado;
                END;

                -- Extraer campos del payload JSON
                DECLARE
                    @App_LoteId             UNIQUEIDENTIFIER,
                    @App_InsumoId           UNIQUEIDENTIFIER,
                    @App_FechaAplicacion    DATETIME2(7),
                    @App_DosisCantidad      DECIMAL(10,4),
                    @App_DosisUnidad        NVARCHAR(10),
                    @App_AreaHa             DECIMAL(10,4),
                    @App_Metodo             NVARCHAR(20),
                    @App_Operador           NVARCHAR(200),
                    @App_Costo              DECIMAL(12,2),
                    @App_Obs               NVARCHAR(1000),
                    @App_GpsLat             DECIMAL(10,7),
                    @App_GpsLon             DECIMAL(10,7),
                    @App_DiasCarencia       INT,
                    @App_Cultivo            NVARCHAR(100);

                SET @App_LoteId          = TRY_CAST(JSON_VALUE(@Payload, '$.loteId') AS UNIQUEIDENTIFIER);
                SET @App_InsumoId        = TRY_CAST(JSON_VALUE(@Payload, '$.insumoId') AS UNIQUEIDENTIFIER);
                SET @App_FechaAplicacion = TRY_CAST(JSON_VALUE(@Payload, '$.fechaAplicacion') AS DATETIME2(7));
                SET @App_DosisCantidad   = TRY_CAST(JSON_VALUE(@Payload, '$.dosis.cantidad') AS DECIMAL(10,4));
                SET @App_DosisUnidad     = JSON_VALUE(@Payload, '$.dosis.unidad');
                SET @App_AreaHa          = TRY_CAST(JSON_VALUE(@Payload, '$.areaAplicadaHa') AS DECIMAL(10,4));
                SET @App_Metodo          = JSON_VALUE(@Payload, '$.metodoAplicacion');
                SET @App_Operador        = JSON_VALUE(@Payload, '$.operadorNombre');
                SET @App_Costo           = ISNULL(TRY_CAST(JSON_VALUE(@Payload, '$.costoTotal') AS DECIMAL(12,2)), 0);
                SET @App_Obs             = JSON_VALUE(@Payload, '$.observaciones');
                SET @App_GpsLat          = TRY_CAST(JSON_VALUE(@Payload, '$.coordenadasGps.latitud') AS DECIMAL(10,7));
                SET @App_GpsLon          = TRY_CAST(JSON_VALUE(@Payload, '$.coordenadasGps.longitud') AS DECIMAL(10,7));

                -- Validar campos requeridos
                IF @App_LoteId IS NULL OR @App_InsumoId IS NULL OR @App_FechaAplicacion IS NULL
                   OR @App_DosisCantidad IS NULL OR @App_AreaHa IS NULL OR @App_Metodo IS NULL
                   OR @App_Operador IS NULL
                BEGIN
                    IF @@TRANCOUNT > 0 ROLLBACK;
                    SET @EstadoOp    = 'RECHAZADA';
                    SET @CodigoError = 'PAYLOAD_INVALIDO';
                    SET @MensajeError = 'Faltan campos requeridos en el payload de CREAR_APLICACION.';
                    GOTO RegistrarResultado;
                END;

                -- Verificar que el lote pertenece al usuario
                IF NOT EXISTS (
                    SELECT 1 FROM Lotes l
                    INNER JOIN Fincas f ON l.FincaId = f.Id
                    WHERE l.Id = @App_LoteId AND f.UsuarioId = @UsuarioId
                      AND l.IsDeleted = 0 AND f.IsDeleted = 0
                )
                BEGIN
                    IF @@TRANCOUNT > 0 ROLLBACK;
                    SET @EstadoOp    = 'RECHAZADA';
                    SET @CodigoError = 'LOTE_NO_AUTORIZADO';
                    SET @MensajeError = 'El lote no existe o no pertenece al usuario autenticado.';
                    GOTO RegistrarResultado;
                END;

                -- Verificar que el insumo existe
                IF NOT EXISTS (
                    SELECT 1 FROM Insumos WHERE Id = @App_InsumoId AND IsDeleted = 0 AND Activo = 1
                )
                BEGIN
                    IF @@TRANCOUNT > 0 ROLLBACK;
                    SET @EstadoOp    = 'RECHAZADA';
                    SET @CodigoError = 'INSUMO_NO_ENCONTRADO';
                    SET @MensajeError = 'El insumo especificado no existe o no esta activo en el catalogo.';
                    GOTO RegistrarResultado;
                END;

                -- Obtener cultivo del lote y dias de carencia del insumo para ese cultivo
                SELECT @App_Cultivo = Cultivo FROM Lotes WHERE Id = @App_LoteId;

                SELECT @App_DiasCarencia = pc.DiasCarencia
                FROM PeriodosCarencia pc
                WHERE pc.InsumoId = @App_InsumoId
                  AND pc.Cultivo  = @App_Cultivo
                  AND pc.IsDeleted = 0;

                -- Si no hay periodo especifico para ese cultivo, usar el maximo disponible del insumo
                IF @App_DiasCarencia IS NULL
                BEGIN
                    SELECT @App_DiasCarencia = MAX(DiasCarencia)
                    FROM PeriodosCarencia
                    WHERE InsumoId = @App_InsumoId AND IsDeleted = 0;
                END;

                -- Si aun no hay datos de carencia, error de configuracion
                IF @App_DiasCarencia IS NULL
                BEGIN
                    IF @@TRANCOUNT > 0 ROLLBACK;
                    SET @EstadoOp    = 'RECHAZADA';
                    SET @CodigoError = 'PERIODO_CARENCIA_NO_CONFIGURADO';
                    SET @MensajeError = 'El insumo no tiene periodo de carencia configurado. Contacte al administrador.';
                    GOTO RegistrarResultado;
                END;

                -- Insertar la aplicacion
                INSERT INTO Aplicaciones (
                    Id, ClientId, LoteId, InsumoId,
                    FechaAplicacion, DosisCantidad, DosisUnidad,
                    AreaAplicadaHa, MetodoAplicacion, OperadorNombre,
                    GpsLatitud, GpsLongitud, Observaciones, CostoTotal,
                    DiasCarenciaAplicables,
                    FechaFinCarencia,
                    EstadoAplicacion, OrigenOffline,
                    ClientTimestamp, DeviceId
                )
                VALUES (
                    NEWSEQUENTIALID(), @EntidadId, @App_LoteId, @App_InsumoId,
                    @App_FechaAplicacion, @App_DosisCantidad, @App_DosisUnidad,
                    @App_AreaHa, @App_Metodo, @App_Operador,
                    @App_GpsLat, @App_GpsLon, @App_Obs, @App_Costo,
                    @App_DiasCarencia,
                    DATEADD(DAY, @App_DiasCarencia, @App_FechaAplicacion),
                    'ACTIVA', 1,
                    @ClientTimestamp, @DeviceId
                );

                -- Obtener el RowVersion generado
                SELECT @RowVersionNuevo = CAST(RowVersion AS VARBINARY(8))
                FROM Aplicaciones WHERE ClientId = @EntidadId;

                -- Insertar/actualizar BloqueoCarencia para el lote
                MERGE BloqueosCarencia AS target
                USING (
                    SELECT
                        @App_LoteId AS LoteId,
                        (SELECT Id FROM Aplicaciones WHERE ClientId = @EntidadId) AS AplicacionId,
                        @App_InsumoId AS InsumoId,
                        (SELECT NombreComercial FROM Insumos WHERE Id = @App_InsumoId) AS InsumoNombre,
                        @App_FechaAplicacion AS FechaAplicacion,
                        DATEADD(DAY, @App_DiasCarencia, @App_FechaAplicacion) AS FechaFinCarencia
                ) AS source (LoteId, AplicacionId, InsumoId, InsumoNombre, FechaAplicacion, FechaFinCarencia)
                ON target.LoteId = source.LoteId AND target.AplicacionId = source.AplicacionId
                WHEN NOT MATCHED THEN
                    INSERT (LoteId, AplicacionId, InsumoId, InsumoNombre, FechaAplicacion, FechaFinCarencia, Activo)
                    VALUES (source.LoteId, source.AplicacionId, source.InsumoId, source.InsumoNombre,
                            source.FechaAplicacion, source.FechaFinCarencia, 1);

                -- Si hay costo, registrar en CostosLote automaticamente
                IF @App_Costo > 0
                BEGIN
                    INSERT INTO CostosLote (
                        Id, ClientId, LoteId, Fecha, Categoria, Descripcion,
                        Monto, AplicacionId, OrigenOffline, ClientTimestamp, DeviceId
                    )
                    VALUES (
                        NEWSEQUENTIALID(), NEWID(), @App_LoteId,
                        CAST(@App_FechaAplicacion AS DATE),
                        'INSUMOS_QUIMICOS',
                        'Aplicacion de ' + (SELECT NombreComercial FROM Insumos WHERE Id = @App_InsumoId),
                        @App_Costo,
                        (SELECT Id FROM Aplicaciones WHERE ClientId = @EntidadId),
                        1, @ClientTimestamp, @DeviceId
                    );
                END;

                COMMIT TRANSACTION;
                SET @EstadoOp = 'APLICADA';
            END

            -- ---- CREAR_COSECHA -------------------------------------------
            ELSE IF @Tipo = 'CREAR_COSECHA'
            BEGIN
                BEGIN TRANSACTION;

                -- Verificar idempotencia por ClientId
                IF EXISTS (SELECT 1 FROM Cosechas WHERE ClientId = @EntidadId)
                BEGIN
                    IF @@TRANCOUNT > 0 ROLLBACK;
                    SELECT @RowVersionNuevo = CAST(RowVersion AS VARBINARY(8))
                    FROM Cosechas WHERE ClientId = @EntidadId;
                    SET @EstadoOp = 'DUPLICADA';
                    GOTO RegistrarResultado;
                END;

                DECLARE
                    @Cos_LoteId         UNIQUEIDENTIFIER,
                    @Cos_FechaCosecha   DATETIME2(7),
                    @Cos_PesoKg         DECIMAL(12,4),
                    @Cos_Calidad        NVARCHAR(10),
                    @Cos_Comprador      NVARCHAR(200),
                    @Cos_PrecioKg       DECIMAL(10,4);

                SET @Cos_LoteId       = TRY_CAST(JSON_VALUE(@Payload, '$.loteId') AS UNIQUEIDENTIFIER);
                SET @Cos_FechaCosecha = TRY_CAST(JSON_VALUE(@Payload, '$.fechaCosecha') AS DATETIME2(7));
                SET @Cos_PesoKg       = TRY_CAST(JSON_VALUE(@Payload, '$.pesoTotalKg') AS DECIMAL(12,4));
                SET @Cos_Calidad      = JSON_VALUE(@Payload, '$.calidadGrado');
                SET @Cos_Comprador    = JSON_VALUE(@Payload, '$.comprador');
                SET @Cos_PrecioKg     = TRY_CAST(JSON_VALUE(@Payload, '$.precioVentaKg') AS DECIMAL(10,4));

                -- Validar lote pertenece al usuario
                IF NOT EXISTS (
                    SELECT 1 FROM Lotes l
                    INNER JOIN Fincas f ON l.FincaId = f.Id
                    WHERE l.Id = @Cos_LoteId AND f.UsuarioId = @UsuarioId
                      AND l.IsDeleted = 0
                )
                BEGIN
                    IF @@TRANCOUNT > 0 ROLLBACK;
                    SET @EstadoOp    = 'RECHAZADA';
                    SET @CodigoError = 'LOTE_NO_AUTORIZADO';
                    SET @MensajeError = 'El lote no existe o no pertenece al usuario autenticado.';
                    GOTO RegistrarResultado;
                END;

                -- REGLA CRITICA: Validar periodo de carencia
                EXEC sp_ValidarPeriodoCarencia
                    @LoteId              = @Cos_LoteId,
                    @FechaCosecha        = @Cos_FechaCosecha,
                    @InsumoId            = NULL,
                    @PuedeCortar         = @PuedeCortar OUTPUT,
                    @DiasRestantes       = @DiasRestantes OUTPUT,
                    @UltimaAplicacion    = @UltimaAplicacion OUTPUT,
                    @InsumoNombreBloqueo = @InsumoNombreBloqueo OUTPUT,
                    @FechaFinCarenciaOut = @FechaFinCarenciaOut OUTPUT;

                IF @PuedeCortar = 0
                BEGIN
                    IF @@TRANCOUNT > 0 ROLLBACK;
                    SET @EstadoOp    = 'RECHAZADA';
                    SET @CodigoError = 'PERIODO_CARENCIA_ACTIVO';
                    SET @MensajeError = 'No se puede registrar cosecha. Periodo de carencia activo de '
                        + ISNULL(@InsumoNombreBloqueo, 'insumo desconocido')
                        + ' hasta '
                        + CONVERT(NVARCHAR(20), @FechaFinCarenciaOut, 120)
                        + '. Dias restantes: ' + CAST(@DiasRestantes AS NVARCHAR(10)) + '.';
                    GOTO RegistrarResultado;
                END;

                -- Insertar cosecha
                INSERT INTO Cosechas (
                    Id, ClientId, LoteId, FechaCosecha, PesoTotalKg,
                    CalidadGrado, Comprador, PrecioVentaKg, IngresoTotal,
                    BloqueadaPorCarencia, OrigenOffline, ClientTimestamp, DeviceId
                )
                VALUES (
                    NEWSEQUENTIALID(), @EntidadId, @Cos_LoteId, @Cos_FechaCosecha, @Cos_PesoKg,
                    @Cos_Calidad, @Cos_Comprador, @Cos_PrecioKg,
                    CASE WHEN @Cos_PrecioKg IS NOT NULL THEN @Cos_PesoKg * @Cos_PrecioKg ELSE NULL END,
                    0, 1, @ClientTimestamp, @DeviceId
                );

                SELECT @RowVersionNuevo = CAST(RowVersion AS VARBINARY(8))
                FROM Cosechas WHERE ClientId = @EntidadId;

                -- Registrar IngresoLote si tiene precio
                IF @Cos_PrecioKg IS NOT NULL AND @Cos_PrecioKg > 0
                BEGIN
                    INSERT INTO IngresosLote (
                        Id, ClientId, LoteId, CosechaId, Fecha, Comprador,
                        KgVendidos, PrecioKg, Moneda
                    )
                    VALUES (
                        NEWSEQUENTIALID(), NEWID(), @Cos_LoteId,
                        (SELECT Id FROM Cosechas WHERE ClientId = @EntidadId),
                        CAST(@Cos_FechaCosecha AS DATE),
                        @Cos_Comprador, @Cos_PesoKg, @Cos_PrecioKg, 'USD'
                    );
                END;

                COMMIT TRANSACTION;
                SET @EstadoOp = 'APLICADA';
            END

            -- ---- CREAR_COSTO ---------------------------------------------
            ELSE IF @Tipo = 'CREAR_COSTO'
            BEGIN
                BEGIN TRANSACTION;

                IF EXISTS (SELECT 1 FROM CostosLote WHERE ClientId = @EntidadId)
                BEGIN
                    IF @@TRANCOUNT > 0 ROLLBACK;
                    SELECT @RowVersionNuevo = CAST(RowVersion AS VARBINARY(8))
                    FROM CostosLote WHERE ClientId = @EntidadId;
                    SET @EstadoOp = 'DUPLICADA';
                    GOTO RegistrarResultado;
                END;

                DECLARE
                    @Costo_LoteId   UNIQUEIDENTIFIER,
                    @Costo_Fecha    DATE,
                    @Costo_Cat      NVARCHAR(30),
                    @Costo_Desc     NVARCHAR(500),
                    @Costo_Monto    DECIMAL(12,2);

                SET @Costo_LoteId  = TRY_CAST(JSON_VALUE(@Payload, '$.loteId') AS UNIQUEIDENTIFIER);
                SET @Costo_Fecha   = TRY_CAST(JSON_VALUE(@Payload, '$.fecha') AS DATE);
                SET @Costo_Cat     = JSON_VALUE(@Payload, '$.categoria');
                SET @Costo_Desc    = JSON_VALUE(@Payload, '$.descripcion');
                SET @Costo_Monto   = TRY_CAST(JSON_VALUE(@Payload, '$.monto') AS DECIMAL(12,2));

                IF NOT EXISTS (
                    SELECT 1 FROM Lotes l
                    INNER JOIN Fincas f ON l.FincaId = f.Id
                    WHERE l.Id = @Costo_LoteId AND f.UsuarioId = @UsuarioId AND l.IsDeleted = 0
                )
                BEGIN
                    IF @@TRANCOUNT > 0 ROLLBACK;
                    SET @EstadoOp    = 'RECHAZADA';
                    SET @CodigoError = 'LOTE_NO_AUTORIZADO';
                    SET @MensajeError = 'El lote no existe o no pertenece al usuario.';
                    GOTO RegistrarResultado;
                END;

                INSERT INTO CostosLote (
                    Id, ClientId, LoteId, Fecha, Categoria, Descripcion,
                    Monto, OrigenOffline, ClientTimestamp, DeviceId
                )
                VALUES (
                    NEWSEQUENTIALID(), @EntidadId, @Costo_LoteId,
                    ISNULL(@Costo_Fecha, CAST(SYSUTCDATETIME() AS DATE)),
                    @Costo_Cat, @Costo_Desc, @Costo_Monto,
                    1, @ClientTimestamp, @DeviceId
                );

                SELECT @RowVersionNuevo = CAST(RowVersion AS VARBINARY(8))
                FROM CostosLote WHERE ClientId = @EntidadId;

                COMMIT TRANSACTION;
                SET @EstadoOp = 'APLICADA';
            END

            -- ---- ACTUALIZAR_COSTO (con resolucion LWW) -------------------
            ELSE IF @Tipo = 'ACTUALIZAR_COSTO'
            BEGIN
                BEGIN TRANSACTION;

                -- Obtener estado actual del registro
                SELECT
                    @RowVersionActual = CAST(RowVersion AS VARBINARY(8)),
                    @UpdatedAt        = LastModified
                FROM CostosLote
                WHERE ClientId = @EntidadId AND IsDeleted = 0;

                IF @RowVersionActual IS NULL
                BEGIN
                    IF @@TRANCOUNT > 0 ROLLBACK;
                    SET @EstadoOp    = 'RECHAZADA';
                    SET @CodigoError = 'ENTIDAD_NO_ENCONTRADA';
                    SET @MensajeError = 'El costo a actualizar no existe en el servidor.';
                    GOTO RegistrarResultado;
                END;

                -- Verificar RowVersion
                IF @RowVersionAnterior IS NOT NULL AND @RowVersionActual != @RowVersionAnterior
                BEGIN
                    -- CONFLICTO detectado. Para CostoLote se aplica LWW.
                    -- Si ClientTimestamp > UpdatedAt: el cliente gana, aplicar.
                    -- Si ClientTimestamp <= UpdatedAt: servidor gana, retornar CONFLICTO.
                    IF @ClientTimestamp > @UpdatedAt
                    BEGIN
                        -- Cliente gana: aplicar con RowVersion actual del servidor
                        -- (No se inserta en ConflictosSync porque se resuelve automaticamente)
                        UPDATE CostosLote
                        SET
                            Monto       = ISNULL(TRY_CAST(JSON_VALUE(@Payload, '$.monto') AS DECIMAL(12,2)), Monto),
                            Categoria   = ISNULL(JSON_VALUE(@Payload, '$.categoria'), Categoria),
                            Descripcion = ISNULL(JSON_VALUE(@Payload, '$.descripcion'), Descripcion),
                            LastModified = SYSUTCDATETIME()
                        WHERE ClientId = @EntidadId AND IsDeleted = 0;

                        SELECT @RowVersionNuevo = CAST(RowVersion AS VARBINARY(8))
                        FROM CostosLote WHERE ClientId = @EntidadId;

                        COMMIT TRANSACTION;
                        SET @EstadoOp = 'APLICADA';
                    END
                    ELSE
                    BEGIN
                        -- Servidor gana: retornar CONFLICTO con datos actuales
                        IF @@TRANCOUNT > 0 ROLLBACK;

                        SELECT @DatosServidor = (
                            SELECT
                                c.Monto, c.Categoria, c.Descripcion,
                                CONVERT(NVARCHAR(30), c.LastModified, 126) AS updatedAt,
                                CONVERT(NVARCHAR(30), CAST(c.RowVersion AS BINARY(8)), 1) AS rowVersion
                            FROM CostosLote c
                            WHERE c.ClientId = @EntidadId
                            FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
                        );

                        -- Registrar el conflicto para auditoria
                        INSERT INTO ConflictosSync (
                            OperacionId, EntidadId, EntidadTipo,
                            DatosCliente, DatosServidor,
                            RowVersionCliente, RowVersionServidor
                        )
                        VALUES (
                            @OperacionId, @EntidadId, @EntidadTipo,
                            @Payload, @DatosServidor,
                            @RowVersionAnterior, @RowVersionActual
                        );

                        SET @EstadoOp    = 'CONFLICTO';
                        SET @CodigoError = 'ROWVERSION_MISMATCH';
                        SET @MensajeError = 'La entidad fue modificada por otro dispositivo. Servidor gana (LWW).';
                    END;
                END
                ELSE
                BEGIN
                    -- RowVersion coincide: aplicar normalmente
                    UPDATE CostosLote
                    SET
                        Monto       = ISNULL(TRY_CAST(JSON_VALUE(@Payload, '$.monto') AS DECIMAL(12,2)), Monto),
                        Categoria   = ISNULL(JSON_VALUE(@Payload, '$.categoria'), Categoria),
                        Descripcion = ISNULL(JSON_VALUE(@Payload, '$.descripcion'), Descripcion),
                        LastModified = SYSUTCDATETIME()
                    WHERE ClientId = @EntidadId AND IsDeleted = 0;

                    SELECT @RowVersionNuevo = CAST(RowVersion AS VARBINARY(8))
                    FROM CostosLote WHERE ClientId = @EntidadId;

                    COMMIT TRANSACTION;
                    SET @EstadoOp = 'APLICADA';
                END;
            END

            -- ---- ELIMINAR_COSTO (soft delete) ----------------------------
            ELSE IF @Tipo = 'ELIMINAR_COSTO'
            BEGIN
                BEGIN TRANSACTION;

                -- Verificar que existe y que no tiene AplicacionId (invariante dominio)
                DECLARE @Costo_AplicacionId UNIQUEIDENTIFIER;
                SELECT
                    @RowVersionActual       = CAST(RowVersion AS VARBINARY(8)),
                    @Costo_AplicacionId     = AplicacionId
                FROM CostosLote
                WHERE ClientId = @EntidadId AND IsDeleted = 0;

                IF @RowVersionActual IS NULL
                BEGIN
                    IF @@TRANCOUNT > 0 ROLLBACK;
                    SET @EstadoOp = 'DUPLICADA';  -- Ya eliminado
                    GOTO RegistrarResultado;
                END;

                -- Un costo vinculado a una aplicacion no puede eliminarse sin anular la aplicacion
                IF @Costo_AplicacionId IS NOT NULL
                BEGIN
                    IF @@TRANCOUNT > 0 ROLLBACK;
                    SET @EstadoOp    = 'RECHAZADA';
                    SET @CodigoError = 'COSTO_VINCULADO_APLICACION';
                    SET @MensajeError = 'Este costo fue generado por una aplicacion quimica y no puede eliminarse directamente. Anule la aplicacion.';
                    GOTO RegistrarResultado;
                END;

                UPDATE CostosLote
                SET IsDeleted = 1, LastModified = SYSUTCDATETIME()
                WHERE ClientId = @EntidadId;

                SELECT @RowVersionNuevo = CAST(RowVersion AS VARBINARY(8))
                FROM CostosLote WHERE ClientId = @EntidadId;

                COMMIT TRANSACTION;
                SET @EstadoOp = 'APLICADA';
            END

            -- ---- CREAR_LOTE o ACTUALIZAR_LOTE ----------------------------
            ELSE IF @Tipo IN ('CREAR_LOTE', 'ACTUALIZAR_LOTE')
            BEGIN
                BEGIN TRANSACTION;

                IF @Tipo = 'CREAR_LOTE'
                BEGIN
                    IF EXISTS (SELECT 1 FROM Lotes WHERE ClientId = @EntidadId)
                    BEGIN
                        IF @@TRANCOUNT > 0 ROLLBACK;
                        SELECT @RowVersionNuevo = CAST(RowVersion AS VARBINARY(8))
                        FROM Lotes WHERE ClientId = @EntidadId;
                        SET @EstadoOp = 'DUPLICADA';
                        GOTO RegistrarResultado;
                    END;

                    DECLARE
                        @Lote_FincaId   UNIQUEIDENTIFIER,
                        @Lote_Nombre    NVARCHAR(200),
                        @Lote_Cultivo   NVARCHAR(100),
                        @Lote_AreaHa    DECIMAL(10,4);

                    SET @Lote_FincaId  = TRY_CAST(JSON_VALUE(@Payload, '$.fincaId') AS UNIQUEIDENTIFIER);
                    SET @Lote_Nombre   = JSON_VALUE(@Payload, '$.nombre');
                    SET @Lote_Cultivo  = JSON_VALUE(@Payload, '$.cultivo');
                    SET @Lote_AreaHa   = TRY_CAST(JSON_VALUE(@Payload, '$.areaHa') AS DECIMAL(10,4));

                    IF NOT EXISTS (
                        SELECT 1 FROM Fincas WHERE Id = @Lote_FincaId AND UsuarioId = @UsuarioId AND IsDeleted = 0
                    )
                    BEGIN
                        IF @@TRANCOUNT > 0 ROLLBACK;
                        SET @EstadoOp    = 'RECHAZADA';
                        SET @CodigoError = 'FINCA_NO_AUTORIZADA';
                        SET @MensajeError = 'La finca no existe o no pertenece al usuario.';
                        GOTO RegistrarResultado;
                    END;

                    INSERT INTO Lotes (Id, ClientId, FincaId, Nombre, Cultivo, AreaHa, Activo)
                    VALUES (NEWSEQUENTIALID(), @EntidadId, @Lote_FincaId, @Lote_Nombre, @Lote_Cultivo, @Lote_AreaHa, 1);

                    SELECT @RowVersionNuevo = CAST(RowVersion AS VARBINARY(8))
                    FROM Lotes WHERE ClientId = @EntidadId;
                END
                ELSE -- ACTUALIZAR_LOTE con LWW
                BEGIN
                    SELECT
                        @RowVersionActual = CAST(RowVersion AS VARBINARY(8)),
                        @UpdatedAt        = LastModified
                    FROM Lotes WHERE ClientId = @EntidadId AND IsDeleted = 0;

                    IF @RowVersionActual IS NULL
                    BEGIN
                        IF @@TRANCOUNT > 0 ROLLBACK;
                        SET @EstadoOp    = 'RECHAZADA';
                        SET @CodigoError = 'ENTIDAD_NO_ENCONTRADA';
                        SET @MensajeError = 'El lote a actualizar no existe.';
                        GOTO RegistrarResultado;
                    END;

                    IF @RowVersionAnterior IS NOT NULL AND @RowVersionActual != @RowVersionAnterior
                       AND @ClientTimestamp <= @UpdatedAt
                    BEGIN
                        IF @@TRANCOUNT > 0 ROLLBACK;
                        SELECT @DatosServidor = (
                            SELECT Nombre, Cultivo, AreaHa,
                                   CONVERT(NVARCHAR(30), LastModified, 126) AS updatedAt
                            FROM Lotes WHERE ClientId = @EntidadId
                            FOR JSON PATH, WITHOUT_ARRAY_WRAPPER
                        );
                        INSERT INTO ConflictosSync (OperacionId, EntidadId, EntidadTipo, DatosCliente, DatosServidor, RowVersionCliente, RowVersionServidor)
                        VALUES (@OperacionId, @EntidadId, @EntidadTipo, @Payload, @DatosServidor, @RowVersionAnterior, @RowVersionActual);
                        SET @EstadoOp    = 'CONFLICTO';
                        SET @CodigoError = 'ROWVERSION_MISMATCH';
                        SET @MensajeError = 'Conflicto detectado. Servidor gana (LWW).';
                        GOTO RegistrarResultado;
                    END;

                    UPDATE Lotes
                    SET
                        Nombre       = ISNULL(JSON_VALUE(@Payload, '$.nombre'), Nombre),
                        Cultivo      = ISNULL(JSON_VALUE(@Payload, '$.cultivo'), Cultivo),
                        AreaHa       = ISNULL(TRY_CAST(JSON_VALUE(@Payload, '$.areaHa') AS DECIMAL(10,4)), AreaHa),
                        LastModified = SYSUTCDATETIME()
                    WHERE ClientId = @EntidadId AND IsDeleted = 0;

                    SELECT @RowVersionNuevo = CAST(RowVersion AS VARBINARY(8))
                    FROM Lotes WHERE ClientId = @EntidadId;
                END;

                COMMIT TRANSACTION;
                SET @EstadoOp = 'APLICADA';
            END

            ELSE
            BEGIN
                -- Tipo de operacion no reconocido
                SET @EstadoOp    = 'RECHAZADA';
                SET @CodigoError = 'TIPO_OPERACION_DESCONOCIDO';
                SET @MensajeError = 'El tipo de operacion "' + @Tipo + '" no esta soportado.';
            END;

        END TRY
        BEGIN CATCH
            IF @@TRANCOUNT > 0 ROLLBACK;
            SET @EstadoOp    = 'ERROR';
            SET @CodigoError = 'ERROR_INTERNO';
            SET @MensajeError = LEFT(ERROR_MESSAGE(), 2000);
        END CATCH;

        -- =====================================================================
        -- RegistrarResultado: Registrar en log y acumular resultado
        -- =====================================================================
        RegistrarResultado:

        -- Registrar en el log de idempotencia (solo para estados finales, no EN_PROCESO)
        IF @EstadoOp NOT IN ('PENDIENTE', 'EN_PROCESO')
        BEGIN
            BEGIN TRY
                INSERT INTO SyncOperacionesLog (
                    OperacionId, DeviceId, UsuarioId, Tipo,
                    EntidadId, EntidadTipo, Estado, RowVersionResultante
                )
                VALUES (
                    @OperacionId, @DeviceId, @UsuarioId, @Tipo,
                    @EntidadId, @EntidadTipo, @EstadoOp, @RowVersionNuevo
                );
            END TRY
            BEGIN CATCH
                -- Si ya existe (race condition en multi-instancia), ignorar
                IF ERROR_NUMBER() != 2627  -- Duplicate key
                    THROW;
            END CATCH;
        END;

        -- Acumular resultado para el response
        INSERT INTO @Resultados (OperacionId, Estado, EntidadId, RowVersionNuevo, CodigoError, MensajeError, DatosServidor)
        VALUES (@OperacionId, ISNULL(@EstadoOp, 'ERROR'), @EntidadId, @RowVersionNuevo, @CodigoError, @MensajeError, @DatosServidor);

        SET @OrdenProceso = @OrdenProceso + 1;
    END; -- FIN WHILE

    -- Retornar resultados al caller
    SELECT
        OperacionId,
        Estado,
        EntidadId,
        RowVersionNuevo,
        CodigoError,
        MensajeError,
        DatosServidor
    FROM @Resultados
    ORDER BY (SELECT NULL);  -- Orden de insercion (ya es orden de procesamiento)

END;
GO


-- =============================================================================
-- SP 3: sp_GetRentabilidadLote
--
-- PROPOSITO:
--   Calcula la rentabilidad completa de un lote en un periodo dado.
--   Implementa la formula definida en bounded-contexts.md (seccion Costos).
--   Provee datos para el endpoint GET /api/lotes/{id}/rentabilidad.
--
-- FORMULA:
--   TotalIngresos = SUM(IngresoLote.TotalVenta) WHERE LoteId AND Fecha IN periodo
--   TotalCostos   = SUM(CostosLote.Monto) WHERE LoteId AND Fecha IN periodo AND !IsDeleted
--   UtilidadBruta = TotalIngresos - TotalCostos
--   MargenBruto   = (UtilidadBruta / TotalIngresos) * 100
--   UtilidadPorHa = UtilidadBruta / Lote.AreaHa
--   TotalKg       = SUM(Cosecha.PesoTotalKg) WHERE LoteId AND FechaCosecha IN periodo
--   CostoPorKg    = TotalCostos / TotalKg
--   UtilidadPorKg = UtilidadBruta / TotalKg
--   ROI           = (UtilidadBruta / TotalCostos) * 100
--
-- INPUTS:
--   @LoteId   -- Lote a analizar
--   @Desde    -- Inicio del periodo (DATE)
--   @Hasta    -- Fin del periodo (DATE, inclusive)
--
-- OUTPUTS:
--   Result set 1: Metricas de rentabilidad
--   Result set 2: Desglose de costos por categoria
--   Result set 3: Detalle de ventas/ingresos
--   Result set 4: Alertas activas (periodos de carencia)
--
-- PERFORMANCE:
--   Usa IX_CostosLote_Rentabilidad, IX_IngresosLote_Rentabilidad,
--   IX_Cosechas_Rentabilidad para seeks directos.
--   Tiempo esperado: < 500ms con 1M filas en tablas de costos.
-- =============================================================================

CREATE OR ALTER PROCEDURE sp_GetRentabilidadLote
    @LoteId     UNIQUEIDENTIFIER,
    @Desde      DATE,
    @Hasta      DATE
AS
BEGIN
    SET NOCOUNT ON;

    -- Validar que el periodo es coherente
    IF @Desde > @Hasta
    BEGIN
        RAISERROR('La fecha de inicio no puede ser posterior a la fecha de fin.', 16, 1);
        RETURN;
    END;

    -- Verificar que el lote existe
    IF NOT EXISTS (SELECT 1 FROM Lotes WHERE Id = @LoteId AND IsDeleted = 0)
    BEGIN
        RAISERROR('El lote especificado no existe o ha sido eliminado.', 16, 1);
        RETURN;
    END;

    -- =========================================================================
    -- Variables de calculo
    -- =========================================================================
    DECLARE
        @AreaHa             DECIMAL(10,4),
        @Cultivo            NVARCHAR(100),
        @LoteNombre         NVARCHAR(200),
        @TotalIngresos      DECIMAL(14,2),
        @TotalCostos        DECIMAL(14,2),
        @UtilidadBruta      DECIMAL(14,2),
        @MargenBruto        DECIMAL(8,4),
        @UtilidadPorHa      DECIMAL(14,2),
        @TotalKg            DECIMAL(14,4),
        @CostoPorKg         DECIMAL(10,4),
        @UtilidadPorKg      DECIMAL(10,4),
        @ROI                DECIMAL(8,4),
        @PrecioPromedioKg   DECIMAL(10,4);

    -- Datos del lote
    SELECT
        @AreaHa      = l.AreaHa,
        @Cultivo     = l.Cultivo,
        @LoteNombre  = l.Nombre
    FROM Lotes l
    WHERE l.Id = @LoteId AND l.IsDeleted = 0;

    -- Total de ingresos en el periodo
    SELECT @TotalIngresos = ISNULL(SUM(il.KgVendidos * il.PrecioKg), 0)
    FROM IngresosLote il
    WHERE il.LoteId   = @LoteId
      AND il.Fecha BETWEEN @Desde AND @Hasta
      AND il.IsDeleted = 0;

    -- Total de costos en el periodo (excluye soft-deleted)
    SELECT @TotalCostos = ISNULL(SUM(cl.Monto), 0)
    FROM CostosLote cl
    WHERE cl.LoteId   = @LoteId
      AND cl.Fecha  BETWEEN @Desde AND @Hasta
      AND cl.IsDeleted = 0;

    -- Total de kilogramos cosechados en el periodo
    SELECT
        @TotalKg           = ISNULL(SUM(c.PesoTotalKg), 0),
        @PrecioPromedioKg  = CASE
            WHEN SUM(c.PesoTotalKg) > 0
            THEN SUM(c.IngresoTotal) / NULLIF(SUM(c.PesoTotalKg), 0)
            ELSE NULL
        END
    FROM Cosechas c
    WHERE c.LoteId        = @LoteId
      AND CAST(c.FechaCosecha AS DATE) BETWEEN @Desde AND @Hasta
      AND c.IsDeleted     = 0
      AND c.BloqueadaPorCarencia = 0;

    -- Calculos derivados
    SET @UtilidadBruta = @TotalIngresos - @TotalCostos;

    SET @MargenBruto = CASE
        WHEN @TotalIngresos > 0
        THEN ROUND((@UtilidadBruta / @TotalIngresos) * 100, 4)
        ELSE NULL
    END;

    SET @UtilidadPorHa = CASE
        WHEN @AreaHa > 0
        THEN ROUND(@UtilidadBruta / @AreaHa, 2)
        ELSE NULL
    END;

    SET @CostoPorKg = CASE
        WHEN @TotalKg > 0
        THEN ROUND(@TotalCostos / @TotalKg, 4)
        ELSE NULL
    END;

    SET @UtilidadPorKg = CASE
        WHEN @TotalKg > 0
        THEN ROUND(@UtilidadBruta / @TotalKg, 4)
        ELSE NULL
    END;

    SET @ROI = CASE
        WHEN @TotalCostos > 0
        THEN ROUND((@UtilidadBruta / @TotalCostos) * 100, 4)
        ELSE NULL
    END;

    -- =========================================================================
    -- RESULT SET 1: Metricas de rentabilidad
    -- =========================================================================
    SELECT
        @LoteId         AS LoteId,
        @LoteNombre     AS LoteNombre,
        @Cultivo        AS CultivoActual,
        @AreaHa         AS AreaHa,
        @Desde          AS PeriodoDesde,
        @Hasta          AS PeriodoHasta,
        -- Ingresos
        @TotalIngresos  AS TotalVentas,
        @PrecioPromedioKg AS PrecioPromedioKg,
        @TotalKg        AS TotalKgCosechados,
        -- Costos
        @TotalCostos    AS TotalCostos,
        CASE WHEN @AreaHa > 0 THEN ROUND(@TotalCostos / @AreaHa, 2) ELSE NULL END AS CostoPorHa,
        @CostoPorKg     AS CostoPorKg,
        -- Rentabilidad
        @UtilidadBruta  AS UtilidadBruta,
        @MargenBruto    AS MargenBruto,
        @UtilidadPorHa  AS UtilidadPorHa,
        @UtilidadPorKg  AS UtilidadPorKg,
        @ROI            AS ROI;

    -- =========================================================================
    -- RESULT SET 2: Desglose de costos por categoria
    -- =========================================================================
    SELECT
        cl.Categoria,
        SUM(cl.Monto)                                           AS TotalCategoria,
        COUNT(*)                                                AS NumeroRegistros,
        CASE
            WHEN @TotalCostos > 0
            THEN ROUND((SUM(cl.Monto) / @TotalCostos) * 100, 2)
            ELSE 0
        END                                                     AS PorcentajeDelTotal
    FROM CostosLote cl
    WHERE cl.LoteId    = @LoteId
      AND cl.Fecha   BETWEEN @Desde AND @Hasta
      AND cl.IsDeleted = 0
    GROUP BY cl.Categoria
    ORDER BY TotalCategoria DESC;

    -- =========================================================================
    -- RESULT SET 3: Detalle de ventas/ingresos
    -- =========================================================================
    SELECT
        il.Fecha,
        il.Comprador,
        il.KgVendidos,
        il.PrecioKg,
        il.KgVendidos * il.PrecioKg     AS TotalVenta,
        il.Moneda
    FROM IngresosLote il
    WHERE il.LoteId    = @LoteId
      AND il.Fecha   BETWEEN @Desde AND @Hasta
      AND il.IsDeleted = 0
    ORDER BY il.Fecha ASC;

    -- =========================================================================
    -- RESULT SET 4: Alertas activas en el lote (periodos de carencia vigentes)
    -- =========================================================================
    SELECT
        'PERIODO_CARENCIA'                                              AS TipoAlerta,
        bc.InsumoNombre                                                 AS Insumo,
        bc.FechaAplicacion,
        bc.FechaFinCarencia,
        DATEDIFF(DAY, CAST(SYSUTCDATETIME() AS DATE),
                 CAST(bc.FechaFinCarencia AS DATE))                     AS DiasRestantes,
        'CRITICA'                                                       AS Severidad,
        'Cosecha bloqueada hasta ' +
            CONVERT(NVARCHAR(10), CAST(bc.FechaFinCarencia AS DATE), 120) +
            ' por aplicacion de ' + bc.InsumoNombre                    AS Mensaje
    FROM BloqueosCarencia bc
    WHERE bc.LoteId  = @LoteId
      AND bc.Activo  = 1
      AND bc.FechaFinCarencia > SYSUTCDATETIME()
    ORDER BY bc.FechaFinCarencia ASC;

END;
GO


-- =============================================================================
-- SP AUXILIAR: sp_PurgarDatosAntiguos
-- Ejecutar como job nocturno (02:00 AM UTC-5 Ecuador).
-- Purga datos de corta vida para mantener performance de indices.
-- =============================================================================

CREATE OR ALTER PROCEDURE sp_PurgarDatosAntiguos
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @FechaCorte90Dias   DATETIME2(7) = DATEADD(DAY, -90, SYSUTCDATETIME());
    DECLARE @FechaCorte24Horas  DATETIME2(7) = DATEADD(HOUR, -24, SYSUTCDATETIME());
    DECLARE @Ahora              DATETIME2(7) = SYSUTCDATETIME();

    -- 1. Purgar SyncOperacionesLog > 90 dias
    DELETE FROM SyncOperacionesLog
    WHERE ProcesadoAt < @FechaCorte90Dias;

    -- 2. Purgar AuthChallenges expirados y ya usados
    DELETE FROM AuthChallenges
    WHERE ExpiresAt < @Ahora AND Usado = 1;

    -- 3. Purgar AuthIntentosFallidos > 24 horas
    DELETE FROM AuthIntentosFallidos
    WHERE IntentoAt < @FechaCorte24Horas;

    -- 4. Desactivar BloqueosCarencia expirados
    UPDATE BloqueosCarencia
    SET Activo       = 0,
        LastModified = @Ahora
    WHERE Activo     = 1
      AND FechaFinCarencia < @Ahora;

    -- 5. Purgar OperacionesSyncPendientes procesadas > 30 dias
    DELETE FROM OperacionesSyncPendientes
    WHERE Estado IN ('APLICADA', 'DUPLICADA', 'RECHAZADA')
      AND ProcesadoAt < DATEADD(DAY, -30, @Ahora);

END;
GO

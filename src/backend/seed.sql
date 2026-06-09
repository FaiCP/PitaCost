-- ============================================================
-- SEED DE PRUEBA — PitaSmart_Dev
-- Ejecutar en: (localdb)\MSSQLLocalDB › PitaSmart_Dev
-- ============================================================

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

USE PitaSmart_Dev;
GO

-- IDs fijos para reproducibilidad
DECLARE @usuarioId    UNIQUEIDENTIFIER = '00000001-0000-0000-0000-000000000001';
DECLARE @fincaId      UNIQUEIDENTIFIER = '00000002-0000-0000-0000-000000000001';
DECLARE @loteId1      UNIQUEIDENTIFIER = '00000003-0000-0000-0000-000000000001';
DECLARE @loteId2      UNIQUEIDENTIFIER = '00000003-0000-0000-0000-000000000002';
DECLARE @insumoId1    UNIQUEIDENTIFIER = '00000004-0000-0000-0000-000000000001'; -- Fungicida
DECLARE @insumoId2    UNIQUEIDENTIFIER = '00000004-0000-0000-0000-000000000002'; -- Insecticida
DECLARE @insumoId3    UNIQUEIDENTIFIER = '00000004-0000-0000-0000-000000000003'; -- Herbicida
DECLARE @now          DATETIMEOFFSET   = SYSDATETIMEOFFSET();

-- ============================================================
-- 1. USUARIO
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM Usuarios WHERE Id = @usuarioId)
INSERT INTO Usuarios (Id, Email, NombreCompleto, Cedula, Telefono, Rol, Activo, FechaRegistro, CreatedAt, UpdatedAt)
VALUES (
    @usuarioId,
    'agricultor@pitasmart.dev',
    'Carlos Andrade Moreno',
    '1712345678',
    '0991234567',
    'AGRICULTOR',
    1,
    @now,
    @now,
    @now
);

-- ============================================================
-- 2. FINCA
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM Fincas WHERE Id = @fincaId)
INSERT INTO Fincas (Id, UsuarioId, Nombre, Provincia, Canton, Parroquia, AreaTotalHa, CreatedAt, UpdatedAt)
VALUES (
    @fincaId,
    @usuarioId,
    'Finca El Progreso',
    'Los Ríos',
    'Quevedo',
    'San Camilo',
    45.5000,
    @now,
    @now
);

-- ============================================================
-- 3. LOTES
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM Lotes WHERE Id = @loteId1)
INSERT INTO Lotes (Id, FincaId, Nombre, Cultivo, AreaHa, Latitud, Longitud, FechaInicioSiembra, Activo, CreatedAt, UpdatedAt)
VALUES (
    @loteId1,
    @fincaId,
    'Lote A — Banano',
    'Banano',
    18.5000,
    -1.0245,
    -79.4654,
    '2024-08-01',
    1,
    @now,
    @now
);

IF NOT EXISTS (SELECT 1 FROM Lotes WHERE Id = @loteId2)
INSERT INTO Lotes (Id, FincaId, Nombre, Cultivo, AreaHa, Latitud, Longitud, FechaInicioSiembra, Activo, CreatedAt, UpdatedAt)
VALUES (
    @loteId2,
    @fincaId,
    'Lote B — Cacao',
    'Cacao',
    12.0000,
    -1.0312,
    -79.4721,
    '2024-06-15',
    1,
    @now,
    @now
);

-- ============================================================
-- 4. INSUMOS (catálogo agroquímicos Agrocalidad)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM Insumos WHERE Id = @insumoId1)
INSERT INTO Insumos (Id, NombreComercial, IngredienteActivo, Fabricante, RegistroAgrocalidad,
    TipoProducto, CategoriaToxico, ConcentracionValor, ConcentracionUnidad,
    DosisMinima, DosisMaxima, UnidadDosis, Activo, CreatedAt, UpdatedAt)
VALUES (
    @insumoId1,
    'Mancozeb 80 WP',
    'Mancozeb',
    'Dow AgroSciences',
    'AGROCALIDAD-2021-0145',
    'FUNGICIDA',
    'III',
    80.0000, '%',
    1.5000, 3.0000, 'KG_HA',
    1, @now, @now
);

IF NOT EXISTS (SELECT 1 FROM Insumos WHERE Id = @insumoId2)
INSERT INTO Insumos (Id, NombreComercial, IngredienteActivo, Fabricante, RegistroAgrocalidad,
    TipoProducto, CategoriaToxico, ConcentracionValor, ConcentracionUnidad,
    DosisMinima, DosisMaxima, UnidadDosis, Activo, CreatedAt, UpdatedAt)
VALUES (
    @insumoId2,
    'Clorpirifos 48 EC',
    'Clorpirifos',
    'Syngenta Ecuador',
    'AGROCALIDAD-2020-0089',
    'INSECTICIDA',
    'II',
    48.0000, '%',
    0.5000, 1.5000, 'L_HA',
    1, @now, @now
);

IF NOT EXISTS (SELECT 1 FROM Insumos WHERE Id = @insumoId3)
INSERT INTO Insumos (Id, NombreComercial, IngredienteActivo, Fabricante, RegistroAgrocalidad,
    TipoProducto, CategoriaToxico, ConcentracionValor, ConcentracionUnidad,
    DosisMinima, DosisMaxima, UnidadDosis, Activo, CreatedAt, UpdatedAt)
VALUES (
    @insumoId3,
    'Glifosato 48 SL',
    'Glifosato',
    'Bayer CropScience',
    'AGROCALIDAD-2019-0231',
    'HERBICIDA',
    'IV',
    48.0000, '%',
    2.0000, 4.0000, 'L_HA',
    1, @now, @now
);

-- ============================================================
-- 5. PERIODOS DE CARENCIA (días antes de cosecha)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM PeriodosCarencia WHERE InsumoId = @insumoId1 AND Cultivo = 'Banano')
INSERT INTO PeriodosCarencia (Id, InsumoId, Cultivo, DiasCarencia, FuenteRegulacion, CreatedAt, UpdatedAt)
VALUES (NEWID(), @insumoId1, 'Banano', 14, 'Resolución Agrocalidad 048-2021', @now, @now);

IF NOT EXISTS (SELECT 1 FROM PeriodosCarencia WHERE InsumoId = @insumoId1 AND Cultivo = 'Cacao')
INSERT INTO PeriodosCarencia (Id, InsumoId, Cultivo, DiasCarencia, FuenteRegulacion, CreatedAt, UpdatedAt)
VALUES (NEWID(), @insumoId1, 'Cacao', 21, 'Resolución Agrocalidad 048-2021', @now, @now);

IF NOT EXISTS (SELECT 1 FROM PeriodosCarencia WHERE InsumoId = @insumoId2 AND Cultivo = 'Banano')
INSERT INTO PeriodosCarencia (Id, InsumoId, Cultivo, DiasCarencia, FuenteRegulacion, CreatedAt, UpdatedAt)
VALUES (NEWID(), @insumoId2, 'Banano', 30, 'Resolución Agrocalidad 031-2020', @now, @now);

IF NOT EXISTS (SELECT 1 FROM PeriodosCarencia WHERE InsumoId = @insumoId3 AND Cultivo = 'Banano')
INSERT INTO PeriodosCarencia (Id, InsumoId, Cultivo, DiasCarencia, FuenteRegulacion, CreatedAt, UpdatedAt)
VALUES (NEWID(), @insumoId3, 'Banano', 7, 'Resolución Agrocalidad 057-2019', @now, @now);

-- ============================================================
-- 6. APLICACIONES DE QUIMICOS (pasadas, sin carencia activa)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM Aplicaciones WHERE Id = '00000005-0000-0000-0000-000000000001')
INSERT INTO Aplicaciones (Id, LoteId, InsumoId, FechaAplicacion,
    DosisCantidad, DosisUnidad, AreaAplicadaHa, MetodoAplicacion, OperadorNombre,
    GpsLatitud, GpsLongitud, Observaciones, CostoTotal,
    DiasCarenciaAplicables, FechaFinCarencia,
    CreadoOffline, ClientTimestamp, CreatedAt, UpdatedAt)
VALUES (
    '00000005-0000-0000-0000-000000000001',
    @loteId1, @insumoId1,
    DATEADD(day, -60, @now),
    2.0000, 'KG_HA', 18.5000, 'FUMIGACION', 'Pedro Quiñonez',
    -1.0245, -79.4654,
    'Aplicación preventiva contra sigatoka',
    185.00,
    14, DATEADD(day, -46, @now),
    0, DATEADD(day, -60, @now), DATEADD(day, -60, @now), DATEADD(day, -60, @now)
);

IF NOT EXISTS (SELECT 1 FROM Aplicaciones WHERE Id = '00000005-0000-0000-0000-000000000002')
INSERT INTO Aplicaciones (Id, LoteId, InsumoId, FechaAplicacion,
    DosisCantidad, DosisUnidad, AreaAplicadaHa, MetodoAplicacion, OperadorNombre,
    GpsLatitud, GpsLongitud, Observaciones, CostoTotal,
    DiasCarenciaAplicables, FechaFinCarencia,
    CreadoOffline, ClientTimestamp, CreatedAt, UpdatedAt)
VALUES (
    '00000005-0000-0000-0000-000000000002',
    @loteId1, @insumoId2,
    DATEADD(day, -45, @now),
    1.0000, 'L_HA', 10.0000, 'FUMIGACION', 'Pedro Quiñonez',
    -1.0245, -79.4654,
    'Control de trips en racimos',
    220.00,
    30, DATEADD(day, -15, @now),
    0, DATEADD(day, -45, @now), DATEADD(day, -45, @now), DATEADD(day, -45, @now)
);

IF NOT EXISTS (SELECT 1 FROM Aplicaciones WHERE Id = '00000005-0000-0000-0000-000000000003')
INSERT INTO Aplicaciones (Id, LoteId, InsumoId, FechaAplicacion,
    DosisCantidad, DosisUnidad, AreaAplicadaHa, MetodoAplicacion, OperadorNombre,
    GpsLatitud, GpsLongitud, Observaciones, CostoTotal,
    DiasCarenciaAplicables, FechaFinCarencia,
    CreadoOffline, ClientTimestamp, CreatedAt, UpdatedAt)
VALUES (
    '00000005-0000-0000-0000-000000000003',
    @loteId2, @insumoId1,
    DATEADD(day, -30, @now),
    2.5000, 'KG_HA', 12.0000, 'FUMIGACION', 'Luis Mero',
    -1.0312, -79.4721,
    'Tratamiento monilia en mazorcas',
    300.00,
    21, DATEADD(day, -9, @now),
    0, DATEADD(day, -30, @now), DATEADD(day, -30, @now), DATEADD(day, -30, @now)
);

-- ============================================================
-- 7. COSECHAS
-- ============================================================
DECLARE @cosechaId1 UNIQUEIDENTIFIER = '00000006-0000-0000-0000-000000000001';
DECLARE @cosechaId2 UNIQUEIDENTIFIER = '00000006-0000-0000-0000-000000000002';
DECLARE @cosechaId3 UNIQUEIDENTIFIER = '00000006-0000-0000-0000-000000000003';

IF NOT EXISTS (SELECT 1 FROM Cosechas WHERE Id = @cosechaId1)
INSERT INTO Cosechas (Id, LoteId, FechaCosecha, PesoTotalKg, CalidadGrado, Comprador,
    PrecioVentaKg, IngresoTotal, Observaciones, BloqueadaPorCarencia,
    CreadoOffline, ClientTimestamp, CreatedAt, UpdatedAt)
VALUES (
    @cosechaId1, @loteId1,
    DATEADD(day, -50, @now),
    12500.0000, 'PRIMERA', 'Exportadora Bananera del Pacífico',
    0.1850, 2312.50,
    'Caja exportación, calibre 22-24', 0,
    0, DATEADD(day, -50, @now), DATEADD(day, -50, @now), DATEADD(day, -50, @now)
);

IF NOT EXISTS (SELECT 1 FROM Cosechas WHERE Id = @cosechaId2)
INSERT INTO Cosechas (Id, LoteId, FechaCosecha, PesoTotalKg, CalidadGrado, Comprador,
    PrecioVentaKg, IngresoTotal, Observaciones, BloqueadaPorCarencia,
    CreadoOffline, ClientTimestamp, CreatedAt, UpdatedAt)
VALUES (
    @cosechaId2, @loteId1,
    DATEADD(day, -10, @now),
    14200.0000, 'PRIMERA', 'Exportadora Bananera del Pacífico',
    0.1900, 2698.00,
    'Muy buen calibre post-tratamiento', 0,
    0, DATEADD(day, -10, @now), DATEADD(day, -10, @now), DATEADD(day, -10, @now)
);

IF NOT EXISTS (SELECT 1 FROM Cosechas WHERE Id = @cosechaId3)
INSERT INTO Cosechas (Id, LoteId, FechaCosecha, PesoTotalKg, CalidadGrado, Comprador,
    PrecioVentaKg, IngresoTotal, Observaciones, BloqueadaPorCarencia,
    CreadoOffline, ClientTimestamp, CreatedAt, UpdatedAt)
VALUES (
    @cosechaId3, @loteId2,
    DATEADD(day, -5, @now),
    3800.0000, 'PRIMERA', 'Chocolatería Nacional',
    2.5000, 9500.00,
    'Fermentación 6 días, secado natural', 0,
    0, DATEADD(day, -5, @now), DATEADD(day, -5, @now), DATEADD(day, -5, @now)
);

-- ============================================================
-- 8. INGRESOS LOTE (ventas registradas)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM IngresosLote WHERE CosechaId = @cosechaId1)
INSERT INTO IngresosLote (Id, LoteId, CosechaId, Fecha, Comprador, KgVendidos, PrecioKg, TotalVenta, CreatedAt, UpdatedAt)
VALUES (NEWID(), @loteId1, @cosechaId1, CAST(DATEADD(day, -50, @now) AS DATE),
    'Exportadora Bananera del Pacífico', 12500.0000, 0.1850, 2312.50, @now, @now);

IF NOT EXISTS (SELECT 1 FROM IngresosLote WHERE CosechaId = @cosechaId2)
INSERT INTO IngresosLote (Id, LoteId, CosechaId, Fecha, Comprador, KgVendidos, PrecioKg, TotalVenta, CreatedAt, UpdatedAt)
VALUES (NEWID(), @loteId1, @cosechaId2, CAST(DATEADD(day, -10, @now) AS DATE),
    'Exportadora Bananera del Pacífico', 14200.0000, 0.1900, 2698.00, @now, @now);

IF NOT EXISTS (SELECT 1 FROM IngresosLote WHERE CosechaId = @cosechaId3)
INSERT INTO IngresosLote (Id, LoteId, CosechaId, Fecha, Comprador, KgVendidos, PrecioKg, TotalVenta, CreatedAt, UpdatedAt)
VALUES (NEWID(), @loteId2, @cosechaId3, CAST(DATEADD(day, -5, @now) AS DATE),
    'Chocolatería Nacional', 3800.0000, 2.5000, 9500.00, @now, @now);

-- ============================================================
-- 9. COSTOS LOTE
-- ============================================================
-- Insumos químicos (vinculados a aplicaciones)
IF NOT EXISTS (SELECT 1 FROM CostosLote WHERE AplicacionId = '00000005-0000-0000-0000-000000000001')
INSERT INTO CostosLote (Id, LoteId, Fecha, Categoria, Descripcion, Monto,
    AplicacionId, CosechaId, CreadoOffline, ClientTimestamp, Eliminado, CreatedAt, UpdatedAt)
VALUES (NEWID(), @loteId1, CAST(DATEADD(day, -60, @now) AS DATE),
    'INSUMOS_QUIMICOS', 'Mancozeb 80 WP — fumigación sigatoka', 185.00,
    '00000005-0000-0000-0000-000000000001', NULL, 0, DATEADD(day,-60,@now), 0, @now, @now);

IF NOT EXISTS (SELECT 1 FROM CostosLote WHERE AplicacionId = '00000005-0000-0000-0000-000000000002')
INSERT INTO CostosLote (Id, LoteId, Fecha, Categoria, Descripcion, Monto,
    AplicacionId, CosechaId, CreadoOffline, ClientTimestamp, Eliminado, CreatedAt, UpdatedAt)
VALUES (NEWID(), @loteId1, CAST(DATEADD(day, -45, @now) AS DATE),
    'INSUMOS_QUIMICOS', 'Clorpirifos 48 EC — control trips', 220.00,
    '00000005-0000-0000-0000-000000000002', NULL, 0, DATEADD(day,-45,@now), 0, @now, @now);

IF NOT EXISTS (SELECT 1 FROM CostosLote WHERE AplicacionId = '00000005-0000-0000-0000-000000000003')
INSERT INTO CostosLote (Id, LoteId, Fecha, Categoria, Descripcion, Monto,
    AplicacionId, CosechaId, CreadoOffline, ClientTimestamp, Eliminado, CreatedAt, UpdatedAt)
VALUES (NEWID(), @loteId2, CAST(DATEADD(day, -30, @now) AS DATE),
    'INSUMOS_QUIMICOS', 'Mancozeb 80 WP — tratamiento monilia', 300.00,
    '00000005-0000-0000-0000-000000000003', NULL, 0, DATEADD(day,-30,@now), 0, @now, @now);

-- Mano de obra
INSERT INTO CostosLote (Id, LoteId, Fecha, Categoria, Descripcion, Monto,
    AplicacionId, CosechaId, CreadoOffline, ClientTimestamp, Eliminado, CreatedAt, UpdatedAt)
VALUES (NEWID(), @loteId1, CAST(DATEADD(day, -55, @now) AS DATE),
    'MANO_DE_OBRA', 'Jornales cosecha — 8 trabajadores', 320.00,
    NULL, NULL, 0, DATEADD(day,-55,@now), 0, @now, @now);

INSERT INTO CostosLote (Id, LoteId, Fecha, Categoria, Descripcion, Monto,
    AplicacionId, CosechaId, CreadoOffline, ClientTimestamp, Eliminado, CreatedAt, UpdatedAt)
VALUES (NEWID(), @loteId1, CAST(DATEADD(day, -12, @now) AS DATE),
    'MANO_DE_OBRA', 'Jornales cosecha — 10 trabajadores', 400.00,
    NULL, NULL, 0, DATEADD(day,-12,@now), 0, @now, @now);

INSERT INTO CostosLote (Id, LoteId, Fecha, Categoria, Descripcion, Monto,
    AplicacionId, CosechaId, CreadoOffline, ClientTimestamp, Eliminado, CreatedAt, UpdatedAt)
VALUES (NEWID(), @loteId2, CAST(DATEADD(day, -8, @now) AS DATE),
    'MANO_DE_OBRA', 'Cosecha y desgrane cacao', 480.00,
    NULL, NULL, 0, DATEADD(day,-8,@now), 0, @now, @now);

-- Transporte
INSERT INTO CostosLote (Id, LoteId, Fecha, Categoria, Descripcion, Monto,
    AplicacionId, CosechaId, CreadoOffline, ClientTimestamp, Eliminado, CreatedAt, UpdatedAt)
VALUES (NEWID(), @loteId1, CAST(DATEADD(day, -50, @now) AS DATE),
    'TRANSPORTE', 'Flete a empacadora — 2 viajes', 150.00,
    NULL, NULL, 0, DATEADD(day,-50,@now), 0, @now, @now);

INSERT INTO CostosLote (Id, LoteId, Fecha, Categoria, Descripcion, Monto,
    AplicacionId, CosechaId, CreadoOffline, ClientTimestamp, Eliminado, CreatedAt, UpdatedAt)
VALUES (NEWID(), @loteId2, CAST(DATEADD(day, -5, @now) AS DATE),
    'TRANSPORTE', 'Flete a centro de acopio cacao', 90.00,
    NULL, NULL, 0, DATEADD(day,-5,@now), 0, @now, @now);

-- Riego
INSERT INTO CostosLote (Id, LoteId, Fecha, Categoria, Descripcion, Monto,
    AplicacionId, CosechaId, CreadoOffline, ClientTimestamp, Eliminado, CreatedAt, UpdatedAt)
VALUES (NEWID(), @loteId1, CAST(DATEADD(day, -30, @now) AS DATE),
    'RIEGO', 'Riego por aspersión — 3 semanas', 200.00,
    NULL, NULL, 0, DATEADD(day,-30,@now), 0, @now, @now);

-- ============================================================
-- 10. PRECIOS DE MERCADO
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM PreciosMercado WHERE Cultivo = 'Banano' AND Vigente = 1)
INSERT INTO PreciosMercado (Id, Cultivo, PrecioKg, Fuente, FechaPublicacion, Vigente, CreatedAt, UpdatedAt)
VALUES (NEWID(), 'Banano', 0.1900, 'Ministerio de Agricultura Ecuador', CAST(GETDATE() AS DATE), 1, @now, @now);

IF NOT EXISTS (SELECT 1 FROM PreciosMercado WHERE Cultivo = 'Cacao' AND Vigente = 1)
INSERT INTO PreciosMercado (Id, Cultivo, PrecioKg, Fuente, FechaPublicacion, Vigente, CreatedAt, UpdatedAt)
VALUES (NEWID(), 'Cacao', 2.5000, 'Bolsa de Productos del Ecuador — BOLPROS', CAST(GETDATE() AS DATE), 1, @now, @now);

GO

-- ============================================================
-- VERIFICACIÓN
-- ============================================================
SELECT 'Usuarios'        AS Tabla, COUNT(*) AS Registros FROM Usuarios       UNION ALL
SELECT 'Fincas',                   COUNT(*)              FROM Fincas          UNION ALL
SELECT 'Lotes',                    COUNT(*)              FROM Lotes           UNION ALL
SELECT 'Insumos',                  COUNT(*)              FROM Insumos         UNION ALL
SELECT 'PeriodosCarencia',         COUNT(*)              FROM PeriodosCarencia UNION ALL
SELECT 'Aplicaciones',             COUNT(*)              FROM Aplicaciones    UNION ALL
SELECT 'Cosechas',                 COUNT(*)              FROM Cosechas        UNION ALL
SELECT 'IngresosLote',             COUNT(*)              FROM IngresosLote    UNION ALL
SELECT 'CostosLote',               COUNT(*)              FROM CostosLote      UNION ALL
SELECT 'PreciosMercado',           COUNT(*)              FROM PreciosMercado;
GO

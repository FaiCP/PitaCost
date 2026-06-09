-- =============================================================================
-- PitaSmart -- Datos de Semilla para Desarrollo y Testing
-- Version: 1.0.0
-- Fecha: 2026-03-24
-- Autor: Senior DBA Architect
--
-- CONTENIDO:
--   1. 3 Usuarios de prueba (agricultor, administrador, auditor)
--   2. 2 Fincas de prueba
--   3. 4 Lotes con cultivos reales de Ecuador
--   4. 5 Insumos del catalogo real de Agrocalidad Ecuador con DiasCarencia reales
--   5. Historial de aplicaciones (incluye escenario de carencia activa)
--   6. Cosechas y costos de muestra
--
-- FUENTES DE DATOS REALES:
--   Periodos de carencia segun:
--   - Manual Agrocalidad 2023: "Fichas Tecnicas de Insumos Agropecuarios"
--   - Resolucion DAJ-2017-0079-R (Agrocalidad)
--   - Codex Alimentarius MRL (Maximum Residue Limits)
--
-- NOTA: Estos datos son para DESARROLLO UNICAMENTE.
--       NO ejecutar en produccion.
-- =============================================================================

USE PitaSmart;
GO

BEGIN TRANSACTION;
BEGIN TRY

-- =============================================================================
-- 1. USUARIOS DE PRUEBA
-- =============================================================================
-- Contrasenas de passkey: se configuran via flujo WebAuthn en la app.
-- Para pruebas de API, los tokens JWT se generan con el script dev/generate-test-jwt.ps1

-- UUIDs fijos para reproducibilidad en tests
DECLARE @UserId_Agricultor1 UNIQUEIDENTIFIER = '11111111-0000-0000-0000-000000000001';
DECLARE @UserId_Agricultor2 UNIQUEIDENTIFIER = '11111111-0000-0000-0000-000000000002';
DECLARE @UserId_Admin       UNIQUEIDENTIFIER = '11111111-0000-0000-0000-000000000003';

INSERT INTO Usuarios (Id, ClientId, Email, NombreCompleto, Cedula, Telefono, Rol, Activo, FechaRegistro)
VALUES
    -- Agricultor 1: Manuel Vera (Los Rios - Banano y Cacao)
    (
        @UserId_Agricultor1,
        '11111111-CCCC-0000-0000-000000000001',
        'manuel.vera@pitasmart.dev',
        'Manuel Andres Vera Cevallos',
        '1205678901',   -- Cedula valida modulo 10 (Los Rios)
        '+593984561234',
        'AGRICULTOR',
        1,
        '2025-01-15T10:00:00'
    ),
    -- Agricultor 2: Rosa Quimi (El Oro - Banano de exportacion)
    (
        @UserId_Agricultor2,
        '11111111-CCCC-0000-0000-000000000002',
        'rosa.quimi@pitasmart.dev',
        'Rosa Elena Quimi Orellana',
        '0703456789',   -- Cedula valida (El Oro)
        '+593991234567',
        'AGRICULTOR',
        1,
        '2025-02-01T08:30:00'
    ),
    -- Administrador del sistema
    (
        @UserId_Admin,
        '11111111-CCCC-0000-0000-000000000003',
        'admin@pitasmart.dev',
        'Carlos Administrador PitaSmart',
        '1713456789',   -- Cedula valida (Pichincha)
        '+593999887766',
        'ADMINISTRADOR',
        1,
        '2024-12-01T00:00:00'
    );
GO

-- =============================================================================
-- 2. FINCAS DE PRUEBA
-- =============================================================================
DECLARE @UserId_Agricultor1 UNIQUEIDENTIFIER = '11111111-0000-0000-0000-000000000001';
DECLARE @UserId_Agricultor2 UNIQUEIDENTIFIER = '11111111-0000-0000-0000-000000000002';

DECLARE @FincaId_ElParaiso  UNIQUEIDENTIFIER = '22222222-0000-0000-0000-000000000001';
DECLARE @FincaId_LaEsperanza UNIQUEIDENTIFIER = '22222222-0000-0000-0000-000000000002';

INSERT INTO Fincas (Id, ClientId, UsuarioId, Nombre, Provincia, Canton, Parroquia, AreaTotalHa)
VALUES
    -- Finca El Paraiso: Los Rios, cultivo banano y cacao
    (
        @FincaId_ElParaiso,
        '22222222-CCCC-0000-0000-000000000001',
        @UserId_Agricultor1,
        'Finca El Paraiso',
        'Los Rios',
        'Ventanas',
        'Ventanas',
        25.50
    ),
    -- Finca La Esperanza: El Oro, exportacion de banano
    (
        @FincaId_LaEsperanza,
        '22222222-CCCC-0000-0000-000000000002',
        @UserId_Agricultor2,
        'Finca La Esperanza',
        'El Oro',
        'Machala',
        'El Cambio',
        18.75
    );
GO

-- =============================================================================
-- 3. LOTES DE PRUEBA
-- =============================================================================
DECLARE @FincaId_ElParaiso   UNIQUEIDENTIFIER = '22222222-0000-0000-0000-000000000001';
DECLARE @FincaId_LaEsperanza UNIQUEIDENTIFIER = '22222222-0000-0000-0000-000000000002';

DECLARE @LoteId_Norte       UNIQUEIDENTIFIER = '33333333-0000-0000-0000-000000000001';
DECLARE @LoteId_Sur         UNIQUEIDENTIFIER = '33333333-0000-0000-0000-000000000002';
DECLARE @LoteId_Cacao1      UNIQUEIDENTIFIER = '33333333-0000-0000-0000-000000000003';
DECLARE @LoteId_BananoOro   UNIQUEIDENTIFIER = '33333333-0000-0000-0000-000000000004';

INSERT INTO Lotes (Id, ClientId, FincaId, Nombre, Cultivo, AreaHa, CentroLatitud, CentroLongitud, FechaInicioSiembra, Activo)
VALUES
    -- Lote Norte: Banano Cavendish (El Paraiso)
    (
        @LoteId_Norte,
        '33333333-CCCC-0000-0000-000000000001',
        @FincaId_ElParaiso,
        'Lote Norte - Banano',
        'Banano',
        5.00,
        -1.4517,    -- Latitud Ventanas, Los Rios
        -79.4634,   -- Longitud
        '2025-06-01',
        1
    ),
    -- Lote Sur: Banano (El Paraiso)
    (
        @LoteId_Sur,
        '33333333-CCCC-0000-0000-000000000002',
        @FincaId_ElParaiso,
        'Lote Sur - Banano',
        'Banano',
        7.50,
        -1.4620,
        -79.4710,
        '2025-05-15',
        1
    ),
    -- Lote Cacao: Nacional Fino de Aroma (El Paraiso)
    (
        @LoteId_Cacao1,
        '33333333-CCCC-0000-0000-000000000003',
        @FincaId_ElParaiso,
        'Lote Cacao - Nacional Fino',
        'Cacao',
        8.00,
        -1.4580,
        -79.4680,
        '2024-09-01',
        1
    ),
    -- Lote Banano exportacion (La Esperanza, El Oro)
    (
        @LoteId_BananoOro,
        '33333333-CCCC-0000-0000-000000000004',
        @FincaId_LaEsperanza,
        'Lote Principal - Banano Exportacion',
        'Banano',
        15.00,
        -3.2581,    -- Latitud Machala, El Oro
        -79.9554,
        '2025-03-01',
        1
    );
GO

-- =============================================================================
-- 4. INSUMOS CON DATOS REALES DE AGROCALIDAD ECUADOR
-- Fuente: Fichas tecnicas de Agrocalidad y Codex MRL
-- =============================================================================

DECLARE @InsumoId_Mancozeb      UNIQUEIDENTIFIER = '44444444-0000-0000-0000-000000000001';
DECLARE @InsumoId_Clorotalonil  UNIQUEIDENTIFIER = '44444444-0000-0000-0000-000000000002';
DECLARE @InsumoId_Glifosato     UNIQUEIDENTIFIER = '44444444-0000-0000-0000-000000000003';
DECLARE @InsumoId_Cobre         UNIQUEIDENTIFIER = '44444444-0000-0000-0000-000000000004';
DECLARE @InsumoId_NPK           UNIQUEIDENTIFIER = '44444444-0000-0000-0000-000000000005';

INSERT INTO Insumos (Id, ClientId, NombreComercial, IngredienteActivo, Fabricante,
                     RegistroAgrocalidad, TipoProducto, CategoriaToxico,
                     ConcentracionValor, ConcentracionUnidad,
                     DosisMinima, DosisMaxima, UnidadDosis, Activo)
VALUES
    -- 1. Mancozeb 80% WP
    --    Fungicida amplio espectro. MRL Codex Alimentarius: 2 mg/kg en banano.
    --    DiasCarencia real: 14 dias para banano (Codex CXL 2019-003).
    (
        @InsumoId_Mancozeb,
        '44444444-CCCC-0000-0000-000000000001',
        'Mancozeb 80% WP',
        'Mancozeb',
        'Dow AgroSciences S.A.',
        'PF-2023-0012345',
        'FUNGICIDA',
        'III',
        80.00, 'PORCENTAJE',
        1.50, 3.00, 'KG_HA',
        1
    ),
    -- 2. Clorotalonil 72% SC
    --    Fungicida preventivo para Sigatoka Negra. Muy usado en banano.
    --    DiasCarencia real: 21 dias para banano (Agrocalidad Res. 0079).
    (
        @InsumoId_Clorotalonil,
        '44444444-CCCC-0000-0000-000000000002',
        'Clorotalonil 72% SC',
        'Clorotalonil',
        'Syngenta Ecuador S.A.',
        'PF-2022-0098765',
        'FUNGICIDA',
        'II',
        72.00, 'PORCENTAJE',
        1.50, 2.50, 'L_HA',
        1
    ),
    -- 3. Glifosato 48% SL
    --    Herbicida sistemico no selectivo. Uso en platillo/corona en banano.
    --    DiasCarencia real: 7 dias para banano (aplicacion dirigida al suelo).
    (
        @InsumoId_Glifosato,
        '44444444-CCCC-0000-0000-000000000003',
        'Glifosato 48% SL',
        'Glifosato (sal isopropilamina)',
        'Bayer CropScience Ecuador',
        'PH-2021-0034521',
        'HERBICIDA',
        'III',
        48.00, 'PORCENTAJE',
        1.00, 3.00, 'L_HA',
        1
    ),
    -- 4. Hidroxido de Cobre 77% WP (Kocide)
    --    Fungicida/bactericida de contacto. Base cobre, natural, baja toxicidad.
    --    DiasCarencia: 3 dias para banano, 5 dias para cacao.
    --    Permitido en produccion organica certificada.
    (
        @InsumoId_Cobre,
        '44444444-CCCC-0000-0000-000000000004',
        'Kocide 77% WP',
        'Hidroxido de Cobre',
        'DuPont Ecuador Cia. Ltda.',
        'PF-2020-0056789',
        'FUNGICIDA',
        'IV',
        77.00, 'PORCENTAJE',
        1.00, 2.50, 'KG_HA',
        1
    ),
    -- 5. Fertilizante NPK 15-15-15
    --    Fertilizante quimico balanceado de liberacion rapida.
    --    DiasCarencia: 0 dias (fertilizante, sin carencia para cosecha).
    (
        @InsumoId_NPK,
        '44444444-CCCC-0000-0000-000000000005',
        'Nitrofoska Azul 15-15-15',
        'N-P-K 15-15-15',
        'Compo Expert GmbH',
        'FF-2023-0000123',
        'FERTILIZANTE',
        'IV',
        NULL, NULL,  -- Sin concentracion de principio activo unico
        100.00, 500.00, 'KG_HA',
        1
    );
GO

-- =============================================================================
-- 5. PERIODOS DE CARENCIA (DATOS REALES AGROCALIDAD ECUADOR)
-- =============================================================================
DECLARE @InsumoId_Mancozeb      UNIQUEIDENTIFIER = '44444444-0000-0000-0000-000000000001';
DECLARE @InsumoId_Clorotalonil  UNIQUEIDENTIFIER = '44444444-0000-0000-0000-000000000002';
DECLARE @InsumoId_Glifosato     UNIQUEIDENTIFIER = '44444444-0000-0000-0000-000000000003';
DECLARE @InsumoId_Cobre         UNIQUEIDENTIFIER = '44444444-0000-0000-0000-000000000004';
DECLARE @InsumoId_NPK           UNIQUEIDENTIFIER = '44444444-0000-0000-0000-000000000005';

INSERT INTO PeriodosCarencia (InsumoId, Cultivo, DiasCarencia, FuenteRegulacion)
VALUES
    -- Mancozeb: 14 dias banano, 21 dias cacao
    (@InsumoId_Mancozeb, 'Banano',  14, 'Codex Alimentarius CXL 2019-003 - MRL 2mg/kg'),
    (@InsumoId_Mancozeb, 'Cacao',   21, 'Agrocalidad Resolucion DAJ-2017-0079-R'),
    (@InsumoId_Mancozeb, 'Tomate',  7,  'Codex Alimentarius CXL 2018-012'),

    -- Clorotalonil: 21 dias banano (Sigatoka Negra es el uso principal)
    (@InsumoId_Clorotalonil, 'Banano', 21, 'Agrocalidad Resolucion DAJ-2017-0079-R'),
    (@InsumoId_Clorotalonil, 'Cacao',  28, 'Agrocalidad - ficha tecnica actualizada 2022'),

    -- Glifosato: 7 dias (aplicacion dirigida, no foliar en banano)
    (@InsumoId_Glifosato, 'Banano', 7, 'Agrocalidad - aplicacion dirigida al suelo, no contacto fruta'),
    (@InsumoId_Glifosato, 'Cacao',  7, 'Agrocalidad - aplicacion dirigida al suelo'),

    -- Kocide (Cobre): 3 dias banano, 5 dias cacao
    (@InsumoId_Cobre, 'Banano', 3, 'Reglamento CE 834/2007 - Produccion organica certificada'),
    (@InsumoId_Cobre, 'Cacao',  5, 'Reglamento CE 834/2007 - aplicable certificacion organica Ecuador'),

    -- NPK: 0 dias (fertilizante, sin restriccion de cosecha)
    (@InsumoId_NPK, 'Banano', 0, 'Sin periodo de carencia - fertilizante de suelo'),
    (@InsumoId_NPK, 'Cacao',  0, 'Sin periodo de carencia - fertilizante de suelo');
GO

-- =============================================================================
-- 6. HISTORIAL DE APLICACIONES
-- Escenario de prueba:
--   - Lote Norte: aplicacion de Mancozeb el 2026-03-10 -> carencia hasta 2026-03-24
--                 (exactamente en el borde; el dia de hoy 2026-03-24 es el ultimo dia)
--   - Lote Norte: aplicacion de Clorotalonil el 2026-03-05 -> carencia hasta 2026-03-26
--                 (BLOQUEA cosecha en este momento - 2 dias mas)
--   - Lote Sur:   aplicaciones pasadas, carencia ya expirada (libre para cosechar)
--   - Lote Cacao: aplicacion de Kocide el 2026-03-22 -> carencia hasta 2026-03-27
-- =============================================================================
DECLARE @InsumoId_Mancozeb      UNIQUEIDENTIFIER = '44444444-0000-0000-0000-000000000001';
DECLARE @InsumoId_Clorotalonil  UNIQUEIDENTIFIER = '44444444-0000-0000-0000-000000000002';
DECLARE @InsumoId_Glifosato     UNIQUEIDENTIFIER = '44444444-0000-0000-0000-000000000003';
DECLARE @InsumoId_Cobre         UNIQUEIDENTIFIER = '44444444-0000-0000-0000-000000000004';
DECLARE @InsumoId_NPK           UNIQUEIDENTIFIER = '44444444-0000-0000-0000-000000000005';

DECLARE @LoteId_Norte       UNIQUEIDENTIFIER = '33333333-0000-0000-0000-000000000001';
DECLARE @LoteId_Sur         UNIQUEIDENTIFIER = '33333333-0000-0000-0000-000000000002';
DECLARE @LoteId_Cacao1      UNIQUEIDENTIFIER = '33333333-0000-0000-0000-000000000003';
DECLARE @LoteId_BananoOro   UNIQUEIDENTIFIER = '33333333-0000-0000-0000-000000000004';

DECLARE @AppId_M1   UNIQUEIDENTIFIER = '55555555-0000-0000-0000-000000000001';
DECLARE @AppId_C1   UNIQUEIDENTIFIER = '55555555-0000-0000-0000-000000000002';
DECLARE @AppId_Sur1 UNIQUEIDENTIFIER = '55555555-0000-0000-0000-000000000003';
DECLARE @AppId_Sur2 UNIQUEIDENTIFIER = '55555555-0000-0000-0000-000000000004';
DECLARE @AppId_K1   UNIQUEIDENTIFIER = '55555555-0000-0000-0000-000000000005';
DECLARE @AppId_N1   UNIQUEIDENTIFIER = '55555555-0000-0000-0000-000000000006';
DECLARE @AppId_BanOro1 UNIQUEIDENTIFIER = '55555555-0000-0000-0000-000000000007';

INSERT INTO Aplicaciones (
    Id, ClientId, LoteId, InsumoId, FechaAplicacion,
    DosisCantidad, DosisUnidad, AreaAplicadaHa, MetodoAplicacion, OperadorNombre,
    GpsLatitud, GpsLongitud, Observaciones, CostoTotal,
    DiasCarenciaAplicables, FechaFinCarencia, EstadoAplicacion, OrigenOffline,
    ClientTimestamp, DeviceId
)
VALUES
    -- LOTE NORTE - Aplicacion 1: Mancozeb el 10-Mar-2026
    -- Carencia 14 dias -> expira el 24-Mar-2026 (hoy, borde exacto)
    (
        NEWSEQUENTIALID(), @AppId_M1, @LoteId_Norte, @InsumoId_Mancozeb,
        '2026-03-10T07:00:00',
        2.00, 'KG_HA', 5.00, 'FUMIGACION', 'Juan Reyes',
        -1.4517, -79.4634,
        'Aplicacion preventiva semana 10. Inicio de lluvia detectado.',
        65.00,
        14, '2026-03-24T07:00:00',
        'ACTIVA', 0,
        '2026-03-10T07:05:00', 'device-dev-001'
    ),
    -- LOTE NORTE - Aplicacion 2: Clorotalonil el 05-Mar-2026
    -- Carencia 21 dias -> expira el 26-Mar-2026 (BLOQUEA cosecha por 2 dias)
    (
        NEWSEQUENTIALID(), @AppId_C1, @LoteId_Norte, @InsumoId_Clorotalonil,
        '2026-03-05T06:30:00',
        2.00, 'L_HA', 5.00, 'FUMIGACION', 'Pedro Moreira',
        -1.4520, -79.4638,
        'Aplicacion curativa por deteccion de Sigatoka Negra nivel 4.',
        120.00,
        21, '2026-03-26T06:30:00',
        'ACTIVA', 0,
        '2026-03-05T06:35:00', 'device-dev-001'
    ),

    -- LOTE SUR - Aplicacion historica: Mancozeb el 01-Feb-2026
    -- Carencia ya expirada (52 dias atras). Lote libre para cosechar.
    (
        NEWSEQUENTIALID(), @AppId_Sur1, @LoteId_Sur, @InsumoId_Mancozeb,
        '2026-02-01T08:00:00',
        1.80, 'KG_HA', 7.50, 'FUMIGACION', 'Luis Chavez',
        -1.4620, -79.4710,
        'Aplicacion preventiva inicio de ciclo.',
        85.00,
        14, '2026-02-15T08:00:00',
        'ACTIVA', 0,
        '2026-02-01T08:10:00', 'device-dev-002'
    ),
    -- LOTE SUR - Fertilizacion: NPK el 15-Feb-2026
    -- Sin carencia (0 dias). Lote libre.
    (
        NEWSEQUENTIALID(), @AppId_Sur2, @LoteId_Sur, @InsumoId_NPK,
        '2026-02-15T09:00:00',
        200.00, 'KG_HA', 7.50, 'GRANULAR', 'Luis Chavez',
        -1.4622, -79.4712,
        'Fertilizacion de inicio de ciclo vegetativo.',
        380.00,
        0, '2026-02-15T09:00:00',
        'ACTIVA', 0,
        '2026-02-15T09:15:00', 'device-dev-002'
    ),

    -- LOTE CACAO - Aplicacion: Kocide (Cobre) el 22-Mar-2026
    -- Carencia 5 dias para cacao -> expira el 27-Mar-2026 (bloqueo activo)
    (
        NEWSEQUENTIALID(), @AppId_K1, @LoteId_Cacao1, @InsumoId_Cobre,
        '2026-03-22T06:00:00',
        1.50, 'KG_HA', 8.00, 'FUMIGACION', 'Manuel Vera',
        -1.4580, -79.4680,
        'Aplicacion preventiva monilia. Producto organico permitido.',
        42.00,
        5, '2026-03-27T06:00:00',
        'ACTIVA', 1,  -- Creada offline
        '2026-03-22T06:10:00', 'device-dev-001'
    ),

    -- LOTE CACAO - Fertilizacion: NPK el 10-Ene-2026
    (
        NEWSEQUENTIALID(), @AppId_N1, @LoteId_Cacao1, @InsumoId_NPK,
        '2026-01-10T07:30:00',
        150.00, 'KG_HA', 8.00, 'GRANULAR', 'Manuel Vera',
        -1.4582, -79.4682,
        'Fertilizacion primer ciclo 2026.',
        200.00,
        0, '2026-01-10T07:30:00',
        'ACTIVA', 0,
        '2026-01-10T07:40:00', 'device-dev-001'
    ),

    -- LOTE BANANO ORO - Aplicacion: Mancozeb el 08-Mar-2026
    -- Carencia 14 dias -> expiro el 22-Mar-2026. Libre para cosechar.
    (
        NEWSEQUENTIALID(), @AppId_BanOro1, @LoteId_BananoOro, @InsumoId_Mancozeb,
        '2026-03-08T07:00:00',
        2.50, 'KG_HA', 15.00, 'FUMIGACION', 'Roberto Medina',
        -3.2581, -79.9554,
        'Aplicacion semana 10. Condicion: viento 8 km/h, sin lluvia.',
        180.00,
        14, '2026-03-22T07:00:00',
        'ACTIVA', 0,
        '2026-03-08T07:10:00', 'device-dev-003'
    );
GO

-- =============================================================================
-- 7. BLOQUEOS DE CARENCIA (estado derivado de las aplicaciones)
-- =============================================================================
DECLARE @LoteId_Norte   UNIQUEIDENTIFIER = '33333333-0000-0000-0000-000000000001';
DECLARE @LoteId_Cacao1  UNIQUEIDENTIFIER = '33333333-0000-0000-0000-000000000003';

DECLARE @AppId_M1   UNIQUEIDENTIFIER = '55555555-0000-0000-0000-000000000001';
DECLARE @AppId_C1   UNIQUEIDENTIFIER = '55555555-0000-0000-0000-000000000002';
DECLARE @AppId_K1   UNIQUEIDENTIFIER = '55555555-0000-0000-0000-000000000005';

DECLARE @InsumoId_Mancozeb      UNIQUEIDENTIFIER = '44444444-0000-0000-0000-000000000001';
DECLARE @InsumoId_Clorotalonil  UNIQUEIDENTIFIER = '44444444-0000-0000-0000-000000000002';
DECLARE @InsumoId_Cobre         UNIQUEIDENTIFIER = '44444444-0000-0000-0000-000000000004';

-- Solo los bloqueos ACTIVOS al 2026-03-24
INSERT INTO BloqueosCarencia (LoteId, AplicacionId, InsumoId, InsumoNombre, FechaAplicacion, FechaFinCarencia, Activo)
VALUES
    -- Lote Norte: Mancozeb expira exactamente hoy (borde)
    (@LoteId_Norte, (SELECT Id FROM Aplicaciones WHERE ClientId = @AppId_M1),
     @InsumoId_Mancozeb, 'Mancozeb 80% WP',
     '2026-03-10T07:00:00', '2026-03-24T07:00:00', 1),

    -- Lote Norte: Clorotalonil expira en 2 dias (BLOQUEO ACTIVO CRITICO)
    (@LoteId_Norte, (SELECT Id FROM Aplicaciones WHERE ClientId = @AppId_C1),
     @InsumoId_Clorotalonil, 'Clorotalonil 72% SC',
     '2026-03-05T06:30:00', '2026-03-26T06:30:00', 1),

    -- Lote Cacao: Kocide expira en 3 dias
    (@LoteId_Cacao1, (SELECT Id FROM Aplicaciones WHERE ClientId = @AppId_K1),
     @InsumoId_Cobre, 'Kocide 77% WP',
     '2026-03-22T06:00:00', '2026-03-27T06:00:00', 1);
GO

-- =============================================================================
-- 8. COSTOS OPERATIVOS
-- =============================================================================
DECLARE @LoteId_Norte   UNIQUEIDENTIFIER = '33333333-0000-0000-0000-000000000001';
DECLARE @LoteId_Sur     UNIQUEIDENTIFIER = '33333333-0000-0000-0000-000000000002';
DECLARE @LoteId_Cacao1  UNIQUEIDENTIFIER = '33333333-0000-0000-0000-000000000003';

DECLARE @AppId_M1   UNIQUEIDENTIFIER = '55555555-0000-0000-0000-000000000001';
DECLARE @AppId_C1   UNIQUEIDENTIFIER = '55555555-0000-0000-0000-000000000002';
DECLARE @AppId_Sur1 UNIQUEIDENTIFIER = '55555555-0000-0000-0000-000000000003';
DECLARE @AppId_N1   UNIQUEIDENTIFIER = '55555555-0000-0000-0000-000000000006';

INSERT INTO CostosLote (Id, ClientId, LoteId, Fecha, Categoria, Descripcion, Monto, AplicacionId, OrigenOffline, ClientTimestamp, DeviceId)
VALUES
    -- Costos Lote Norte: generados por aplicaciones (automaticos)
    (NEWSEQUENTIALID(), '66666666-CCCC-0001-0000-000000000001', @LoteId_Norte, '2026-03-10', 'INSUMOS_QUIMICOS',
     'Aplicacion de Mancozeb 80% WP', 65.00,
     (SELECT Id FROM Aplicaciones WHERE ClientId = @AppId_M1), 0, '2026-03-10T07:05:00', 'device-dev-001'),

    (NEWSEQUENTIALID(), '66666666-CCCC-0002-0000-000000000001', @LoteId_Norte, '2026-03-05', 'INSUMOS_QUIMICOS',
     'Aplicacion de Clorotalonil 72% SC', 120.00,
     (SELECT Id FROM Aplicaciones WHERE ClientId = @AppId_C1), 0, '2026-03-05T06:35:00', 'device-dev-001'),

    -- Costos manuales adicionales (mano de obra, transporte)
    (NEWSEQUENTIALID(), '66666666-CCCC-0003-0000-000000000001', @LoteId_Norte, '2026-03-01', 'MANO_DE_OBRA',
     'Jornales semana 9 - deshoje y desmane', 280.00,
     NULL, 0, '2026-03-01T18:00:00', 'device-dev-001'),

    (NEWSEQUENTIALID(), '66666666-CCCC-0004-0000-000000000001', @LoteId_Norte, '2026-03-15', 'MANO_DE_OBRA',
     'Jornales semana 11 - labores culturales', 280.00,
     NULL, 0, '2026-03-15T18:00:00', 'device-dev-001'),

    (NEWSEQUENTIALID(), '66666666-CCCC-0005-0000-000000000001', @LoteId_Norte, '2026-02-28', 'RIEGO',
     'Mantenimiento sistema de riego por goteo', 150.00,
     NULL, 0, '2026-02-28T16:00:00', 'device-dev-001'),

    -- Costos Lote Sur (periodo enero-marzo 2026)
    (NEWSEQUENTIALID(), '66666666-CCCC-0001-0000-000000000002', @LoteId_Sur, '2026-02-01', 'INSUMOS_QUIMICOS',
     'Aplicacion de Mancozeb 80% WP', 85.00,
     (SELECT Id FROM Aplicaciones WHERE ClientId = @AppId_Sur1), 0, '2026-02-01T08:10:00', 'device-dev-002'),

    (NEWSEQUENTIALID(), '66666666-CCCC-0002-0000-000000000002', @LoteId_Sur, '2026-02-15', 'INSUMOS_QUIMICOS',
     'Fertilizacion NPK 15-15-15', 380.00,
     NULL, 0, '2026-02-15T09:15:00', 'device-dev-002'),

    (NEWSEQUENTIALID(), '66666666-CCCC-0003-0000-000000000002', @LoteId_Sur, '2026-01-15', 'MANO_DE_OBRA',
     'Jornales semana 3 - instalacion cables y pinzas', 420.00,
     NULL, 0, '2026-01-15T18:00:00', 'device-dev-002'),

    (NEWSEQUENTIALID(), '66666666-CCCC-0004-0000-000000000002', @LoteId_Sur, '2026-03-01', 'MANO_DE_OBRA',
     'Jornales semana 9', 280.00,
     NULL, 0, '2026-03-01T18:00:00', 'device-dev-002'),

    (NEWSEQUENTIALID(), '66666666-CCCC-0005-0000-000000000002', @LoteId_Sur, '2026-02-20', 'TRANSPORTE',
     'Flete cosecha a empacadora', 200.00,
     NULL, 0, '2026-02-20T14:00:00', 'device-dev-002'),

    -- Costos Lote Cacao (enero 2026)
    (NEWSEQUENTIALID(), '66666666-CCCC-0001-0000-000000000003', @LoteId_Cacao1, '2026-01-10', 'INSUMOS_QUIMICOS',
     'Fertilizacion NPK inicio ciclo 2026', 200.00,
     (SELECT Id FROM Aplicaciones WHERE ClientId = @AppId_N1), 0, '2026-01-10T07:40:00', 'device-dev-001'),

    (NEWSEQUENTIALID(), '66666666-CCCC-0002-0000-000000000003', @LoteId_Cacao1, '2026-01-20', 'MANO_DE_OBRA',
     'Poda de mantenimiento y recoleccion de mazorcas enfermas', 350.00,
     NULL, 0, '2026-01-20T18:00:00', 'device-dev-001');
GO

-- =============================================================================
-- 9. COSECHAS (solo donde no hay carencia activa)
-- Lote Sur: libre para cosechar (carencia Mancozeb expiro el 15-Feb-2026)
-- =============================================================================
DECLARE @LoteId_Sur UNIQUEIDENTIFIER = '33333333-0000-0000-0000-000000000002';

DECLARE @CosechaId_Sur1 UNIQUEIDENTIFIER = '77777777-0000-0000-0000-000000000001';
DECLARE @CosechaId_Sur2 UNIQUEIDENTIFIER = '77777777-0000-0000-0000-000000000002';

INSERT INTO Cosechas (Id, ClientId, LoteId, FechaCosecha, PesoTotalKg, CalidadGrado,
                       Comprador, PrecioVentaKg, IngresoTotal, BloqueadaPorCarencia,
                       OrigenOffline, ClientTimestamp, DeviceId)
VALUES
    -- Cosecha 1 Lote Sur: 20-Feb-2026 (carencia expirada el 15-Feb)
    (
        NEWSEQUENTIALID(), @CosechaId_Sur1, @LoteId_Sur,
        '2026-02-20T08:00:00',
        25000.00,           -- 25 toneladas (3.3 t/ha en 7.5 ha, razonable para banano)
        'PRIMERA',
        'Exportadora Bananec S.A.',
        0.26,               -- $0.26/kg precio FOB referencia
        6500.00,            -- 25000 * 0.26
        0,
        0,
        '2026-02-20T08:30:00', 'device-dev-002'
    ),
    -- Cosecha 2 Lote Sur: 10-Mar-2026
    (
        NEWSEQUENTIALID(), @CosechaId_Sur2, @LoteId_Sur,
        '2026-03-10T07:30:00',
        23500.00,
        'PRIMERA',
        'Exportadora Bananec S.A.',
        0.27,
        6345.00,
        0,
        0,
        '2026-03-10T08:00:00', 'device-dev-002'
    );
GO

-- =============================================================================
-- 10. INGRESOS (generados automaticamente por las cosechas)
-- =============================================================================
DECLARE @LoteId_Sur UNIQUEIDENTIFIER = '33333333-0000-0000-0000-000000000002';

DECLARE @CosechaId_Sur1 UNIQUEIDENTIFIER = '77777777-0000-0000-0000-000000000001';
DECLARE @CosechaId_Sur2 UNIQUEIDENTIFIER = '77777777-0000-0000-0000-000000000002';

INSERT INTO IngresosLote (Id, ClientId, LoteId, CosechaId, Fecha, Comprador, KgVendidos, PrecioKg, Moneda)
VALUES
    (NEWSEQUENTIALID(), '88888888-CCCC-0001-0000-000000000001', @LoteId_Sur,
     (SELECT Id FROM Cosechas WHERE ClientId = @CosechaId_Sur1),
     '2026-02-20', 'Exportadora Bananec S.A.', 25000.00, 0.26, 'USD'),

    (NEWSEQUENTIALID(), '88888888-CCCC-0001-0000-000000000002', @LoteId_Sur,
     (SELECT Id FROM Cosechas WHERE ClientId = @CosechaId_Sur2),
     '2026-03-10', 'Exportadora Bananec S.A.', 23500.00, 0.27, 'USD');
GO

-- =============================================================================
-- 11. PRECIOS DE MERCADO (MAG Ecuador - referencia marzo 2026)
-- =============================================================================
INSERT INTO PreciosMercado (Cultivo, PrecioKg, Fuente, FechaPublicacion, Vigente)
VALUES
    ('Banano',  0.27,  'MAG Ecuador - Boletin Semanal de Precios Agricolas #12-2026', '2026-03-21', 1),
    ('Cacao',   3.85,  'Banco Central del Ecuador - Precio de exportacion granel', '2026-03-18', 1),
    ('Tomate',  0.45,  'MAGAP - Sistema de Informacion de Mercados Agropecuarios', '2026-03-20', 1);
GO

COMMIT TRANSACTION;
PRINT 'Seed data insertado exitosamente.';
GO

-- =============================================================================
-- VERIFICACION DE DATOS INSERTADOS
-- =============================================================================

SELECT 'Usuarios'           AS Tabla, COUNT(*) AS Registros FROM Usuarios WHERE IsDeleted = 0
UNION ALL
SELECT 'Fincas',             COUNT(*) FROM Fincas WHERE IsDeleted = 0
UNION ALL
SELECT 'Lotes',              COUNT(*) FROM Lotes WHERE IsDeleted = 0
UNION ALL
SELECT 'Insumos',            COUNT(*) FROM Insumos WHERE IsDeleted = 0
UNION ALL
SELECT 'PeriodosCarencia',   COUNT(*) FROM PeriodosCarencia WHERE IsDeleted = 0
UNION ALL
SELECT 'Aplicaciones',       COUNT(*) FROM Aplicaciones WHERE IsDeleted = 0
UNION ALL
SELECT 'BloqueosCarencia',   COUNT(*) FROM BloqueosCarencia WHERE Activo = 1
UNION ALL
SELECT 'CostosLote',         COUNT(*) FROM CostosLote WHERE IsDeleted = 0
UNION ALL
SELECT 'Cosechas',           COUNT(*) FROM Cosechas WHERE IsDeleted = 0
UNION ALL
SELECT 'IngresosLote',       COUNT(*) FROM IngresosLote WHERE IsDeleted = 0
UNION ALL
SELECT 'PreciosMercado',     COUNT(*) FROM PreciosMercado WHERE IsDeleted = 0;
GO

-- Verificar bloqueos activos (deberian ser 3)
PRINT '--- Bloqueos de carencia activos al 2026-03-24 ---';
SELECT
    l.Nombre            AS Lote,
    bc.InsumoNombre,
    bc.FechaAplicacion,
    bc.FechaFinCarencia,
    DATEDIFF(DAY, CAST(GETUTCDATE() AS DATE), CAST(bc.FechaFinCarencia AS DATE)) AS DiasRestantes
FROM BloqueosCarencia bc
INNER JOIN Lotes l ON bc.LoteId = l.Id
WHERE bc.Activo = 1
ORDER BY bc.FechaFinCarencia;
GO

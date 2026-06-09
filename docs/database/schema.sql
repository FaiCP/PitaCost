-- =============================================================================
-- PitaSmart -- Schema Principal SQL Server 2022
-- Version: 1.0.0
-- Fecha: 2026-03-24
-- Autor: Senior DBA Architect
-- Descripcion: Modelo de datos completo para el sistema PitaSmart.
--              Cubre todos los Bounded Contexts definidos en bounded-contexts.md.
-- =============================================================================

USE master;
GO

-- Crear base de datos si no existe
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'PitaSmart')
BEGIN
    CREATE DATABASE PitaSmart
        COLLATE Modern_Spanish_CI_AS;
END
GO

USE PitaSmart;
GO

-- =============================================================================
-- CONFIGURACION DE COLLATION Y OPCIONES
-- =============================================================================
-- Modern_Spanish_CI_AS:
--   CI = Case Insensitive (busquedas sin distincion de mayusculas)
--   AS = Accent Sensitive  (distingue tildes: "limón" != "limon")
-- Justificacion: Los datos son en espanol ecuatoriano; nombres, cultivos y
-- ubicaciones deben buscar case-insensitive pero respetar tildes.
-- =============================================================================


-- =============================================================================
-- BOUNDED CONTEXT: IDENTIDAD
-- Tablas: Usuarios, CredencialesPasskey, SesionesDispositivo, AuthChallenges,
--         AuthIntentosFallidos
-- =============================================================================

-- -----------------------------------------------------------------------------
-- Tabla: Usuarios
-- Aggregate Root del bounded context Identidad.
-- Representa al agricultor, administrador o auditor del sistema.
-- -----------------------------------------------------------------------------
CREATE TABLE Usuarios (
    -- Clave primaria generada en servidor (identidad es siempre server-side)
    Id                  UNIQUEIDENTIFIER    NOT NULL    DEFAULT NEWSEQUENTIALID(),
    -- ClientId permite referencias desde dispositivos offline al mismo registro
    ClientId            UNIQUEIDENTIFIER    NOT NULL,

    -- Datos de identidad
    Email               NVARCHAR(254)       NOT NULL,
    NombreCompleto      NVARCHAR(300)       NOT NULL,
    -- Cedula ecuatoriana: 10 digitos, validacion modulo 10 en capa de aplicacion
    Cedula              NCHAR(10)           NOT NULL,
    Telefono            NVARCHAR(15)        NULL,
    -- Enum: AGRICULTOR | ADMINISTRADOR | AUDITOR
    Rol                 NVARCHAR(20)        NOT NULL    DEFAULT 'AGRICULTOR',
    Activo              BIT                 NOT NULL    DEFAULT 1,
    FechaRegistro       DATETIME2(7)        NOT NULL    DEFAULT SYSUTCDATETIME(),
    UltimoAcceso        DATETIME2(7)        NULL,

    -- Columnas de auditoria estandar (presentes en todas las tablas sincronizables)
    CreatedAt           DATETIME2(7)        NOT NULL    DEFAULT SYSUTCDATETIME(),
    LastModified        DATETIME2(7)        NOT NULL    DEFAULT SYSUTCDATETIME(),
    IsDeleted           BIT                 NOT NULL    DEFAULT 0,
    RowVersion          ROWVERSION          NOT NULL,   -- Tipo TIMESTAMP de SQL Server

    -- Restricciones
    CONSTRAINT PK_Usuarios                  PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UQ_Usuarios_Email            UNIQUE (Email),
    CONSTRAINT UQ_Usuarios_Cedula           UNIQUE (Cedula),
    CONSTRAINT UQ_Usuarios_ClientId         UNIQUE (ClientId),
    CONSTRAINT CHK_Usuarios_Rol             CHECK (Rol IN ('AGRICULTOR', 'ADMINISTRADOR', 'AUDITOR')),
    CONSTRAINT CHK_Usuarios_Email_Len       CHECK (LEN(Email) BETWEEN 5 AND 254),
    CONSTRAINT CHK_Usuarios_Cedula_Formato  CHECK (Cedula NOT LIKE '%[^0-9]%' AND LEN(Cedula) = 10),
    CONSTRAINT CHK_Usuarios_Nombre_Len      CHECK (LEN(NombreCompleto) >= 2)
);
GO

-- -----------------------------------------------------------------------------
-- Tabla: CredencialesPasskey (WebAuthn / FIDO2)
-- Un usuario puede tener multiples passkeys (un dispositivo por credencial).
-- La clave publica se almacena como VARBINARY; nunca se expone por API.
-- -----------------------------------------------------------------------------
CREATE TABLE CredencialesPasskey (
    Id                  UNIQUEIDENTIFIER    NOT NULL    DEFAULT NEWSEQUENTIALID(),
    ClientId            UNIQUEIDENTIFIER    NOT NULL,
    UsuarioId           UNIQUEIDENTIFIER    NOT NULL,

    -- Datos WebAuthn
    -- CredentialId es bytes crudos del autenticador (longitud variable, max 1024 bytes)
    CredentialIdBytes   VARBINARY(1024)     NOT NULL,
    -- Representacion Base64Url del CredentialId para busquedas eficientes
    CredentialIdBase64  NVARCHAR(1400)      NOT NULL,
    PublicKeyCose       VARBINARY(8192)     NOT NULL,   -- Clave publica COSE
    SignCount           BIGINT              NOT NULL    DEFAULT 0,
    AaGuid              UNIQUEIDENTIFIER    NOT NULL,
    CredentialType      NVARCHAR(50)        NOT NULL    DEFAULT 'public-key',
    DispositivoNombre   NVARCHAR(200)       NULL,
    Activa              BIT                 NOT NULL    DEFAULT 1,
    FechaRegistro       DATETIME2(7)        NOT NULL    DEFAULT SYSUTCDATETIME(),

    -- Auditoria
    CreatedAt           DATETIME2(7)        NOT NULL    DEFAULT SYSUTCDATETIME(),
    LastModified        DATETIME2(7)        NOT NULL    DEFAULT SYSUTCDATETIME(),
    IsDeleted           BIT                 NOT NULL    DEFAULT 0,
    RowVersion          ROWVERSION          NOT NULL,

    CONSTRAINT PK_CredencialesPasskey              PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UQ_CredencialesPasskey_ClientId     UNIQUE (ClientId),
    CONSTRAINT UQ_CredencialesPasskey_CredId       UNIQUE (CredentialIdBase64),
    CONSTRAINT FK_CredencialesPasskey_Usuario      FOREIGN KEY (UsuarioId)
        REFERENCES Usuarios (Id)
        ON DELETE CASCADE,  -- Si se borra el usuario, se borran sus passkeys
    CONSTRAINT CHK_Passkey_SignCount               CHECK (SignCount >= 0),
    CONSTRAINT CHK_Passkey_Type                   CHECK (CredentialType = 'public-key')
);
GO

-- -----------------------------------------------------------------------------
-- Tabla: SesionesDispositivo
-- Registra sesiones activas por dispositivo. Permite revocar sesiones
-- individualmente (ej: telefono perdido).
-- NOTA SEGURIDAD: RefreshTokenHash almacena el hash SHA-256 del token,
--                 nunca el token en texto claro.
-- -----------------------------------------------------------------------------
CREATE TABLE SesionesDispositivo (
    Id                  UNIQUEIDENTIFIER    NOT NULL    DEFAULT NEWSEQUENTIALID(),
    UsuarioId           UNIQUEIDENTIFIER    NOT NULL,
    DeviceId            NVARCHAR(100)       NOT NULL,
    RefreshTokenHash    NVARCHAR(500)       NOT NULL,   -- SHA-256 del refresh token
    Plataforma          NVARCHAR(50)        NULL,       -- Android, iOS, Web
    AppVersion          NVARCHAR(20)        NULL,
    FechaCreacion       DATETIME2(7)        NOT NULL    DEFAULT SYSUTCDATETIME(),
    FechaExpiracion     DATETIME2(7)        NOT NULL,
    Activa              BIT                 NOT NULL    DEFAULT 1,
    UltimaActividad     DATETIME2(7)        NOT NULL    DEFAULT SYSUTCDATETIME(),

    -- Auditoria
    CreatedAt           DATETIME2(7)        NOT NULL    DEFAULT SYSUTCDATETIME(),
    LastModified        DATETIME2(7)        NOT NULL    DEFAULT SYSUTCDATETIME(),
    IsDeleted           BIT                 NOT NULL    DEFAULT 0,
    RowVersion          ROWVERSION          NOT NULL,

    CONSTRAINT PK_SesionesDispositivo           PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_SesionesDispositivo_Usuario   FOREIGN KEY (UsuarioId)
        REFERENCES Usuarios (Id)
        ON DELETE CASCADE,
    CONSTRAINT CHK_Sesion_Expiracion            CHECK (FechaExpiracion > FechaCreacion),
    CONSTRAINT CHK_Sesion_Plataforma            CHECK (Plataforma IN ('Android', 'iOS', 'Web') OR Plataforma IS NULL)
);
GO

-- -----------------------------------------------------------------------------
-- Tabla: AuthChallenges
-- Almacena challenges WebAuthn temporales (TTL: 60 segundos).
-- Los challenges expirados son purgados por un job de mantenimiento.
-- -----------------------------------------------------------------------------
CREATE TABLE AuthChallenges (
    Id                  UNIQUEIDENTIFIER    NOT NULL    DEFAULT NEWSEQUENTIALID(),
    -- ChallengeToken es el valor base64 enviado al cliente
    ChallengeToken      NVARCHAR(500)       NOT NULL,
    Email               NVARCHAR(254)       NULL,       -- NULL para flujo de registro inicial
    -- Enum: AUTENTICACION | REGISTRO
    Tipo                NVARCHAR(20)        NOT NULL,
    Usado               BIT                 NOT NULL    DEFAULT 0,
    CreadoAt            DATETIME2(7)        NOT NULL    DEFAULT SYSUTCDATETIME(),
    ExpiresAt           DATETIME2(7)        NOT NULL,

    CONSTRAINT PK_AuthChallenges        PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UQ_AuthChallenges_Token  UNIQUE (ChallengeToken),
    CONSTRAINT CHK_AuthChallenge_Tipo   CHECK (Tipo IN ('AUTENTICACION', 'REGISTRO')),
    CONSTRAINT CHK_AuthChallenge_Exp    CHECK (ExpiresAt > CreadoAt)
);
GO

-- -----------------------------------------------------------------------------
-- Tabla: AuthIntentosFallidos
-- Registra intentos fallidos para enforcement del rate limit de 5 intentos
-- en 15 minutos definido en bounded-contexts.md (Identidad, invariante 5).
-- -----------------------------------------------------------------------------
CREATE TABLE AuthIntentosFallidos (
    Id                  BIGINT              NOT NULL    IDENTITY(1,1),
    Email               NVARCHAR(254)       NULL,
    IpAddress           NVARCHAR(45)        NOT NULL,   -- IPv4 o IPv6
    IntentoAt           DATETIME2(7)        NOT NULL    DEFAULT SYSUTCDATETIME(),
    -- CodigoFalla identifica el tipo: VERIFICATION_FAILED, CREDENTIAL_NOT_FOUND, etc.
    CodigoFalla         NVARCHAR(50)        NOT NULL,

    CONSTRAINT PK_AuthIntentosFallidos  PRIMARY KEY CLUSTERED (Id)
);
GO


-- =============================================================================
-- BOUNDED CONTEXT: COSTOS (incluye Fincas y Lotes como Aggregate Root)
-- Tablas: Fincas, Lotes, CostosLote, IngresosLote, PreciosMercado
-- =============================================================================

-- -----------------------------------------------------------------------------
-- Tabla: Fincas
-- Propiedad agricola de un usuario. Un usuario puede tener multiples fincas.
-- -----------------------------------------------------------------------------
CREATE TABLE Fincas (
    Id                  UNIQUEIDENTIFIER    NOT NULL    DEFAULT NEWSEQUENTIALID(),
    ClientId            UNIQUEIDENTIFIER    NOT NULL,
    UsuarioId           UNIQUEIDENTIFIER    NOT NULL,

    Nombre              NVARCHAR(200)       NOT NULL,
    Provincia           NVARCHAR(100)       NOT NULL,
    Canton              NVARCHAR(100)       NOT NULL,
    Parroquia           NVARCHAR(100)       NULL,
    AreaTotalHa         DECIMAL(10, 4)      NOT NULL,

    -- Auditoria
    CreatedAt           DATETIME2(7)        NOT NULL    DEFAULT SYSUTCDATETIME(),
    LastModified        DATETIME2(7)        NOT NULL    DEFAULT SYSUTCDATETIME(),
    IsDeleted           BIT                 NOT NULL    DEFAULT 0,
    RowVersion          ROWVERSION          NOT NULL,

    CONSTRAINT PK_Fincas                PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UQ_Fincas_ClientId       UNIQUE (ClientId),
    CONSTRAINT FK_Fincas_Usuario        FOREIGN KEY (UsuarioId)
        REFERENCES Usuarios (Id)
        ON DELETE RESTRICT,             -- No se puede borrar usuario con fincas
    CONSTRAINT CHK_Fincas_Area         CHECK (AreaTotalHa > 0),
    CONSTRAINT CHK_Fincas_Nombre       CHECK (LEN(Nombre) >= 2)
);
GO

-- -----------------------------------------------------------------------------
-- Tabla: Lotes
-- Aggregate Root del calculo de rentabilidad.
-- Subdivision de una finca para un cultivo especifico.
-- Incluye coordenadas GPS del centroide del lote.
-- -----------------------------------------------------------------------------
CREATE TABLE Lotes (
    Id                  UNIQUEIDENTIFIER    NOT NULL    DEFAULT NEWSEQUENTIALID(),
    ClientId            UNIQUEIDENTIFIER    NOT NULL,
    FincaId             UNIQUEIDENTIFIER    NOT NULL,

    Nombre              NVARCHAR(200)       NOT NULL,
    Cultivo             NVARCHAR(100)       NOT NULL,
    AreaHa              DECIMAL(10, 4)      NOT NULL,

    -- Coordenadas GPS del centroide (Value Object descompuesto en columnas)
    -- Rango Ecuador continental + Galapagos segun api-contract.md
    CentroLatitud       DECIMAL(10, 7)      NULL,
    CentroLongitud      DECIMAL(10, 7)      NULL,

    FechaInicioSiembra  DATE                NULL,
    Activo              BIT                 NOT NULL    DEFAULT 1,

    -- Auditoria
    CreatedAt           DATETIME2(7)        NOT NULL    DEFAULT SYSUTCDATETIME(),
    LastModified        DATETIME2(7)        NOT NULL    DEFAULT SYSUTCDATETIME(),
    IsDeleted           BIT                 NOT NULL    DEFAULT 0,
    RowVersion          ROWVERSION          NOT NULL,

    CONSTRAINT PK_Lotes                     PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UQ_Lotes_ClientId            UNIQUE (ClientId),
    CONSTRAINT FK_Lotes_Finca              FOREIGN KEY (FincaId)
        REFERENCES Fincas (Id)
        ON DELETE RESTRICT,
    CONSTRAINT CHK_Lotes_Area             CHECK (AreaHa > 0),
    CONSTRAINT CHK_Lotes_Latitud          CHECK (CentroLatitud IS NULL OR (CentroLatitud BETWEEN -5.0 AND 2.0)),
    CONSTRAINT CHK_Lotes_Longitud         CHECK (CentroLongitud IS NULL OR (CentroLongitud BETWEEN -92.0 AND -75.0)),
    CONSTRAINT CHK_Lotes_GPS_Consistente  CHECK (
        (CentroLatitud IS NULL AND CentroLongitud IS NULL) OR
        (CentroLatitud IS NOT NULL AND CentroLongitud IS NOT NULL)
    ),
    CONSTRAINT CHK_Lotes_Nombre           CHECK (LEN(Nombre) >= 2)
);
GO

-- -----------------------------------------------------------------------------
-- Tabla: CostosLote
-- Registra cada costo operativo asociado a un lote.
-- AplicacionId y CosechaId son FK opcionales para trazabilidad de origen.
-- Nota: FK a Aplicaciones y Cosechas usa NOCHECK diferido para evitar
--       dependencia circular en orden de creacion.
-- -----------------------------------------------------------------------------
CREATE TABLE CostosLote (
    Id                  UNIQUEIDENTIFIER    NOT NULL    DEFAULT NEWSEQUENTIALID(),
    ClientId            UNIQUEIDENTIFIER    NOT NULL,
    LoteId              UNIQUEIDENTIFIER    NOT NULL,

    -- Fecha de ocurrencia del costo (DATE, no DATETIME porque es registro contable diario)
    Fecha               DATE                NOT NULL,
    -- Enum: INSUMOS_QUIMICOS | MANO_DE_OBRA | TRANSPORTE | RIEGO | MAQUINARIA | OTROS
    Categoria           NVARCHAR(30)        NOT NULL,
    Descripcion         NVARCHAR(500)       NULL,
    -- Value Object Dinero descompuesto; moneda siempre USD en Ecuador
    Monto               DECIMAL(12, 2)      NOT NULL,
    Moneda              NCHAR(3)            NOT NULL    DEFAULT 'USD',

    -- FK opcionales para trazabilidad de origen del costo
    AplicacionId        UNIQUEIDENTIFIER    NULL,       -- FK a Aplicaciones (diferida)
    CosechaId           UNIQUEIDENTIFIER    NULL,       -- FK a Cosechas (diferida)

    -- Control offline
    OrigenOffline       BIT                 NOT NULL    DEFAULT 0,
    ClientTimestamp     DATETIME2(7)        NOT NULL    DEFAULT SYSUTCDATETIME(),
    DeviceId            NVARCHAR(100)       NULL,

    -- Auditoria
    CreatedAt           DATETIME2(7)        NOT NULL    DEFAULT SYSUTCDATETIME(),
    LastModified        DATETIME2(7)        NOT NULL    DEFAULT SYSUTCDATETIME(),
    IsDeleted           BIT                 NOT NULL    DEFAULT 0,
    RowVersion          ROWVERSION          NOT NULL,

    CONSTRAINT PK_CostosLote                PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UQ_CostosLote_ClientId       UNIQUE (ClientId),
    CONSTRAINT FK_CostosLote_Lote          FOREIGN KEY (LoteId)
        REFERENCES Lotes (Id)
        ON DELETE RESTRICT,
    CONSTRAINT CHK_CostosLote_Categoria    CHECK (Categoria IN (
        'INSUMOS_QUIMICOS', 'MANO_DE_OBRA', 'TRANSPORTE',
        'RIEGO', 'MAQUINARIA', 'OTROS'
    )),
    CONSTRAINT CHK_CostosLote_Monto        CHECK (Monto >= 0),
    CONSTRAINT CHK_CostosLote_Moneda       CHECK (Moneda = 'USD'),
    CONSTRAINT CHK_CostosLote_Fecha        CHECK (Fecha <= CAST(SYSUTCDATETIME() AS DATE))
);
GO

-- -----------------------------------------------------------------------------
-- Tabla: IngresosLote
-- Registra ingresos por venta de cosecha. Se crea automaticamente via evento
-- CosechaRegistradaEvent -> RegistrarIngresoCosechaHandler.
-- -----------------------------------------------------------------------------
CREATE TABLE IngresosLote (
    Id                  UNIQUEIDENTIFIER    NOT NULL    DEFAULT NEWSEQUENTIALID(),
    ClientId            UNIQUEIDENTIFIER    NOT NULL,
    LoteId              UNIQUEIDENTIFIER    NOT NULL,
    CosechaId           UNIQUEIDENTIFIER    NOT NULL,   -- FK a Cosechas

    Fecha               DATE                NOT NULL,
    Comprador           NVARCHAR(200)       NULL,
    KgVendidos          DECIMAL(12, 4)      NOT NULL,
    PrecioKg            DECIMAL(10, 4)      NOT NULL,
    TotalVenta          AS (KgVendidos * PrecioKg),     -- Columna calculada persistida
    Moneda              NCHAR(3)            NOT NULL    DEFAULT 'USD',

    -- Auditoria
    CreatedAt           DATETIME2(7)        NOT NULL    DEFAULT SYSUTCDATETIME(),
    LastModified        DATETIME2(7)        NOT NULL    DEFAULT SYSUTCDATETIME(),
    IsDeleted           BIT                 NOT NULL    DEFAULT 0,
    RowVersion          ROWVERSION          NOT NULL,

    CONSTRAINT PK_IngresosLote              PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UQ_IngresosLote_ClientId     UNIQUE (ClientId),
    CONSTRAINT FK_IngresosLote_Lote        FOREIGN KEY (LoteId)
        REFERENCES Lotes (Id)
        ON DELETE RESTRICT,
    CONSTRAINT CHK_IngresosLote_Kg         CHECK (KgVendidos > 0),
    CONSTRAINT CHK_IngresosLote_Precio     CHECK (PrecioKg >= 0),
    CONSTRAINT CHK_IngresosLote_Moneda     CHECK (Moneda = 'USD')
);
GO

-- -----------------------------------------------------------------------------
-- Tabla: PreciosMercado
-- Precios de referencia por cultivo, publicados por MAG Ecuador u otras fuentes.
-- Fuente autoritativa para el dashboard financiero.
-- -----------------------------------------------------------------------------
CREATE TABLE PreciosMercado (
    Id                  UNIQUEIDENTIFIER    NOT NULL    DEFAULT NEWSEQUENTIALID(),
    Cultivo             NVARCHAR(100)       NOT NULL,
    PrecioKg            DECIMAL(10, 4)      NOT NULL,
    Fuente              NVARCHAR(200)       NULL,
    FechaPublicacion    DATE                NOT NULL,
    Vigente             BIT                 NOT NULL    DEFAULT 1,

    -- Auditoria
    CreatedAt           DATETIME2(7)        NOT NULL    DEFAULT SYSUTCDATETIME(),
    LastModified        DATETIME2(7)        NOT NULL    DEFAULT SYSUTCDATETIME(),
    IsDeleted           BIT                 NOT NULL    DEFAULT 0,
    RowVersion          ROWVERSION          NOT NULL,

    CONSTRAINT PK_PreciosMercado            PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT CHK_PreciosMercado_Precio    CHECK (PrecioKg >= 0)
);
GO


-- =============================================================================
-- BOUNDED CONTEXT: AGROQUIMICOS
-- Tablas: Insumos, PeriodosCarencia, FichasTecnicas
-- Este contexto es principalmente de LECTURA (catalogo mantenido por admins).
-- =============================================================================

-- -----------------------------------------------------------------------------
-- Tabla: Insumos
-- Catalogo de productos agroquimicos registrados en Agrocalidad.
-- RegistroAgrocalidad es el identificador regulatorio oficial (UNICO).
-- -----------------------------------------------------------------------------
CREATE TABLE Insumos (
    Id                      UNIQUEIDENTIFIER    NOT NULL    DEFAULT NEWSEQUENTIALID(),
    ClientId                UNIQUEIDENTIFIER    NOT NULL,

    NombreComercial         NVARCHAR(200)       NOT NULL,
    IngredienteActivo       NVARCHAR(200)       NOT NULL,
    Fabricante              NVARCHAR(200)       NULL,
    RegistroAgrocalidad     NVARCHAR(50)        NOT NULL,

    -- Enum: FUNGICIDA | HERBICIDA | INSECTICIDA | FERTILIZANTE | NEMATICIDA | OTRO
    TipoProducto            NVARCHAR(20)        NOT NULL,
    -- Enum: I | II | III | IV  (I = Extremadamente toxico, IV = Ligeramente toxico)
    CategoriaToxico         NCHAR(2)            NOT NULL,

    -- Value Object Concentracion descompuesto
    ConcentracionValor      DECIMAL(10, 4)      NULL,
    ConcentracionUnidad     NVARCHAR(20)        NULL,       -- PORCENTAJE, G_L, etc.

    -- Dosis recomendada por Agrocalidad
    DosisMinima             DECIMAL(10, 4)      NULL,
    DosisMaxima             DECIMAL(10, 4)      NULL,
    -- Enum: L_HA | KG_HA | ML_HA | G_HA | CC_HA
    UnidadDosis             NVARCHAR(10)        NULL,

    Activo                  BIT                 NOT NULL    DEFAULT 1,

    -- Auditoria
    CreatedAt               DATETIME2(7)        NOT NULL    DEFAULT SYSUTCDATETIME(),
    LastModified            DATETIME2(7)        NOT NULL    DEFAULT SYSUTCDATETIME(),
    IsDeleted               BIT                 NOT NULL    DEFAULT 0,
    RowVersion              ROWVERSION          NOT NULL,

    CONSTRAINT PK_Insumos                       PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UQ_Insumos_ClientId              UNIQUE (ClientId),
    CONSTRAINT UQ_Insumos_RegistroAgrocalidad   UNIQUE (RegistroAgrocalidad),
    CONSTRAINT CHK_Insumos_TipoProducto         CHECK (TipoProducto IN (
        'FUNGICIDA', 'HERBICIDA', 'INSECTICIDA',
        'FERTILIZANTE', 'NEMATICIDA', 'OTRO'
    )),
    CONSTRAINT CHK_Insumos_CategoriaToxico      CHECK (CategoriaToxico IN ('I', 'II', 'III', 'IV')),
    CONSTRAINT CHK_Insumos_Dosis               CHECK (
        DosisMaxima IS NULL OR DosisMinima IS NULL OR DosisMaxima >= DosisMinima
    ),
    CONSTRAINT CHK_Insumos_UnidadDosis         CHECK (UnidadDosis IN (
        'L_HA', 'KG_HA', 'ML_HA', 'G_HA', 'CC_HA'
    ) OR UnidadDosis IS NULL),
    CONSTRAINT CHK_Insumos_Nombre              CHECK (LEN(NombreComercial) >= 2)
);
GO

-- -----------------------------------------------------------------------------
-- Tabla: PeriodosCarencia
-- Periodos de carencia POR CULTIVO para cada insumo.
-- Un insumo puede tener diferentes periodos segun el cultivo.
-- Invariante: Un insumo debe tener al menos un periodo de carencia.
-- -----------------------------------------------------------------------------
CREATE TABLE PeriodosCarencia (
    Id                  UNIQUEIDENTIFIER    NOT NULL    DEFAULT NEWSEQUENTIALID(),
    InsumoId            UNIQUEIDENTIFIER    NOT NULL,
    Cultivo             NVARCHAR(100)       NOT NULL,
    DiasCarencia        INT                 NOT NULL,
    FuenteRegulacion    NVARCHAR(200)       NULL,

    -- Auditoria
    CreatedAt           DATETIME2(7)        NOT NULL    DEFAULT SYSUTCDATETIME(),
    LastModified        DATETIME2(7)        NOT NULL    DEFAULT SYSUTCDATETIME(),
    IsDeleted           BIT                 NOT NULL    DEFAULT 0,
    RowVersion          ROWVERSION          NOT NULL,

    CONSTRAINT PK_PeriodosCarencia          PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UQ_PeriodosCarencia_Key      UNIQUE (InsumoId, Cultivo),  -- Un periodo por insumo-cultivo
    CONSTRAINT FK_PeriodosCarencia_Insumo   FOREIGN KEY (InsumoId)
        REFERENCES Insumos (Id)
        ON DELETE CASCADE,      -- Si se elimina un insumo, se eliminan sus periodos
    CONSTRAINT CHK_PeriodosCarencia_Dias    CHECK (DiasCarencia >= 0)    -- 0 = sin carencia
);
GO

-- -----------------------------------------------------------------------------
-- Tabla: FichasTecnicas
-- Documentacion tecnica adjunta a cada insumo.
-- ContenidoHtml almacena la ficha en formato HTML para renderizado movil.
-- -----------------------------------------------------------------------------
CREATE TABLE FichasTecnicas (
    Id                  UNIQUEIDENTIFIER    NOT NULL    DEFAULT NEWSEQUENTIALID(),
    InsumoId            UNIQUEIDENTIFIER    NOT NULL,
    ContenidoHtml       NVARCHAR(MAX)       NULL,
    UrlDocumento        NVARCHAR(500)       NULL,
    FechaActualizacion  DATETIME2(7)        NOT NULL    DEFAULT SYSUTCDATETIME(),

    -- Auditoria
    CreatedAt           DATETIME2(7)        NOT NULL    DEFAULT SYSUTCDATETIME(),
    LastModified        DATETIME2(7)        NOT NULL    DEFAULT SYSUTCDATETIME(),
    IsDeleted           BIT                 NOT NULL    DEFAULT 0,
    RowVersion          ROWVERSION          NOT NULL,

    CONSTRAINT PK_FichasTecnicas            PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_FichasTecnicas_Insumo     FOREIGN KEY (InsumoId)
        REFERENCES Insumos (Id)
        ON DELETE CASCADE
);
GO


-- =============================================================================
-- BOUNDED CONTEXT: APLICACIONES
-- Tablas: Aplicaciones, DetallesAplicacion
-- Nucleo de trazabilidad exigida por Agrocalidad.
-- CRITICO: Las aplicaciones NO se pueden eliminar (solo anular). Ver invariante 5.
-- =============================================================================

-- -----------------------------------------------------------------------------
-- Tabla: Aplicaciones (AplicacionQuimico en el dominio)
-- Cada fila representa una aplicacion fisica de un insumo a un lote.
-- FechaFinCarencia es calculada por el servidor al recibir la aplicacion.
-- -----------------------------------------------------------------------------
CREATE TABLE Aplicaciones (
    Id                      UNIQUEIDENTIFIER    NOT NULL    DEFAULT NEWSEQUENTIALID(),
    -- ClientId = UUID generado en dispositivo; es el Id de dominio del cliente
    ClientId                UNIQUEIDENTIFIER    NOT NULL,
    LoteId                  UNIQUEIDENTIFIER    NOT NULL,
    InsumoId                UNIQUEIDENTIFIER    NOT NULL,

    FechaAplicacion         DATETIME2(7)        NOT NULL,

    -- Value Object Dosis descompuesto
    DosisCantidad           DECIMAL(10, 4)      NOT NULL,
    -- Enum: L_HA | KG_HA | ML_HA | G_HA | CC_HA
    DosisUnidad             NVARCHAR(10)        NOT NULL,

    AreaAplicadaHa          DECIMAL(10, 4)      NOT NULL,
    -- Enum: FUMIGACION | DRENCH | INYECCION | GRANULAR | OTRO
    MetodoAplicacion        NVARCHAR(20)        NOT NULL,
    OperadorNombre          NVARCHAR(200)       NOT NULL,

    -- Value Object CoordenadasGps descompuesto (nullable)
    GpsLatitud              DECIMAL(10, 7)      NULL,
    GpsLongitud             DECIMAL(10, 7)      NULL,

    Observaciones           NVARCHAR(1000)      NULL,
    CostoTotal              DECIMAL(12, 2)      NOT NULL    DEFAULT 0,

    -- Periodo de carencia calculado al momento del registro
    DiasCarenciaAplicables  INT                 NOT NULL,
    FechaFinCarencia        DATETIME2(7)        NOT NULL,

    -- Estado de la aplicacion (ACTIVA | ANULADA; no se elimina fisicamente)
    EstadoAplicacion        NVARCHAR(10)        NOT NULL    DEFAULT 'ACTIVA',
    MotivoAnulacion         NVARCHAR(500)       NULL,

    -- Control offline
    OrigenOffline           BIT                 NOT NULL    DEFAULT 0,
    ClientTimestamp         DATETIME2(7)        NOT NULL    DEFAULT SYSUTCDATETIME(),
    DeviceId                NVARCHAR(100)       NULL,

    -- Auditoria
    CreatedAt               DATETIME2(7)        NOT NULL    DEFAULT SYSUTCDATETIME(),
    LastModified            DATETIME2(7)        NOT NULL    DEFAULT SYSUTCDATETIME(),
    IsDeleted               BIT                 NOT NULL    DEFAULT 0,  -- Siempre 0; soft delete prohibido
    RowVersion              ROWVERSION          NOT NULL,

    CONSTRAINT PK_Aplicaciones                  PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UQ_Aplicaciones_ClientId         UNIQUE (ClientId),
    CONSTRAINT FK_Aplicaciones_Lote            FOREIGN KEY (LoteId)
        REFERENCES Lotes (Id)
        ON DELETE RESTRICT,
    CONSTRAINT FK_Aplicaciones_Insumo          FOREIGN KEY (InsumoId)
        REFERENCES Insumos (Id)
        ON DELETE RESTRICT,
    CONSTRAINT CHK_Aplicaciones_Dosis         CHECK (DosisCantidad > 0),
    CONSTRAINT CHK_Aplicaciones_Area          CHECK (AreaAplicadaHa > 0),
    CONSTRAINT CHK_Aplicaciones_Costo         CHECK (CostoTotal >= 0),
    CONSTRAINT CHK_Aplicaciones_DiasCarencia  CHECK (DiasCarenciaAplicables >= 0),
    CONSTRAINT CHK_Aplicaciones_FechaFin      CHECK (FechaFinCarencia >= FechaAplicacion),
    CONSTRAINT CHK_Aplicaciones_Metodo        CHECK (MetodoAplicacion IN (
        'FUMIGACION', 'DRENCH', 'INYECCION', 'GRANULAR', 'OTRO'
    )),
    CONSTRAINT CHK_Aplicaciones_DosisUnidad   CHECK (DosisUnidad IN (
        'L_HA', 'KG_HA', 'ML_HA', 'G_HA', 'CC_HA'
    )),
    CONSTRAINT CHK_Aplicaciones_Estado        CHECK (EstadoAplicacion IN ('ACTIVA', 'ANULADA')),
    CONSTRAINT CHK_Aplicaciones_GPS           CHECK (
        (GpsLatitud IS NULL AND GpsLongitud IS NULL) OR
        (GpsLatitud IS NOT NULL AND GpsLongitud IS NOT NULL AND
         GpsLatitud BETWEEN -5.0 AND 2.0 AND
         GpsLongitud BETWEEN -92.0 AND -75.0)
    ),
    -- Tolerancia de +1 hora por GPS/relojes: validacion final en capa de aplicacion
    CONSTRAINT CHK_Aplicaciones_FechaNoFutura CHECK (FechaAplicacion <= DATEADD(HOUR, 1, SYSUTCDATETIME()))
);
GO

-- -----------------------------------------------------------------------------
-- Tabla: DetallesAplicacion
-- Campos adicionales de la aplicacion (condicion climatica, etc.).
-- Implementa el patron de "extensiones de dominio" sin alterar el schema base.
-- -----------------------------------------------------------------------------
CREATE TABLE DetallesAplicacion (
    Id              UNIQUEIDENTIFIER    NOT NULL    DEFAULT NEWSEQUENTIALID(),
    AplicacionId    UNIQUEIDENTIFIER    NOT NULL,
    Campo           NVARCHAR(50)        NOT NULL,
    Valor           NVARCHAR(500)       NOT NULL,

    -- Auditoria minima (no necesita RowVersion; es child de Aplicacion)
    CreatedAt       DATETIME2(7)        NOT NULL    DEFAULT SYSUTCDATETIME(),

    CONSTRAINT PK_DetallesAplicacion        PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_DetallesAplicacion_App    FOREIGN KEY (AplicacionId)
        REFERENCES Aplicaciones (Id)
        ON DELETE CASCADE,
    CONSTRAINT CHK_DetallesAplicacion_Campo CHECK (LEN(Campo) >= 1)
);
GO


-- =============================================================================
-- BOUNDED CONTEXT: COSECHA
-- Tablas: Cosechas, BloqueosCarencia
-- Regla critica: verificacion de periodo de carencia antes de permitir cosecha.
-- =============================================================================

-- -----------------------------------------------------------------------------
-- Tabla: Cosechas
-- Aggregate Root del contexto Cosecha.
-- BloqueadaPorCarencia es un flag calculado al momento del registro que
-- permite auditoria de intentos de cosecha en periodo de carencia.
-- -----------------------------------------------------------------------------
CREATE TABLE Cosechas (
    Id                      UNIQUEIDENTIFIER    NOT NULL    DEFAULT NEWSEQUENTIALID(),
    ClientId                UNIQUEIDENTIFIER    NOT NULL,
    LoteId                  UNIQUEIDENTIFIER    NOT NULL,

    FechaCosecha            DATETIME2(7)        NOT NULL,
    PesoTotalKg             DECIMAL(12, 4)      NOT NULL,
    -- Enum: PREMIUM | PRIMERA | SEGUNDA | RECHAZO
    CalidadGrado            NVARCHAR(10)        NOT NULL,
    Comprador               NVARCHAR(200)       NULL,
    PrecioVentaKg           DECIMAL(10, 4)      NULL,
    -- IngresoTotal es calculado y persistido (no columna AS, para evitar
    -- problemas con sync y auditoria de valores historicos)
    IngresoTotal            DECIMAL(12, 2)      NULL,

    Observaciones           NVARCHAR(1000)      NULL,

    -- Flag de auditoria: si se intento registrar en periodo de carencia
    -- Una cosecha con BloqueadaPorCarencia = 1 significa que fue RECHAZADA
    -- en ese momento. Si llego a ser registrada, es porque la carencia expiro.
    BloqueadaPorCarencia    BIT                 NOT NULL    DEFAULT 0,

    -- Control offline
    OrigenOffline           BIT                 NOT NULL    DEFAULT 0,
    ClientTimestamp         DATETIME2(7)        NOT NULL    DEFAULT SYSUTCDATETIME(),
    DeviceId                NVARCHAR(100)       NULL,

    -- Auditoria
    CreatedAt               DATETIME2(7)        NOT NULL    DEFAULT SYSUTCDATETIME(),
    LastModified            DATETIME2(7)        NOT NULL    DEFAULT SYSUTCDATETIME(),
    IsDeleted               BIT                 NOT NULL    DEFAULT 0,
    RowVersion              ROWVERSION          NOT NULL,

    CONSTRAINT PK_Cosechas                  PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UQ_Cosechas_ClientId         UNIQUE (ClientId),
    CONSTRAINT FK_Cosechas_Lote            FOREIGN KEY (LoteId)
        REFERENCES Lotes (Id)
        ON DELETE RESTRICT,
    CONSTRAINT CHK_Cosechas_Peso           CHECK (PesoTotalKg > 0),
    CONSTRAINT CHK_Cosechas_Calidad        CHECK (CalidadGrado IN ('PREMIUM', 'PRIMERA', 'SEGUNDA', 'RECHAZO')),
    CONSTRAINT CHK_Cosechas_Precio         CHECK (PrecioVentaKg IS NULL OR PrecioVentaKg >= 0),
    CONSTRAINT CHK_Cosechas_Ingreso        CHECK (IngresoTotal IS NULL OR IngresoTotal >= 0)
);
GO

-- -----------------------------------------------------------------------------
-- Tabla: BloqueosCarencia
-- Vincula un lote con la aplicacion que lo bloquea.
-- Permite consultas eficientes de "hay algun bloqueo activo en este lote".
-- Se actualiza via evento AplicacionRegistradaEvent y por el job de expiracion.
-- -----------------------------------------------------------------------------
CREATE TABLE BloqueosCarencia (
    Id                  UNIQUEIDENTIFIER    NOT NULL    DEFAULT NEWSEQUENTIALID(),
    LoteId              UNIQUEIDENTIFIER    NOT NULL,
    AplicacionId        UNIQUEIDENTIFIER    NOT NULL,
    InsumoId            UNIQUEIDENTIFIER    NOT NULL,   -- Desnormalizado para queries rapidas
    InsumoNombre        NVARCHAR(200)       NOT NULL,   -- Desnormalizado para mostrar en alertas
    FechaAplicacion     DATETIME2(7)        NOT NULL,
    FechaFinCarencia    DATETIME2(7)        NOT NULL,
    Activo              BIT                 NOT NULL    DEFAULT 1,

    -- Auditoria
    CreatedAt           DATETIME2(7)        NOT NULL    DEFAULT SYSUTCDATETIME(),
    LastModified        DATETIME2(7)        NOT NULL    DEFAULT SYSUTCDATETIME(),

    CONSTRAINT PK_BloqueosCarencia              PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT UQ_BloqueosCarencia_Key          UNIQUE (LoteId, AplicacionId),
    CONSTRAINT FK_BloqueosCarencia_Lote         FOREIGN KEY (LoteId)
        REFERENCES Lotes (Id)
        ON DELETE RESTRICT,
    CONSTRAINT FK_BloqueosCarencia_Aplicacion   FOREIGN KEY (AplicacionId)
        REFERENCES Aplicaciones (Id)
        ON DELETE RESTRICT,
    CONSTRAINT CHK_BloqueosCarencia_Fechas      CHECK (FechaFinCarencia >= FechaAplicacion)
);
GO


-- =============================================================================
-- BOUNDED CONTEXT: SINCRONIZACION
-- Tablas: OperacionesSyncPendientes, ConflictosSync, SyncOperacionesLog
-- Anti-Corruption Layer entre dispositivos offline y bounded contexts del servidor.
-- =============================================================================

-- -----------------------------------------------------------------------------
-- Tabla: OperacionesSyncPendientes
-- Cola de operaciones enviadas por dispositivos offline.
-- Cada fila es una operacion atomica con su payload JSON serializado.
-- Estado manejado por el motor de sync del servidor.
-- -----------------------------------------------------------------------------
CREATE TABLE OperacionesSyncPendientes (
    -- OperacionId es la idempotency key generada en el cliente
    OperacionId         UNIQUEIDENTIFIER    NOT NULL,
    DeviceId            NVARCHAR(100)       NOT NULL,
    UsuarioId           UNIQUEIDENTIFIER    NOT NULL,

    -- Enum: CREAR_APLICACION | ACTUALIZAR_APLICACION | CREAR_COSECHA |
    --       CREAR_COSTO | ACTUALIZAR_COSTO | ELIMINAR_COSTO |
    --       CREAR_LOTE | ACTUALIZAR_LOTE
    Tipo                NVARCHAR(30)        NOT NULL,
    EntidadId           UNIQUEIDENTIFIER    NOT NULL,
    EntidadTipo         NVARCHAR(50)        NOT NULL,
    Payload             NVARCHAR(MAX)       NOT NULL,   -- JSON serializado
    ClientTimestamp     DATETIME2(7)        NOT NULL,
    -- RowVersion que conocia el cliente antes de la operacion (para updates)
    RowVersionAnterior  VARBINARY(8)        NULL,

    -- Enum: PENDIENTE | EN_PROCESO | APLICADA | DUPLICADA |
    --       CONFLICTO | RECHAZADA | ERROR
    Estado              NVARCHAR(15)        NOT NULL    DEFAULT 'PENDIENTE',
    IntentoNumero       INT                 NOT NULL    DEFAULT 1,
    ProcesadoAt         DATETIME2(7)        NULL,
    -- Detalle del error si aplica (max 2000 chars segun spec)
    ErrorDetalle        NVARCHAR(2000)      NULL,

    -- Auditoria
    CreatedAt           DATETIME2(7)        NOT NULL    DEFAULT SYSUTCDATETIME(),
    LastModified        DATETIME2(7)        NOT NULL    DEFAULT SYSUTCDATETIME(),

    CONSTRAINT PK_OperacionesSyncPendientes     PRIMARY KEY CLUSTERED (OperacionId),
    CONSTRAINT FK_Sync_Usuario                  FOREIGN KEY (UsuarioId)
        REFERENCES Usuarios (Id)
        ON DELETE RESTRICT,
    CONSTRAINT CHK_Sync_Tipo                    CHECK (Tipo IN (
        'CREAR_APLICACION', 'ACTUALIZAR_APLICACION',
        'CREAR_COSECHA',
        'CREAR_COSTO', 'ACTUALIZAR_COSTO', 'ELIMINAR_COSTO',
        'CREAR_LOTE', 'ACTUALIZAR_LOTE'
    )),
    CONSTRAINT CHK_Sync_Estado                  CHECK (Estado IN (
        'PENDIENTE', 'EN_PROCESO', 'APLICADA', 'DUPLICADA',
        'CONFLICTO', 'RECHAZADA', 'ERROR'
    )),
    CONSTRAINT CHK_Sync_Intentos                CHECK (IntentoNumero >= 1)
);
GO

-- -----------------------------------------------------------------------------
-- Tabla: ConflictosSync
-- Registra conflictos de RowVersion detectados durante el sync.
-- Permite resolucion manual o automatica (LWW para entidades no criticas).
-- Se retiene para auditoria; no se purga automaticamente.
-- -----------------------------------------------------------------------------
CREATE TABLE ConflictosSync (
    Id                  UNIQUEIDENTIFIER    NOT NULL    DEFAULT NEWSEQUENTIALID(),
    OperacionId         UNIQUEIDENTIFIER    NOT NULL,
    EntidadId           UNIQUEIDENTIFIER    NOT NULL,
    EntidadTipo         NVARCHAR(50)        NOT NULL,

    DatosCliente        NVARCHAR(MAX)       NOT NULL,   -- JSON de la version del cliente
    DatosServidor       NVARCHAR(MAX)       NOT NULL,   -- JSON de la version del servidor

    RowVersionCliente   VARBINARY(8)        NOT NULL,
    RowVersionServidor  VARBINARY(8)        NOT NULL,

    -- Enum: CLIENTE_GANA | SERVIDOR_GANA | MERGE_MANUAL | NULL (sin resolver)
    Resolucion          NVARCHAR(15)        NULL,
    -- "AUTO" o ID del usuario que resolvio
    ResueltoPor         NVARCHAR(50)        NULL,
    ResueltoAt          DATETIME2(7)        NULL,

    -- Auditoria
    CreadoAt            DATETIME2(7)        NOT NULL    DEFAULT SYSUTCDATETIME(),

    CONSTRAINT PK_ConflictosSync            PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_ConflictosSync_Op         FOREIGN KEY (OperacionId)
        REFERENCES OperacionesSyncPendientes (OperacionId)
        ON DELETE CASCADE,
    CONSTRAINT CHK_ConflictoSync_Resolucion CHECK (Resolucion IN (
        'CLIENTE_GANA', 'SERVIDOR_GANA', 'MERGE_MANUAL'
    ) OR Resolucion IS NULL)
);
GO

-- -----------------------------------------------------------------------------
-- Tabla: SyncOperacionesLog
-- Log permanente de operaciones procesadas para garantia de idempotencia.
-- Si OperacionId ya existe en esta tabla, la operacion fue procesada.
-- Se purga cada 90 dias (invariante del bounded context Sincronizacion).
-- -----------------------------------------------------------------------------
CREATE TABLE SyncOperacionesLog (
    OperacionId     UNIQUEIDENTIFIER    NOT NULL,
    DeviceId        NVARCHAR(100)       NOT NULL,
    UsuarioId       UNIQUEIDENTIFIER    NOT NULL,
    Tipo            NVARCHAR(30)        NOT NULL,
    EntidadId       UNIQUEIDENTIFIER    NOT NULL,
    EntidadTipo     NVARCHAR(50)        NOT NULL,
    -- Estado final que se retorna al cliente
    Estado          NVARCHAR(15)        NOT NULL,
    ProcesadoAt     DATETIME2(7)        NOT NULL    DEFAULT SYSUTCDATETIME(),
    -- RowVersion del registro en SQL Server tras la operacion
    RowVersionResultante VARBINARY(8)   NULL,

    CONSTRAINT PK_SyncOperacionesLog    PRIMARY KEY CLUSTERED (OperacionId)
);
GO

-- Tabla de auditoria de eventos de dominio disparados (traza tecnica)
CREATE TABLE AuditoriaSyncEventos (
    Id              BIGINT              NOT NULL    IDENTITY(1,1),
    OperacionId     UNIQUEIDENTIFIER    NOT NULL,
    EventoTipo      NVARCHAR(100)       NOT NULL,
    EventoPayload   NVARCHAR(MAX)       NULL,
    DisparadoAt     DATETIME2(7)        NOT NULL    DEFAULT SYSUTCDATETIME(),

    CONSTRAINT PK_AuditoriaSyncEventos  PRIMARY KEY CLUSTERED (Id)
);
GO


-- =============================================================================
-- FK DIFERIDAS: CostosLote -> Aplicaciones y Cosechas
-- Se agregan aqui porque Aplicaciones y Cosechas se crean despues de CostosLote.
-- =============================================================================

ALTER TABLE CostosLote
    ADD CONSTRAINT FK_CostosLote_Aplicacion FOREIGN KEY (AplicacionId)
        REFERENCES Aplicaciones (Id)
        ON DELETE SET NULL;     -- Si se anula una aplicacion, el costo se desvincula

ALTER TABLE CostosLote
    ADD CONSTRAINT FK_CostosLote_Cosecha FOREIGN KEY (CosechaId)
        REFERENCES Cosechas (Id)
        ON DELETE SET NULL;

-- FK de IngresosLote a Cosechas (Cosechas creada antes que IngresosLote en schema,
-- pero la FK se declara aqui por claridad de contexto)
ALTER TABLE IngresosLote
    ADD CONSTRAINT FK_IngresosLote_Cosecha FOREIGN KEY (CosechaId)
        REFERENCES Cosechas (Id)
        ON DELETE RESTRICT;
GO


-- =============================================================================
-- TRIGGERS DE AUDITORIA
-- Mantiene LastModified actualizado automaticamente en cada UPDATE.
-- Evita que la capa de aplicacion tenga que recordar actualizar el campo.
-- NOTA DE PERFORMANCE: Los triggers tienen overhead por registro. Para tablas
-- de alto volumen (Aplicaciones, CostosLote), considerar moverlo a la app.
-- =============================================================================

-- Macro de trigger de auditoria (LastModified update)
-- Se crea uno por cada tabla principal sincronizable.

CREATE OR ALTER TRIGGER TR_Usuarios_LastModified
ON Usuarios AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE Usuarios
    SET LastModified = SYSUTCDATETIME()
    WHERE Id IN (SELECT Id FROM inserted);
END;
GO

CREATE OR ALTER TRIGGER TR_Fincas_LastModified
ON Fincas AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE Fincas
    SET LastModified = SYSUTCDATETIME()
    WHERE Id IN (SELECT Id FROM inserted);
END;
GO

CREATE OR ALTER TRIGGER TR_Lotes_LastModified
ON Lotes AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE Lotes
    SET LastModified = SYSUTCDATETIME()
    WHERE Id IN (SELECT Id FROM inserted);
END;
GO

CREATE OR ALTER TRIGGER TR_Aplicaciones_LastModified
ON Aplicaciones AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE Aplicaciones
    SET LastModified = SYSUTCDATETIME()
    WHERE Id IN (SELECT Id FROM inserted);
END;
GO

CREATE OR ALTER TRIGGER TR_Cosechas_LastModified
ON Cosechas AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE Cosechas
    SET LastModified = SYSUTCDATETIME()
    WHERE Id IN (SELECT Id FROM inserted);
END;
GO

CREATE OR ALTER TRIGGER TR_CostosLote_LastModified
ON CostosLote AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE CostosLote
    SET LastModified = SYSUTCDATETIME()
    WHERE Id IN (SELECT Id FROM inserted);
END;
GO

-- Trigger para prevenir DELETE fisico en Aplicaciones (solo se permite IsDeleted=0 o ANULADA)
CREATE OR ALTER TRIGGER TR_Aplicaciones_PrevenirDelete
ON Aplicaciones INSTEAD OF DELETE
AS
BEGIN
    SET NOCOUNT ON;
    RAISERROR(
        'Las aplicaciones de quimicos no pueden ser eliminadas fisicamente. Use EstadoAplicacion = ''ANULADA'' para anular.',
        16, 1
    );
    ROLLBACK;
END;
GO

-- Trigger para actualizar BloqueosCarencia cuando una aplicacion se anula
CREATE OR ALTER TRIGGER TR_Aplicaciones_AnulacionBloqueo
ON Aplicaciones AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    -- Cuando una aplicacion pasa a ANULADA, desactivar el bloqueo de carencia
    IF UPDATE(EstadoAplicacion)
    BEGIN
        UPDATE BloqueosCarencia
        SET Activo      = 0,
            LastModified = SYSUTCDATETIME()
        WHERE AplicacionId IN (
            SELECT Id FROM inserted WHERE EstadoAplicacion = 'ANULADA'
        );
    END
END;
GO

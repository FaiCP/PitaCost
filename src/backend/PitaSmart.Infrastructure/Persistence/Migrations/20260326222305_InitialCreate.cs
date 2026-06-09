using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PitaSmart.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Aplicaciones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LoteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InsumoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FechaAplicacion = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DosisCantidad = table.Column<decimal>(type: "decimal(10,4)", nullable: false),
                    DosisUnidad = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AreaAplicadaHa = table.Column<decimal>(type: "decimal(10,4)", nullable: false),
                    MetodoAplicacion = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    OperadorNombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    GpsLatitud = table.Column<double>(type: "float", nullable: true),
                    GpsLongitud = table.Column<double>(type: "float", nullable: true),
                    Observaciones = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CostoTotal = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    DiasCarenciaAplicables = table.Column<int>(type: "int", nullable: false),
                    FechaFinCarencia = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreadoOffline = table.Column<bool>(type: "bit", nullable: false),
                    ClientTimestamp = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DeviceId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Aplicaciones", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Cosechas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LoteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FechaCosecha = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    PesoTotalKg = table.Column<decimal>(type: "decimal(12,4)", nullable: false),
                    CalidadGrado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Comprador = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PrecioVentaKg = table.Column<decimal>(type: "decimal(10,4)", nullable: true),
                    IngresoTotal = table.Column<decimal>(type: "decimal(12,2)", nullable: true),
                    Observaciones = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    BloqueadaPorCarencia = table.Column<bool>(type: "bit", nullable: false),
                    CreadoOffline = table.Column<bool>(type: "bit", nullable: false),
                    ClientTimestamp = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cosechas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Fincas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Provincia = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Canton = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Parroquia = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AreaTotalHa = table.Column<decimal>(type: "decimal(10,4)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Fincas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Insumos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NombreComercial = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IngredienteActivo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Fabricante = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RegistroAgrocalidad = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TipoProducto = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CategoriaToxico = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    ConcentracionValor = table.Column<decimal>(type: "decimal(10,4)", nullable: false),
                    ConcentracionUnidad = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    DosisMinima = table.Column<decimal>(type: "decimal(10,4)", nullable: false),
                    DosisMaxima = table.Column<decimal>(type: "decimal(10,4)", nullable: false),
                    UnidadDosis = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Insumos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PreciosMercado",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Cultivo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PrecioKg = table.Column<decimal>(type: "decimal(10,4)", nullable: false),
                    Fuente = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FechaPublicacion = table.Column<DateOnly>(type: "date", nullable: false),
                    Vigente = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PreciosMercado", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SyncOperacionesLog",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OperacionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeviceId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EntidadId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntidadTipo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ClientTimestamp = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersionAnterior = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IntentoNumero = table.Column<int>(type: "int", nullable: false),
                    ProcesadoAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ErrorDetalle = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SyncOperacionesLog", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Usuarios",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(254)", maxLength: 254, nullable: false),
                    NombreCompleto = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Cedula = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Telefono = table.Column<string>(type: "nvarchar(15)", maxLength: 15, nullable: true),
                    Rol = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    FechaRegistro = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UltimoAcceso = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Usuarios", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Lotes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FincaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Cultivo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AreaHa = table.Column<decimal>(type: "decimal(10,4)", nullable: false),
                    Latitud = table.Column<double>(type: "float", nullable: true),
                    Longitud = table.Column<double>(type: "float", nullable: true),
                    FechaInicioSiembra = table.Column<DateOnly>(type: "date", nullable: true),
                    Activo = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Lotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Lotes_Fincas_FincaId",
                        column: x => x.FincaId,
                        principalTable: "Fincas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FichasTecnicas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InsumoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContenidoHtml = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UrlDocumento = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    FechaActualizacion = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FichasTecnicas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FichasTecnicas_Insumos_InsumoId",
                        column: x => x.InsumoId,
                        principalTable: "Insumos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PeriodosCarencia",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InsumoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Cultivo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DiasCarencia = table.Column<int>(type: "int", nullable: false),
                    FuenteRegulacion = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PeriodosCarencia", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PeriodosCarencia_Insumos_InsumoId",
                        column: x => x.InsumoId,
                        principalTable: "Insumos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CredencialesPasskey",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CredentialId = table.Column<byte[]>(type: "varbinary(512)", maxLength: 512, nullable: false),
                    PublicKey = table.Column<byte[]>(type: "varbinary(1024)", maxLength: 1024, nullable: false),
                    SignCount = table.Column<long>(type: "bigint", nullable: false),
                    AaGuid = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CredentialType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FechaRegistro = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DispositivoNombre = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Activa = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CredencialesPasskey", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CredencialesPasskey_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SesionesDispositivo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeviceId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    RefreshTokenHash = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Plataforma = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    AppVersion = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    FechaCreacion = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    FechaExpiracion = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Activa = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SesionesDispositivo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SesionesDispositivo_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CostosLote",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LoteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    Categoria = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Descripcion = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Monto = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    AplicacionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CosechaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreadoOffline = table.Column<bool>(type: "bit", nullable: false),
                    ClientTimestamp = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Eliminado = table.Column<bool>(type: "bit", nullable: false),
                    EliminadoAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CostosLote", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CostosLote_Lotes_LoteId",
                        column: x => x.LoteId,
                        principalTable: "Lotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IngresosLote",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LoteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CosechaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    Comprador = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    KgVendidos = table.Column<decimal>(type: "decimal(12,4)", nullable: false),
                    PrecioKg = table.Column<decimal>(type: "decimal(10,4)", nullable: false),
                    TotalVenta = table.Column<decimal>(type: "decimal(12,2)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IngresosLote", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IngresosLote_Lotes_LoteId",
                        column: x => x.LoteId,
                        principalTable: "Lotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Aplicaciones_LoteId",
                table: "Aplicaciones",
                column: "LoteId");

            migrationBuilder.CreateIndex(
                name: "IX_Aplicaciones_LoteId_FechaFinCarencia",
                table: "Aplicaciones",
                columns: new[] { "LoteId", "FechaFinCarencia" });

            migrationBuilder.CreateIndex(
                name: "IX_Cosechas_LoteId",
                table: "Cosechas",
                column: "LoteId");

            migrationBuilder.CreateIndex(
                name: "IX_CostosLote_LoteId_Fecha",
                table: "CostosLote",
                columns: new[] { "LoteId", "Fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_CredencialesPasskey_CredentialId",
                table: "CredencialesPasskey",
                column: "CredentialId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CredencialesPasskey_UsuarioId",
                table: "CredencialesPasskey",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_FichasTecnicas_InsumoId",
                table: "FichasTecnicas",
                column: "InsumoId");

            migrationBuilder.CreateIndex(
                name: "IX_Fincas_UsuarioId",
                table: "Fincas",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_IngresosLote_LoteId_Fecha",
                table: "IngresosLote",
                columns: new[] { "LoteId", "Fecha" });

            migrationBuilder.CreateIndex(
                name: "IX_Insumos_RegistroAgrocalidad",
                table: "Insumos",
                column: "RegistroAgrocalidad",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Lotes_FincaId",
                table: "Lotes",
                column: "FincaId");

            migrationBuilder.CreateIndex(
                name: "IX_PeriodosCarencia_InsumoId_Cultivo",
                table: "PeriodosCarencia",
                columns: new[] { "InsumoId", "Cultivo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PreciosMercado_Cultivo_Vigente",
                table: "PreciosMercado",
                columns: new[] { "Cultivo", "Vigente" });

            migrationBuilder.CreateIndex(
                name: "IX_SesionesDispositivo_UsuarioId_DeviceId",
                table: "SesionesDispositivo",
                columns: new[] { "UsuarioId", "DeviceId" });

            migrationBuilder.CreateIndex(
                name: "IX_SyncOperacionesLog_DeviceId_ProcesadoAt",
                table: "SyncOperacionesLog",
                columns: new[] { "DeviceId", "ProcesadoAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SyncOperacionesLog_OperacionId",
                table: "SyncOperacionesLog",
                column: "OperacionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_Cedula",
                table: "Usuarios",
                column: "Cedula",
                unique: true,
                filter: "[Cedula] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_Email",
                table: "Usuarios",
                column: "Email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Aplicaciones");

            migrationBuilder.DropTable(
                name: "Cosechas");

            migrationBuilder.DropTable(
                name: "CostosLote");

            migrationBuilder.DropTable(
                name: "CredencialesPasskey");

            migrationBuilder.DropTable(
                name: "FichasTecnicas");

            migrationBuilder.DropTable(
                name: "IngresosLote");

            migrationBuilder.DropTable(
                name: "PeriodosCarencia");

            migrationBuilder.DropTable(
                name: "PreciosMercado");

            migrationBuilder.DropTable(
                name: "SesionesDispositivo");

            migrationBuilder.DropTable(
                name: "SyncOperacionesLog");

            migrationBuilder.DropTable(
                name: "Lotes");

            migrationBuilder.DropTable(
                name: "Insumos");

            migrationBuilder.DropTable(
                name: "Usuarios");

            migrationBuilder.DropTable(
                name: "Fincas");
        }
    }
}

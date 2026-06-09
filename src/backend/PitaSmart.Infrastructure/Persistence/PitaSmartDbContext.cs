#nullable enable
using Microsoft.EntityFrameworkCore;
using PitaSmart.Application.Common.Interfaces;
using PitaSmart.Domain.Agroquimicos.Entities;
using PitaSmart.Domain.Aplicaciones.Entities;
using PitaSmart.Domain.Common;
using PitaSmart.Domain.Costos.Entities;
using PitaSmart.Domain.Identidad.Entities;
using PitaSmart.Domain.Sincronizacion.Entities;

namespace PitaSmart.Infrastructure.Persistence;

/// <summary>
/// DbContext principal de PitaSmart. Configura todas las entidades de todos los bounded contexts.
/// Implementa <see cref="IApplicationDbContext"/> para ser inyectado en la capa de aplicación.
/// </summary>
public class PitaSmartDbContext : DbContext, IApplicationDbContext
{
    private readonly ICurrentUserService? _currentUserService;

    public PitaSmartDbContext(
        DbContextOptions<PitaSmartDbContext> options,
        ICurrentUserService? currentUserService = null)
        : base(options)
    {
        _currentUserService = currentUserService;
    }

    // --- Agroquímicos ---
    public DbSet<Insumo> Insumos => Set<Insumo>();
    public DbSet<PeriodoCarencia> PeriodosCarencia => Set<PeriodoCarencia>();
    public DbSet<FichaTecnica> FichasTecnicas => Set<FichaTecnica>();

    // --- Aplicaciones ---
    public DbSet<AplicacionQuimico> Aplicaciones => Set<AplicacionQuimico>();

    // --- Cosecha ---
    public DbSet<Domain.Cosecha.Entities.Cosecha> Cosechas => Set<Domain.Cosecha.Entities.Cosecha>();

    // --- Costos ---
    public DbSet<Finca> Fincas => Set<Finca>();
    public DbSet<Lote> Lotes => Set<Lote>();
    public DbSet<CostoLote> CostosLote => Set<CostoLote>();
    public DbSet<IngresoLote> IngresosLote => Set<IngresoLote>();
    public DbSet<PrecioMercado> PreciosMercado => Set<PrecioMercado>();

    // --- Identidad ---
    public DbSet<Usuario> Usuarios => Set<Usuario>();
    public DbSet<CredencialPasskey> CredencialesPasskey => Set<CredencialPasskey>();
    public DbSet<SesionDispositivo> SesionesDispositivo => Set<SesionDispositivo>();

    // --- Sincronización ---
    public DbSet<OperacionPendiente> OperacionesPendientes => Set<OperacionPendiente>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // --- Insumo ---
        modelBuilder.Entity<Insumo>(entity =>
        {
            entity.ToTable("Insumos");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.NombreComercial).HasMaxLength(200).IsRequired();
            entity.Property(e => e.IngredienteActivo).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Fabricante).HasMaxLength(200);
            entity.Property(e => e.RegistroAgrocalidad).HasMaxLength(50);
            entity.HasIndex(e => e.RegistroAgrocalidad).IsUnique();
            entity.Property(e => e.TipoProducto).HasMaxLength(50);
            entity.Property(e => e.CategoriaToxico).HasMaxLength(10);
            entity.Property(e => e.DosisMinima).HasColumnType("decimal(10,4)");
            entity.Property(e => e.DosisMaxima).HasColumnType("decimal(10,4)");
            entity.Property(e => e.UnidadDosis).HasMaxLength(20);
            entity.Property(e => e.RowVersion).IsRowVersion();
            entity.OwnsOne(e => e.Concentracion, c =>
            {
                c.Property(p => p.Valor).HasColumnName("ConcentracionValor").HasColumnType("decimal(10,4)");
                c.Property(p => p.Unidad).HasColumnName("ConcentracionUnidad").HasMaxLength(50);
            });
            entity.HasMany(e => e.PeriodosCarencia).WithOne(p => p.Insumo).HasForeignKey(p => p.InsumoId);
            entity.HasMany(e => e.FichasTecnicas).WithOne(f => f.Insumo).HasForeignKey(f => f.InsumoId);
        });

        modelBuilder.Entity<PeriodoCarencia>(entity =>
        {
            entity.ToTable("PeriodosCarencia");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Cultivo).HasMaxLength(100).IsRequired();
            entity.Property(e => e.FuenteRegulacion).HasMaxLength(200);
            entity.HasIndex(e => new { e.InsumoId, e.Cultivo }).IsUnique();
        });

        modelBuilder.Entity<FichaTecnica>(entity =>
        {
            entity.ToTable("FichasTecnicas");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UrlDocumento).HasMaxLength(500);
        });

        // --- AplicacionQuimico ---
        modelBuilder.Entity<AplicacionQuimico>(entity =>
        {
            entity.ToTable("Aplicaciones");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.AreaAplicadaHa).HasColumnType("decimal(10,4)");
            entity.Property(e => e.MetodoAplicacion).HasMaxLength(50).IsRequired();
            entity.Property(e => e.OperadorNombre).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Observaciones).HasMaxLength(1000);
            entity.Property(e => e.CostoTotal).HasColumnType("decimal(12,2)");
            entity.Property(e => e.DeviceId).HasMaxLength(100);
            entity.Property(e => e.RowVersion).IsRowVersion();

            entity.OwnsOne(e => e.Dosis, d =>
            {
                d.Property(p => p.Cantidad).HasColumnName("DosisCantidad").HasColumnType("decimal(10,4)").IsRequired();
                d.Property(p => p.Unidad).HasColumnName("DosisUnidad").HasMaxLength(20).IsRequired();
            });

            entity.OwnsOne(e => e.CoordenadasGps, c =>
            {
                c.Property(p => p.Latitud).HasColumnName("GpsLatitud");
                c.Property(p => p.Longitud).HasColumnName("GpsLongitud");
            });

            entity.HasIndex(e => e.LoteId);
            entity.HasIndex(e => new { e.LoteId, e.FechaFinCarencia });
        });

        // --- Cosecha ---
        modelBuilder.Entity<Domain.Cosecha.Entities.Cosecha>(entity =>
        {
            entity.ToTable("Cosechas");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.PesoTotalKg).HasColumnType("decimal(12,4)");
            entity.Property(e => e.CalidadGrado).HasMaxLength(20);
            entity.Property(e => e.Comprador).HasMaxLength(200);
            entity.Property(e => e.PrecioVentaKg).HasColumnType("decimal(10,4)");
            entity.Property(e => e.IngresoTotal).HasColumnType("decimal(12,2)");
            entity.Property(e => e.Observaciones).HasMaxLength(1000);
            entity.Property(e => e.RowVersion).IsRowVersion();
            entity.HasIndex(e => e.LoteId);
        });

        // --- Finca ---
        modelBuilder.Entity<Finca>(entity =>
        {
            entity.ToTable("Fincas");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Nombre).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Provincia).HasMaxLength(100);
            entity.Property(e => e.Canton).HasMaxLength(100);
            entity.Property(e => e.Parroquia).HasMaxLength(100);
            entity.Property(e => e.AreaTotalHa).HasColumnType("decimal(10,4)");
            entity.HasIndex(e => e.UsuarioId);
            entity.HasMany(e => e.Lotes).WithOne(l => l.Finca).HasForeignKey(l => l.FincaId);
        });

        // --- Lote ---
        modelBuilder.Entity<Lote>(entity =>
        {
            entity.ToTable("Lotes");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Nombre).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Cultivo).HasMaxLength(100);
            entity.Property(e => e.AreaHa).HasColumnType("decimal(10,4)");
            entity.Property(e => e.RowVersion).IsRowVersion();
            entity.HasIndex(e => e.FincaId);
        });

        // --- CostoLote ---
        modelBuilder.Entity<CostoLote>(entity =>
        {
            entity.ToTable("CostosLote");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Categoria).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Descripcion).HasMaxLength(500);
            entity.Property(e => e.Monto).HasColumnType("decimal(12,2)");
            entity.Property(e => e.RowVersion).IsRowVersion();
            entity.HasIndex(e => new { e.LoteId, e.Fecha });
            entity.HasQueryFilter(e => !e.Eliminado); // Global filter: soft deletes excluidos por defecto.
        });

        // --- IngresoLote ---
        modelBuilder.Entity<IngresoLote>(entity =>
        {
            entity.ToTable("IngresosLote");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Comprador).HasMaxLength(200);
            entity.Property(e => e.KgVendidos).HasColumnType("decimal(12,4)");
            entity.Property(e => e.PrecioKg).HasColumnType("decimal(10,4)");
            entity.Property(e => e.TotalVenta).HasColumnType("decimal(12,2)");
            entity.HasIndex(e => new { e.LoteId, e.Fecha });
        });

        // --- PrecioMercado ---
        modelBuilder.Entity<PrecioMercado>(entity =>
        {
            entity.ToTable("PreciosMercado");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Cultivo).HasMaxLength(100);
            entity.Property(e => e.PrecioKg).HasColumnType("decimal(10,4)");
            entity.Property(e => e.Fuente).HasMaxLength(200);
            entity.HasIndex(e => new { e.Cultivo, e.Vigente });
        });

        // --- Usuario ---
        modelBuilder.Entity<Usuario>(entity =>
        {
            entity.ToTable("Usuarios");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Email).HasMaxLength(254).IsRequired();
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.NombreCompleto).HasMaxLength(300);
            entity.Property(e => e.Cedula).HasMaxLength(10);
            entity.HasIndex(e => e.Cedula).IsUnique().HasFilter("[Cedula] IS NOT NULL");
            entity.Property(e => e.Telefono).HasMaxLength(15);
            entity.Property(e => e.Rol).HasMaxLength(20);
            entity.HasMany(e => e.Credenciales).WithOne(c => c.Usuario).HasForeignKey(c => c.UsuarioId);
            entity.HasMany(e => e.Sesiones).WithOne(s => s.Usuario).HasForeignKey(s => s.UsuarioId);
        });

        // --- CredencialPasskey ---
        modelBuilder.Entity<CredencialPasskey>(entity =>
        {
            entity.ToTable("CredencialesPasskey");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CredentialId).HasMaxLength(512);
            entity.HasIndex(e => e.CredentialId).IsUnique();
            entity.Property(e => e.PublicKey).HasMaxLength(1024);
            entity.Property(e => e.CredentialType).HasMaxLength(50);
            entity.Property(e => e.DispositivoNombre).HasMaxLength(200);
        });

        // --- SesionDispositivo ---
        modelBuilder.Entity<SesionDispositivo>(entity =>
        {
            entity.ToTable("SesionesDispositivo");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.DeviceId).HasMaxLength(100);
            entity.Property(e => e.RefreshTokenHash).HasMaxLength(500);
            entity.Property(e => e.Plataforma).HasMaxLength(50);
            entity.Property(e => e.AppVersion).HasMaxLength(20);
            entity.HasIndex(e => new { e.UsuarioId, e.DeviceId });
        });

        // --- OperacionPendiente ---
        modelBuilder.Entity<OperacionPendiente>(entity =>
        {
            entity.ToTable("SyncOperacionesLog");
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.OperacionId).IsUnique();
            entity.Property(e => e.DeviceId).HasMaxLength(100);
            entity.Property(e => e.EntidadTipo).HasMaxLength(50);
            entity.Property(e => e.ErrorDetalle).HasMaxLength(2000);
            entity.Property(e => e.Tipo).HasConversion<string>().HasMaxLength(50);
            entity.Property(e => e.Estado).HasConversion<string>().HasMaxLength(20);
            entity.HasIndex(e => new { e.DeviceId, e.ProcesadoAt });
        });
    }

    /// <summary>
    /// Intercepta SaveChanges para llenar automáticamente campos de auditoría.
    /// </summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var userId = _currentUserService?.Email ?? "system";

        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    entry.Entity.UpdatedAt = now;
                    entry.Entity.CreatedBy = userId;
                    entry.Entity.UpdatedBy = userId;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    entry.Entity.UpdatedBy = userId;
                    break;
            }
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
}

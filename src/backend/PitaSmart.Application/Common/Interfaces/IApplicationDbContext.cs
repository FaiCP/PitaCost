#nullable enable
using Microsoft.EntityFrameworkCore;
using PitaSmart.Domain.Agroquimicos.Entities;
using PitaSmart.Domain.Aplicaciones.Entities;
using PitaSmart.Domain.Costos.Entities;
using PitaSmart.Domain.Identidad.Entities;

namespace PitaSmart.Application.Common.Interfaces;

/// <summary>
/// Abstracción del DbContext para la capa de aplicación.
/// Expone DbSets para consultas directas y SaveChangesAsync para persistencia.
/// </summary>
public interface IApplicationDbContext
{
    DbSet<Finca> Fincas { get; }
    DbSet<Lote> Lotes { get; }
    DbSet<CostoLote> CostosLote { get; }
    DbSet<Insumo> Insumos { get; }
    DbSet<PeriodoCarencia> PeriodosCarencia { get; }
    DbSet<AplicacionQuimico> Aplicaciones { get; }
    DbSet<Domain.Cosecha.Entities.Cosecha> Cosechas { get; }
    DbSet<Usuario> Usuarios { get; }

    /// <summary>Guarda todos los cambios pendientes en la base de datos.</summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

#nullable enable
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PitaSmart.Application.Common.Interfaces;
using PitaSmart.Domain.Agroquimicos.Interfaces;
using PitaSmart.Domain.Aplicaciones.Interfaces;
using PitaSmart.Domain.Costos.Interfaces;
using PitaSmart.Domain.Cosecha.Interfaces;
using PitaSmart.Domain.Identidad.Interfaces;
using PitaSmart.Domain.Sincronizacion.Interfaces;
using PitaSmart.Infrastructure.Identity;
using PitaSmart.Infrastructure.Persistence;
using PitaSmart.Infrastructure.Persistence.Repositories;

namespace PitaSmart.Infrastructure;

/// <summary>
/// Registro de servicios de la capa de infraestructura en el contenedor DI.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Agrega todos los servicios de infraestructura: DbContext, repositorios, identity, etc.
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // --- EF Core con SQL Server ---
        services.AddDbContext<PitaSmartDbContext>(options =>
        {
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"),
                sqlOptions =>
                {
                    sqlOptions.MigrationsAssembly(typeof(PitaSmartDbContext).Assembly.FullName);
                    sqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(10),
                        errorNumbersToAdd: null);
                });
        });

        services.AddScoped<IApplicationDbContext>(provider =>
            provider.GetRequiredService<PitaSmartDbContext>());

        // --- Repositorios ---
        services.AddScoped<IAplicacionRepository, AplicacionRepository>();
        services.AddScoped<IInsumoRepository, InsumoRepository>();
        services.AddScoped<ILoteRepository, LoteRepository>();
        services.AddScoped<ICostoRepository, CostoRepository>();
        services.AddScoped<ISyncRepository, SyncRepository>();
        services.AddScoped<ICosechaRepository, CosechaRepository>();
        services.AddScoped<IUsuarioRepository, UsuarioRepository>();

        // --- Identity & Auth ---
        services.AddScoped<JwtTokenService>();
        services.AddScoped<Microsoft.AspNetCore.Identity.IPasswordHasher<PitaSmart.Domain.Identidad.Entities.Usuario>,
                           Microsoft.AspNetCore.Identity.PasswordHasher<PitaSmart.Domain.Identidad.Entities.Usuario>>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();

        return services;
    }
}

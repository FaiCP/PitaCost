#nullable enable
using System.Reflection;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using PitaSmart.Application.Common.Behaviors;

namespace PitaSmart.Application;

/// <summary>
/// Registro de servicios de la capa de aplicacion en el contenedor DI.
/// Registra MediatR (handlers + behaviors) y FluentValidation (validadores).
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Agrega MediatR con pipeline behaviors y FluentValidation al contenedor de servicios.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        // MediatR: registra todos los handlers del assembly de Application.
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
        });

        // FluentValidation: registra todos los validadores del assembly de Application.
        services.AddValidatorsFromAssembly(assembly);

        return services;
    }
}

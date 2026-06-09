#nullable enable
using FluentValidation;
using PitaSmart.Domain.Agroquimicos.Interfaces;
using PitaSmart.Domain.Costos.Interfaces;

namespace PitaSmart.Application.Features.Aplicaciones.Commands.RegistrarAplicacion;

/// <summary>
/// Validador FluentValidation para <see cref="RegistrarAplicacionCommand"/>.
/// Valida reglas de contrato API y existencia de entidades referenciadas.
/// </summary>
public class RegistrarAplicacionValidator : AbstractValidator<RegistrarAplicacionCommand>
{
    private static readonly string[] MetodosPermitidos =
        ["FUMIGACION", "DRENCH", "INYECCION", "GRANULAR", "OTRO"];

    private static readonly string[] UnidadesPermitidas =
        ["L_HA", "KG_HA", "ML_HA", "G_HA", "CC_HA"];

    public RegistrarAplicacionValidator(
        IInsumoRepository insumoRepository,
        ILoteRepository loteRepository)
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("El ID de la aplicacion es requerido.");

        RuleFor(x => x.LoteId)
            .NotEmpty().WithMessage("El ID del lote es requerido.")
            .MustAsync(async (loteId, ct) => await loteRepository.ExistsAsync(loteId, ct))
            .WithMessage("El lote especificado no existe.");

        RuleFor(x => x.InsumoId)
            .NotEmpty().WithMessage("El ID del insumo es requerido.")
            .MustAsync(async (insumoId, ct) => await insumoRepository.ExistsAsync(insumoId, ct))
            .WithMessage("El insumo especificado no existe en el catalogo.");

        RuleFor(x => x.FechaAplicacion)
            .NotEmpty().WithMessage("La fecha de aplicacion es requerida.")
            .Must(fecha => fecha <= DateTimeOffset.UtcNow.AddHours(1))
            .WithMessage("La fecha de aplicacion no puede ser futura (tolerancia maxima: +1 hora).")
            .Must(fecha => fecha >= DateTimeOffset.UtcNow.AddDays(-30))
            .WithMessage("La fecha de aplicacion no puede ser anterior a 30 dias.");

        RuleFor(x => x.Dosis)
            .NotNull().WithMessage("La dosis es requerida.");

        When(x => x.Dosis is not null, () =>
        {
            RuleFor(x => x.Dosis.Cantidad)
                .GreaterThan(0).WithMessage("La dosis debe ser mayor a 0.");

            RuleFor(x => x.Dosis.Unidad)
                .NotEmpty().WithMessage("La unidad de dosis es requerida.")
                .Must(u => UnidadesPermitidas.Contains(u))
                .WithMessage($"La unidad de dosis debe ser una de: {string.Join(", ", UnidadesPermitidas)}.");
        });

        RuleFor(x => x.AreaAplicadaHa)
            .GreaterThan(0).WithMessage("El area aplicada debe ser mayor a 0.");

        RuleFor(x => x.MetodoAplicacion)
            .NotEmpty().WithMessage("El metodo de aplicacion es requerido.")
            .Must(m => MetodosPermitidos.Contains(m))
            .WithMessage($"El metodo debe ser uno de: {string.Join(", ", MetodosPermitidos)}.");

        RuleFor(x => x.OperadorNombre)
            .NotEmpty().WithMessage("El nombre del operador es requerido.")
            .MinimumLength(2).WithMessage("El nombre del operador debe tener al menos 2 caracteres.")
            .MaximumLength(200).WithMessage("El nombre del operador no puede exceder 200 caracteres.");

        When(x => x.CoordenadasGps is not null, () =>
        {
            RuleFor(x => x.CoordenadasGps!.Latitud)
                .InclusiveBetween(-5, 2)
                .WithMessage("La latitud debe estar entre -5 y 2 (rango Ecuador).");

            RuleFor(x => x.CoordenadasGps!.Longitud)
                .InclusiveBetween(-92, -75)
                .WithMessage("La longitud debe estar entre -92 y -75 (rango Ecuador).");
        });

        RuleFor(x => x.CostoTotal)
            .GreaterThanOrEqualTo(0).WithMessage("El costo total no puede ser negativo.");

        RuleFor(x => x.Observaciones)
            .MaximumLength(1000).WithMessage("Las observaciones no pueden exceder 1000 caracteres.");

        RuleFor(x => x.ClientTimestamp)
            .NotEmpty().WithMessage("El timestamp del cliente es requerido.");
    }
}

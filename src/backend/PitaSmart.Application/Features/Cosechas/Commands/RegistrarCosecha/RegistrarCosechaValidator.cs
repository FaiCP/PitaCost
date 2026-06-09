#nullable enable
using FluentValidation;
using PitaSmart.Domain.Costos.Interfaces;

namespace PitaSmart.Application.Features.Cosechas.Commands.RegistrarCosecha;

/// <summary>
/// Validador FluentValidation para <see cref="RegistrarCosechaCommand"/>.
/// Valida reglas de contrato API antes de que el handler procese la logica de negocio.
/// La validacion del Periodo de Carencia se realiza en el handler (requiere consulta a BD).
/// </summary>
public class RegistrarCosechaValidator : AbstractValidator<RegistrarCosechaCommand>
{
    /// <summary>Grados de calidad validos segun contrato.</summary>
    private static readonly string[] CalidadesPermitidas =
        ["PREMIUM", "PRIMERA", "SEGUNDA", "RECHAZO"];

    public RegistrarCosechaValidator(ILoteRepository loteRepository)
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("El ID de la cosecha es requerido.");

        RuleFor(x => x.LoteId)
            .NotEmpty().WithMessage("El ID del lote es requerido.")
            .MustAsync(async (loteId, ct) => await loteRepository.ExistsAsync(loteId, ct))
            .WithMessage("El lote especificado no existe.");

        RuleFor(x => x.FechaCosecha)
            .NotEmpty().WithMessage("La fecha de cosecha es requerida.")
            .Must(fecha => fecha <= DateTimeOffset.UtcNow.AddHours(1))
            .WithMessage("La fecha de cosecha no puede ser futura (tolerancia maxima: +1 hora).");

        RuleFor(x => x.PesoTotalKg)
            .GreaterThan(0).WithMessage("El peso total debe ser mayor a 0 kg.");

        RuleFor(x => x.CalidadGrado)
            .NotEmpty().WithMessage("El grado de calidad es requerido.")
            .Must(c => CalidadesPermitidas.Contains(c))
            .WithMessage($"El grado de calidad debe ser uno de: {string.Join(", ", CalidadesPermitidas)}.");

        RuleFor(x => x.PrecioVentaKg)
            .GreaterThanOrEqualTo(0)
            .When(x => x.PrecioVentaKg.HasValue)
            .WithMessage("El precio de venta por kg no puede ser negativo.");

        RuleFor(x => x.Comprador)
            .MaximumLength(200)
            .WithMessage("El nombre del comprador no puede exceder 200 caracteres.");

        RuleFor(x => x.Observaciones)
            .MaximumLength(1000)
            .WithMessage("Las observaciones no pueden exceder 1000 caracteres.");

        RuleFor(x => x.ClientTimestamp)
            .NotEmpty().WithMessage("El timestamp del cliente es requerido.");
    }
}

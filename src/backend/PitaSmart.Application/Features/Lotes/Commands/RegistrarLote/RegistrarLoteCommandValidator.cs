#nullable enable
using FluentValidation;

namespace PitaSmart.Application.Features.Lotes.Commands.RegistrarLote;

/// <summary>
/// Validador FluentValidation para <see cref="RegistrarLoteCommand"/>.
/// </summary>
public class RegistrarLoteCommandValidator : AbstractValidator<RegistrarLoteCommand>
{
    public RegistrarLoteCommandValidator()
    {
        RuleFor(x => x.FincaId)
            .NotEmpty()
            .WithMessage("El ID de la finca es requerido.");

        RuleFor(x => x.Nombre)
            .NotEmpty()
            .WithMessage("El nombre del lote es requerido.")
            .MaximumLength(200)
            .WithMessage("El nombre no puede superar los 200 caracteres.");

        RuleFor(x => x.Cultivo)
            .NotEmpty()
            .WithMessage("El cultivo es requerido.")
            .MaximumLength(100)
            .WithMessage("El cultivo no puede superar los 100 caracteres.");

        RuleFor(x => x.AreaHa)
            .GreaterThan(0)
            .WithMessage("El área debe ser mayor a 0.")
            .LessThanOrEqualTo(99_999.9999m)
            .WithMessage("El área excede el límite permitido.");

        RuleFor(x => x.Latitud)
            .InclusiveBetween(-90, 90)
            .WithMessage("La latitud debe estar entre -90 y 90.")
            .When(x => x.Latitud.HasValue);

        RuleFor(x => x.Longitud)
            .InclusiveBetween(-180, 180)
            .WithMessage("La longitud debe estar entre -180 y 180.")
            .When(x => x.Longitud.HasValue);
    }
}

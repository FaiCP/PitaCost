#nullable enable
using FluentValidation;

namespace PitaSmart.Application.Features.Fincas.Commands.RegistrarFinca;

/// <summary>
/// Validador FluentValidation para <see cref="RegistrarFincaCommand"/>.
/// </summary>
public class RegistrarFincaCommandValidator : AbstractValidator<RegistrarFincaCommand>
{
    public RegistrarFincaCommandValidator()
    {
        RuleFor(x => x.Nombre)
            .NotEmpty()
            .WithMessage("El nombre de la finca es requerido.")
            .MaximumLength(200)
            .WithMessage("El nombre no puede superar los 200 caracteres.");

        RuleFor(x => x.Provincia)
            .NotEmpty()
            .WithMessage("La provincia es requerida.")
            .MaximumLength(100)
            .WithMessage("La provincia no puede superar los 100 caracteres.");

        RuleFor(x => x.Canton)
            .NotEmpty()
            .WithMessage("El cantón es requerido.")
            .MaximumLength(100)
            .WithMessage("El cantón no puede superar los 100 caracteres.");

        RuleFor(x => x.Parroquia)
            .MaximumLength(100)
            .WithMessage("La parroquia no puede superar los 100 caracteres.")
            .When(x => x.Parroquia is not null);

        RuleFor(x => x.AreaTotalHa)
            .GreaterThan(0)
            .WithMessage("El área total debe ser mayor a 0.")
            .LessThanOrEqualTo(99_999.9999m)
            .WithMessage("El área total excede el límite permitido.");
    }
}

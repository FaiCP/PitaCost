#nullable enable
using FluentValidation;

namespace PitaSmart.Application.Features.Costos.Commands.ActualizarCosto;

/// <summary>
/// Validador FluentValidation para <see cref="ActualizarCostoCommand"/>.
/// </summary>
public class ActualizarCostoCommandValidator : AbstractValidator<ActualizarCostoCommand>
{
    private static readonly string[] CategoriasValidas =
    [
        "INSUMOS_QUIMICOS", "MANO_DE_OBRA", "TRANSPORTE",
        "RIEGO", "MAQUINARIA", "OTROS"
    ];

    public ActualizarCostoCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("El ID del costo es requerido.");

        RuleFor(x => x.Descripcion)
            .NotEmpty()
            .WithMessage("La descripción es requerida.")
            .MaximumLength(500)
            .WithMessage("La descripción no puede superar los 500 caracteres.");

        RuleFor(x => x.Monto)
            .GreaterThan(0)
            .WithMessage("El monto debe ser mayor a 0.")
            .LessThanOrEqualTo(9_999_999.99m)
            .WithMessage("El monto excede el límite permitido.");

        RuleFor(x => x.Categoria)
            .NotEmpty()
            .WithMessage("La categoría es requerida.")
            .Must(c => CategoriasValidas.Contains(c))
            .WithMessage($"Categoría inválida. Valores permitidos: {string.Join(", ", CategoriasValidas)}.");

        RuleFor(x => x.Fecha)
            .NotEmpty()
            .WithMessage("La fecha del costo es requerida.")
            .Must(f => f <= DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)))
            .WithMessage("La fecha del costo no puede ser futura.");
    }
}

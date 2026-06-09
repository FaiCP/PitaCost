#nullable enable
using FluentValidation;

namespace PitaSmart.Application.Features.Usuarios.Commands.RegistrarUsuario;

/// <summary>
/// Validador FluentValidation para <see cref="RegistrarUsuarioCommand"/>.
/// </summary>
public class RegistrarUsuarioCommandValidator : AbstractValidator<RegistrarUsuarioCommand>
{
    public RegistrarUsuarioCommandValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage("El email es requerido.")
            .EmailAddress()
            .WithMessage("El formato del email es inválido.")
            .MaximumLength(254)
            .WithMessage("El email no puede superar los 254 caracteres.");

        RuleFor(x => x.NombreCompleto)
            .NotEmpty()
            .WithMessage("El nombre completo es requerido.")
            .MaximumLength(300)
            .WithMessage("El nombre completo no puede superar los 300 caracteres.");

        RuleFor(x => x.Cedula)
            .MaximumLength(10)
            .WithMessage("La cédula no puede superar los 10 caracteres.")
            .When(x => x.Cedula is not null);

        RuleFor(x => x.Telefono)
            .MaximumLength(15)
            .WithMessage("El teléfono no puede superar los 15 caracteres.")
            .When(x => x.Telefono is not null);
    }
}

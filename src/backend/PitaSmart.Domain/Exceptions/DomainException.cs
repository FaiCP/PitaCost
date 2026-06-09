#nullable enable
namespace PitaSmart.Domain.Exceptions;

/// <summary>
/// Excepción base para violaciones de reglas de negocio del dominio.
/// </summary>
public class DomainException : Exception
{
    /// <summary>Código de error estructurado para el cliente.</summary>
    public string Code { get; }

    public DomainException(string code, string message) : base(message)
    {
        Code = code;
    }

    public DomainException(string code, string message, Exception innerException)
        : base(message, innerException)
    {
        Code = code;
    }
}

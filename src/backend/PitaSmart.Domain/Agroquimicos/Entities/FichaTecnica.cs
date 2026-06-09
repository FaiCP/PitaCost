#nullable enable
using PitaSmart.Domain.Common;

namespace PitaSmart.Domain.Agroquimicos.Entities;

/// <summary>
/// Ficha técnica de un insumo agroquímico.
/// </summary>
public class FichaTecnica : BaseEntity
{
    public Guid InsumoId { get; private set; }
    public string ContenidoHtml { get; private set; } = string.Empty;
    public string? UrlDocumento { get; private set; }
    public DateTimeOffset FechaActualizacion { get; private set; }
    public Insumo Insumo { get; private set; } = null!;

    private FichaTecnica() { }
}

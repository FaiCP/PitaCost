#nullable enable
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using PitaSmart.Application.Common.Interfaces;

namespace PitaSmart.Infrastructure.Identity;

/// <summary>
/// Implementación de <see cref="ICurrentUserService"/> que extrae datos del usuario
/// desde los claims JWT del HttpContext.
/// </summary>
public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc />
    public Guid UserId
    {
        get
        {
            var sub = _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? _httpContextAccessor.HttpContext?.User.FindFirstValue("sub");
            return Guid.TryParse(sub, out var id) ? id : Guid.Empty;
        }
    }

    /// <inheritdoc />
    public string Email =>
        _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.Email)
        ?? _httpContextAccessor.HttpContext?.User.FindFirstValue("email")
        ?? string.Empty;

    /// <inheritdoc />
    public string Role =>
        _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.Role) ?? "AGRICULTOR";

    /// <inheritdoc />
    public IReadOnlyList<Guid> FincaIds =>
        _httpContextAccessor.HttpContext?.User.FindAll("finca_ids")
            .Select(c => Guid.TryParse(c.Value, out var id) ? id : Guid.Empty)
            .Where(id => id != Guid.Empty)
            .ToList()
        ?? [];
}

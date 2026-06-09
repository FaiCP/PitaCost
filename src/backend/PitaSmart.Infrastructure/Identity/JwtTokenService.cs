#nullable enable
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using PitaSmart.Domain.Identidad.Entities;

namespace PitaSmart.Infrastructure.Identity;

/// <summary>
/// Servicio de generación de tokens JWT según los claims definidos en el contrato API.
/// </summary>
public class JwtTokenService
{
    private readonly IConfiguration _configuration;

    public JwtTokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>
    /// Genera un access token JWT para el usuario autenticado.
    /// Claims: sub, email, name, role, finca_ids, iat, exp, iss, aud.
    /// </summary>
    public string GenerarAccessToken(Usuario usuario, IEnumerable<Guid> fincaIds)
    {
        var jwtSettings = _configuration.GetSection("Jwt");
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtSettings["Key"]
                ?? throw new InvalidOperationException("JWT Key no configurada.")));

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, usuario.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, usuario.Email),
            new("name", usuario.NombreCompleto),
            new(ClaimTypes.Role, usuario.Rol),
            new(JwtRegisteredClaimNames.Iat,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
                ClaimValueTypes.Integer64)
        };

        foreach (var fincaId in fincaIds)
        {
            claims.Add(new Claim("finca_ids", fincaId.ToString()));
        }

        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: jwtSettings["Issuer"] ?? "https://api.pitasmart.ec",
            audience: jwtSettings["Audience"] ?? "pitasmart-app",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Genera un refresh token opaco con prefijo para identificación.
    /// </summary>
    public string GenerarRefreshToken()
    {
        var randomBytes = new byte[32];
        using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return $"rt-{Convert.ToBase64String(randomBytes).Replace("+", "-").Replace("/", "_").TrimEnd('=')}";
    }
}

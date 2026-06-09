#nullable enable
using Microsoft.EntityFrameworkCore;
using PitaSmart.Domain.Identidad.Entities;
using PitaSmart.Domain.Identidad.Interfaces;

namespace PitaSmart.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementación EF Core del repositorio de usuarios.
/// </summary>
public class UsuarioRepository : IUsuarioRepository
{
    private readonly PitaSmartDbContext _context;

    public UsuarioRepository(PitaSmartDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc />
    public async Task<Usuario?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Usuarios.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Usuario?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await _context.Usuarios.FirstOrDefaultAsync(
            u => u.Email == email, cancellationToken);
    }

    /// <inheritdoc />
    public async Task AddUsuarioAsync(Usuario usuario, CancellationToken cancellationToken = default)
    {
        await _context.Usuarios.AddAsync(usuario, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<CredencialPasskey?> GetCredencialByIdAsync(byte[] credentialId, CancellationToken cancellationToken = default)
    {
        return await _context.CredencialesPasskey
            .FirstOrDefaultAsync(c => c.CredentialId == credentialId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task AddCredencialAsync(CredencialPasskey credencial, CancellationToken cancellationToken = default)
    {
        await _context.CredencialesPasskey.AddAsync(credencial, cancellationToken);
    }

    /// <inheritdoc />
    public async Task AddSesionAsync(SesionDispositivo sesion, CancellationToken cancellationToken = default)
    {
        await _context.SesionesDispositivo.AddAsync(sesion, cancellationToken);
    }
}

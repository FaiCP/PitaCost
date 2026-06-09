#nullable enable
using PitaSmart.Domain.Identidad.Entities;

namespace PitaSmart.Domain.Identidad.Interfaces;

/// <summary>
/// Repositorio de usuarios y credenciales.
/// </summary>
public interface IUsuarioRepository
{
    Task<Usuario?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Usuario?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task AddUsuarioAsync(Usuario usuario, CancellationToken cancellationToken = default);
    Task<CredencialPasskey?> GetCredencialByIdAsync(byte[] credentialId, CancellationToken cancellationToken = default);
    Task AddCredencialAsync(CredencialPasskey credencial, CancellationToken cancellationToken = default);
    Task AddSesionAsync(SesionDispositivo sesion, CancellationToken cancellationToken = default);
}

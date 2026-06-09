#nullable enable
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace PitaSmart.Infrastructure.Realtime;

/// <summary>
/// Hub SignalR para notificación en tiempo real de precios de mercado.
/// Los clientes conectados reciben actualizaciones cuando se publican nuevos precios.
/// </summary>
[Authorize]
public class PreciosMercadoHub : Hub
{
    /// <summary>
    /// El cliente se suscribe a un cultivo específico para recibir actualizaciones de precios.
    /// </summary>
    /// <param name="cultivo">Nombre del cultivo (Banano, Cacao, etc.).</param>
    public async Task SuscribirCultivo(string cultivo)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"precios-{cultivo.ToLowerInvariant()}");
    }

    /// <summary>
    /// El cliente se desuscribe de un cultivo.
    /// </summary>
    public async Task DesuscribirCultivo(string cultivo)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"precios-{cultivo.ToLowerInvariant()}");
    }
}

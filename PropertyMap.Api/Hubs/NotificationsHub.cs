using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace PropertyMap.Api.Hubs;

[Authorize]
public class NotificationsHub : Hub
{
    // El hub no expone métodos invocables por el cliente todavía;
    // solo recibe pushes del servidor vía IHubContext<NotificationsHub>.
    // SignalR enruta por usuario usando ClaimTypes.NameIdentifier (default IUserIdProvider).
}

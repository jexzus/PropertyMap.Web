using Microsoft.AspNetCore.SignalR;
using PropertyMap.Api.Hubs;
using PropertyMap.Core.DTOs.Notifications;
using PropertyMap.Core.Interfaces;

namespace PropertyMap.Api.Services;

public class SignalRNotificationPublisher(IHubContext<NotificationsHub> hub) : INotificationPublisher
{
    public async Task PublishToUserAsync(string userId, NotificationDto notification) =>
        await hub.Clients.User(userId).SendAsync("ReceiveNotification", notification);
}

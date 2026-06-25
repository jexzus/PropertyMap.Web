using PropertyMap.Core.DTOs.Notifications;

namespace PropertyMap.Core.Interfaces;

public interface INotificationPublisher
{
    Task PublishToUserAsync(string userId, NotificationDto notification);
}

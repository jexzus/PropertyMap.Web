using PropertyMap.Core.DTOs.Notifications;
using PropertyMap.Core.Entities;

namespace PropertyMap.Core.Interfaces;

public interface INotificationRepository
{
    Task<Notification> AddAsync(Notification notification);
    Task<List<NotificationDto>> GetByUserAsync(string userId, int take = 20);
    Task<int> GetUnreadCountAsync(string userId);
    Task MarkAsReadAsync(int id, string userId);
    Task MarkAllAsReadAsync(string userId);
}

using PropertyMap.Core.DTOs.Notifications;

namespace PropertyMap.Web.Services;

public interface INotificationsApiService
{
    Task<List<NotificationDto>> GetMyNotificationsAsync(int take = 20);
    Task<int> GetUnreadCountAsync();
    Task MarkAsReadAsync(int id);
    Task MarkAllAsReadAsync();
}

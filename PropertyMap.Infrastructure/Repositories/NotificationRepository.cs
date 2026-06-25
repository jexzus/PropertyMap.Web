using Microsoft.EntityFrameworkCore;
using PropertyMap.Core.DTOs.Notifications;
using PropertyMap.Core.Entities;
using PropertyMap.Core.Interfaces;
using PropertyMap.Infrastructure.Data;

namespace PropertyMap.Infrastructure.Repositories;

public class NotificationRepository(AppDbContext ctx) : INotificationRepository
{
    public async Task<Notification> AddAsync(Notification notification)
    {
        ctx.Notifications.Add(notification);
        await ctx.SaveChangesAsync();
        return notification;
    }

    public async Task<List<NotificationDto>> GetByUserAsync(string userId, int take = 20) =>
        await ctx.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.FechaCreacion)
            .Take(take)
            .Select(n => new NotificationDto(
                n.Id, n.Tipo, n.Titulo, n.Mensaje, n.Leida, n.UrlAccion, n.FechaCreacion))
            .ToListAsync();

    public async Task<int> GetUnreadCountAsync(string userId) =>
        await ctx.Notifications.CountAsync(n => n.UserId == userId && !n.Leida);

    public async Task MarkAsReadAsync(int id, string userId)
    {
        var notification = await ctx.Notifications
            .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);
        if (notification is not null)
        {
            notification.Leida = true;
            await ctx.SaveChangesAsync();
        }
    }

    public async Task MarkAllAsReadAsync(string userId)
    {
        var unread = await ctx.Notifications
            .Where(n => n.UserId == userId && !n.Leida)
            .ToListAsync();
        foreach (var n in unread) n.Leida = true;
        await ctx.SaveChangesAsync();
    }
}

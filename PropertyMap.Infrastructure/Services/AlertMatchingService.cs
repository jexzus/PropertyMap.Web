using Microsoft.EntityFrameworkCore;
using PropertyMap.Core.DTOs.Notifications;
using PropertyMap.Core.Entities;
using PropertyMap.Core.Enums;
using PropertyMap.Core.Interfaces;
using PropertyMap.Infrastructure.Data;

namespace PropertyMap.Infrastructure.Services;

public class AlertMatchingService(
    AppDbContext ctx,
    IAlertRepository alerts,
    IEmailService email,
    INotificationPublisher publisher) : IAlertMatchingService
{
    public async Task NotifyMatchingAlertsAsync(PropertyListing listing)
    {
        var matches = await alerts.GetActiveMatchingAsync(listing);
        if (matches.Count == 0) return;

        var preferences = await ctx.NotificationPreferences
            .Where(p => matches.Select(m => m.UserId).Contains(p.UserId))
            .ToListAsync();

        foreach (var alert in matches)
        {
            var pref = preferences.FirstOrDefault(p => p.UserId == alert.UserId);

            var notification = new Notification
            {
                UserId = alert.UserId,
                Tipo = TipoNotificacion.AlertaCoincidencia,
                Titulo = "¡Nueva propiedad para tu alerta!",
                Mensaje = $"{listing.Titulo} coincide con tu alerta \"{alert.Nombre ?? "sin nombre"}\".",
                UrlAccion = $"/propiedad/{listing.Id}",
                FechaCreacion = DateTime.UtcNow
            };
            ctx.Notifications.Add(notification);
            await ctx.SaveChangesAsync();

            if (pref is null || pref.RecibirPush)
            {
                await publisher.PublishToUserAsync(alert.UserId, new NotificationDto(
                    notification.Id, notification.Tipo, notification.Titulo,
                    notification.Mensaje, notification.Leida, notification.UrlAccion,
                    notification.FechaCreacion));
            }

            if ((pref is null || pref.RecibirEmail) && (pref is null || pref.AlertasCoincidencia)
                && alert.User?.Email is not null)
            {
                await email.SendAlertMatchAsync(
                    alert.User.Email, alert.User.Nombre,
                    alert.Nombre ?? "tu búsqueda", listing.Titulo, listing.Id);
            }
        }
    }
}

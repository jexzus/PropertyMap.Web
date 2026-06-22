using Microsoft.EntityFrameworkCore;
using PropertyMap.Core.Entities;
using PropertyMap.Core.Interfaces;
using PropertyMap.Infrastructure.Data;

namespace PropertyMap.Infrastructure.Services;

public class ViewTrackingService(AppDbContext ctx) : IViewTrackingService
{
    public async Task TrackViewAsync(int listingId, string? userId, string ipAddress, DateOnly date)
    {
        var startOfDay = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var endOfDay = startOfDay.AddDays(1);

        bool alreadyViewed;
        if (userId != null)
        {
            alreadyViewed = await ctx.PropertyViews.AnyAsync(v =>
                v.PropertyListingId == listingId &&
                v.UserId == userId &&
                v.FechaVista >= startOfDay &&
                v.FechaVista < endOfDay);
        }
        else
        {
            alreadyViewed = await ctx.PropertyViews.AnyAsync(v =>
                v.PropertyListingId == listingId &&
                v.IpAddress == ipAddress &&
                v.FechaVista >= startOfDay &&
                v.FechaVista < endOfDay);
        }

        if (!alreadyViewed)
        {
            ctx.PropertyViews.Add(new PropertyView
            {
                PropertyListingId = listingId,
                UserId = userId,
                IpAddress = ipAddress,
                FechaVista = DateTime.UtcNow
            });
            await ctx.SaveChangesAsync();
        }
    }
}

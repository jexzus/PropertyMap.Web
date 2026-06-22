namespace PropertyMap.Core.Interfaces;

public interface IViewTrackingService
{
    Task TrackViewAsync(int listingId, string? userId, string ipAddress, DateOnly date);
}

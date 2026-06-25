using PropertyMap.Core.Entities;

namespace PropertyMap.Core.Interfaces;

public interface IAlertMatchingService
{
    Task NotifyMatchingAlertsAsync(PropertyListing listing);
}

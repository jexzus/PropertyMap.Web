using PropertyMap.Core.Entities;

namespace PropertyMap.Core.Interfaces;

public interface ISubscriptionRepository
{
    Task<Subscription?> GetByUserIdAsync(string userId);
    Task<Subscription> CreateOrReplaceAsync(string userId, int planId, DateTime vencimiento);
}

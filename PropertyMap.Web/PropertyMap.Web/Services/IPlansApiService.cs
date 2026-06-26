using PropertyMap.Core.DTOs.Plans;

namespace PropertyMap.Web.Services;

public interface IPlansApiService
{
    Task<List<PlanDto>> GetActiveAsync();
    Task<SubscriptionDto?> GetMineAsync();
    Task<SubscriptionDto?> SubscribeAsync(int planId);
}

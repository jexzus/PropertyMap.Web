using PropertyMap.Core.DTOs.Ratings;

namespace PropertyMap.Web.Services;

public interface IRatingsApiService
{
    Task<PropertyRatingStatsDto?> RatePropertyAsync(RatePropertyRequest request);
    Task<PropertyRatingStatsDto?> GetPropertyStatsAsync(int listingId);
    Task<AgentRatingStatsDto?> RateAgentAsync(RateAgentRequest request);
    Task<AgentRatingStatsDto?> GetAgentStatsAsync(int publisherId);
    Task<List<AgentRankingItemDto>> GetRankingAsync(string? ciudad = null, int top = 20);
}

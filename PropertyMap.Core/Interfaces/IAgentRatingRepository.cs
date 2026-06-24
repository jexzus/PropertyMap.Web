using PropertyMap.Core.DTOs.Ratings;
using PropertyMap.Core.Entities;

namespace PropertyMap.Core.Interfaces;

public interface IAgentRatingRepository
{
    Task<bool> HasConsultaWithPublisherAsync(int publisherId, string userId);
    Task<AgentRating?> GetByUserAndPublisherAsync(int publisherId, string userId);
    Task AddOrUpdateAsync(AgentRating rating);
    Task<AgentRatingStatsDto> GetStatsAsync(int publisherId);
    Task<List<AgentRankingItemDto>> GetRankingAsync(string? ciudad, int top = 20);
}

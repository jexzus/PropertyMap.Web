using PropertyMap.Core.DTOs.Stats;

namespace PropertyMap.Core.Interfaces;

public interface IListingStatsRepository
{
    Task<ListingStatsDto?> GetForListingAsync(int listingId, int publisherId);
    Task<List<ListingStatsDto>> GetForPublisherAsync(int publisherId);
}

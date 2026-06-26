using PropertyMap.Core.DTOs.Stats;

namespace PropertyMap.Web.Services;

public interface IStatsApiService
{
    Task<List<ListingStatsDto>> GetMineAsync();
}

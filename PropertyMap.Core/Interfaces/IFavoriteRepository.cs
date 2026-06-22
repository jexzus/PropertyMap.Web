using PropertyMap.Core.DTOs.Properties;
using PropertyMap.Core.Entities;

namespace PropertyMap.Core.Interfaces;

public interface IFavoriteRepository
{
    Task AddAsync(PropertyFavorite favorite);
    Task RemoveAsync(int listingId, string userId);
    Task<List<MyListingDto>> GetByUserAsync(string userId);
    Task<bool> IsFavoritedAsync(int listingId, string userId);
    Task<int> GetCountAsync(int listingId);
}

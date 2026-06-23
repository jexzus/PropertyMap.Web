using PropertyMap.Core.DTOs.Properties;

namespace PropertyMap.Web.Services;

public interface IFavoritesApiService
{
    Task<List<MyListingDto>> GetFavoritesAsync();
    Task<bool> ToggleFavoriteAsync(int listingId, bool currentlyFavorited);
    Task<(bool IsFavorited, int Count)> GetStatusAsync(int listingId);
}

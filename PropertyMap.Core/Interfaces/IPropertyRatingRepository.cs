// PropertyMap.Core/Interfaces/IPropertyRatingRepository.cs
using PropertyMap.Core.DTOs.Ratings;
using PropertyMap.Core.Entities;

namespace PropertyMap.Core.Interfaces;

public interface IPropertyRatingRepository
{
    Task<bool> HasConsultaAsync(int listingId, string userId);
    Task<PropertyRating?> GetByUserAndListingAsync(int listingId, string userId);
    Task AddOrUpdateAsync(PropertyRating rating);
    Task<PropertyRatingStatsDto> GetStatsAsync(int listingId);
}

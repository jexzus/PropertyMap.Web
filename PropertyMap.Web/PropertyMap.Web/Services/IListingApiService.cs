using PropertyMap.Core.DTOs;
using PropertyMap.Core.Entities;

namespace PropertyMap.Web.Services;

public interface IListingApiService
{
    Task<IEnumerable<PropertyListing>> GetActiveListingsAsync();
    Task<IEnumerable<ListingMapDto>> GetActiveListingsForMapAsync();
    Task<ListingDetailDto?> GetByIdAsync(int id);
}

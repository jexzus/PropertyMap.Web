using PropertyMap.Core.DTOs;
using PropertyMap.Core.Entities;

namespace PropertyMap.Core.Interfaces;

public interface IListingRepository
{
    Task<IEnumerable<PropertyListing>> GetActiveListingsAsync();
    Task<IEnumerable<PropertyListing>> GetListingsByPublisherAsync(int publisherId);
    Task<IEnumerable<ListingMapDto>> GetActiveListingsForMapAsync();
    Task<PropertyListing?> GetByIdAsync(int id);
    Task<ListingDetailDto?> GetByIdAsDetailAsync(int id);
    Task<PropertyListing> AddAsync(PropertyListing listing);
    Task UpdateAsync(PropertyListing listing);
    Task DeleteAsync(int id);
}

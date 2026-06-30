using PropertyMap.Core.DTOs;
using PropertyMap.Core.DTOs.Admin;
using PropertyMap.Core.DTOs.Properties;
using PropertyMap.Core.Entities;

namespace PropertyMap.Core.Interfaces;

public interface IListingRepository
{
    Task<IEnumerable<PropertyListing>> GetActiveListingsAsync();
    Task<PagedResultDto<PropertyListing>> SearchAsync(
        string? q, string? operacion, string? tipoPropiedad,
        decimal? precioMax, int? dormitoriosMin, int? banosMin,
        int page, int pageSize);
    Task<IEnumerable<PropertyListing>> GetListingsByPublisherAsync(int publisherId);
    Task<IEnumerable<ListingMapDto>> GetActiveListingsForMapAsync();
    Task<PropertyListing?> GetByIdAsync(int id);
    Task<ListingDetailDto?> GetByIdAsDetailAsync(int id);
    Task<IEnumerable<MyListingDto>> GetMyListingsAsync(int publisherId);
    Task<IEnumerable<PendingListingDto>> GetPendingListingsAsync();
    Task<PropertyListing> AddAsync(PropertyListing listing);
    Task UpdateAsync(PropertyListing listing);
    Task DeleteAsync(int id);
}

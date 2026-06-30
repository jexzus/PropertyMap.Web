using PropertyMap.Core.DTOs;
using PropertyMap.Core.Entities;

namespace PropertyMap.Web.Services;

public interface IListingApiService
{
    Task<IEnumerable<PropertyListing>> GetActiveListingsAsync();
    Task<PagedResultDto<PropertyListing>> SearchAsync(
        string? q, string? operacion, string? tipoPropiedad,
        decimal? precioMax, int? dormitoriosMin, int? banosMin,
        int page, int pageSize);
    Task<IEnumerable<ListingMapDto>> GetActiveListingsForMapAsync();
    Task<ListingDetailDto?> GetByIdAsync(int id);
}

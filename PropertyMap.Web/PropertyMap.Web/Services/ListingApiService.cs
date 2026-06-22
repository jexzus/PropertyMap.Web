using System.Net.Http.Json;
using PropertyMap.Core.DTOs;
using PropertyMap.Core.Entities;

namespace PropertyMap.Web.Services;

public class ListingApiService : IListingApiService
{
    private readonly HttpClient _http;

    public ListingApiService(HttpClient http)
    {
        _http = http;
    }

    public async Task<IEnumerable<PropertyListing>> GetActiveListingsAsync()
    {
        return await _http.GetFromJsonAsync<IEnumerable<PropertyListing>>("/api/listings")
               ?? Enumerable.Empty<PropertyListing>();
    }

    public async Task<IEnumerable<ListingMapDto>> GetActiveListingsForMapAsync()
    {
        return await _http.GetFromJsonAsync<IEnumerable<ListingMapDto>>("/api/listings/map")
               ?? Enumerable.Empty<ListingMapDto>();
    }

    public async Task<ListingDetailDto?> GetByIdAsync(int id)
    {
        try
        {
            return await _http.GetFromJsonAsync<ListingDetailDto>($"/api/listings/{id}");
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }
}

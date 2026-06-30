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

    public async Task<PagedResultDto<PropertyListing>> SearchAsync(
        string? q, string? operacion, string? tipoPropiedad,
        decimal? precioMax, int? dormitoriosMin, int? banosMin,
        int page, int pageSize)
    {
        var queryParts = new List<string> { $"page={page}", $"pageSize={pageSize}" };
        if (!string.IsNullOrWhiteSpace(q)) queryParts.Add($"q={Uri.EscapeDataString(q)}");
        if (!string.IsNullOrWhiteSpace(operacion)) queryParts.Add($"operacion={Uri.EscapeDataString(operacion)}");
        if (!string.IsNullOrWhiteSpace(tipoPropiedad)) queryParts.Add($"tipoPropiedad={Uri.EscapeDataString(tipoPropiedad)}");
        if (precioMax.HasValue) queryParts.Add($"precioMax={precioMax.Value}");
        if (dormitoriosMin.HasValue) queryParts.Add($"dormitoriosMin={dormitoriosMin.Value}");
        if (banosMin.HasValue) queryParts.Add($"banosMin={banosMin.Value}");

        var url = $"/api/listings/search?{string.Join("&", queryParts)}";
        return await _http.GetFromJsonAsync<PagedResultDto<PropertyListing>>(url)
               ?? new PagedResultDto<PropertyListing>([], 0, page, pageSize);
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

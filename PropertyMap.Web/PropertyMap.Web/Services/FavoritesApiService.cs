using System.Net.Http.Headers;
using System.Net.Http.Json;
using PropertyMap.Core.DTOs.Properties;
using PropertyMap.Core.DTOs.User;

namespace PropertyMap.Web.Services;

public class FavoritesApiService : IFavoritesApiService
{
    private readonly HttpClient _http;
    private readonly MemoryTokenStore _tokenStore;

    public FavoritesApiService(IHttpClientFactory httpFactory, MemoryTokenStore tokenStore)
    {
        _http = httpFactory.CreateClient("api");
        _tokenStore = tokenStore;
    }

    private void SetAuth()
    {
        _http.DefaultRequestHeaders.Authorization = _tokenStore.AccessToken is null
            ? null
            : new AuthenticationHeaderValue("Bearer", _tokenStore.AccessToken);
    }

    public async Task<List<MyListingDto>> GetFavoritesAsync()
    {
        try
        {
            SetAuth();
            var result = await _http.GetFromJsonAsync<List<MyListingDto>>("api/favorites");
            return result ?? [];
        }
        catch { return []; }
    }

    public async Task<bool> ToggleFavoriteAsync(int listingId, bool currentlyFavorited)
    {
        try
        {
            SetAuth();
            if (currentlyFavorited)
            {
                var resp = await _http.DeleteAsync($"api/favorites/{listingId}");
                return !resp.IsSuccessStatusCode ? currentlyFavorited : false;
            }
            else
            {
                var resp = await _http.PostAsync($"api/favorites/{listingId}", null);
                return resp.IsSuccessStatusCode;
            }
        }
        catch { return currentlyFavorited; }
    }

    public async Task<(bool IsFavorited, int Count)> GetStatusAsync(int listingId)
    {
        try
        {
            SetAuth();
            var resp = await _http.GetFromJsonAsync<FavoriteStatusResponse>(
                $"api/favorites/{listingId}/status");
            return (resp?.IsFavorited ?? false, resp?.Count ?? 0);
        }
        catch { return (false, 0); }
    }
}

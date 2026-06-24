using System.Net.Http.Headers;
using System.Net.Http.Json;
using PropertyMap.Core.DTOs.Ratings;

namespace PropertyMap.Web.Services;

public class RatingsApiService : IRatingsApiService
{
    private readonly HttpClient _http;
    private readonly MemoryTokenStore _tokenStore;

    public RatingsApiService(IHttpClientFactory httpFactory, MemoryTokenStore tokenStore)
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

    public async Task<PropertyRatingStatsDto?> RatePropertyAsync(RatePropertyRequest request)
    {
        try
        {
            SetAuth();
            var resp = await _http.PostAsJsonAsync("api/ratings/property", request);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<PropertyRatingStatsDto>();
        }
        catch { return null; }
    }

    public async Task<PropertyRatingStatsDto?> GetPropertyStatsAsync(int listingId)
    {
        try
        {
            return await _http.GetFromJsonAsync<PropertyRatingStatsDto>(
                $"api/ratings/property/{listingId}/stats");
        }
        catch { return null; }
    }

    public async Task<AgentRatingStatsDto?> RateAgentAsync(RateAgentRequest request)
    {
        try
        {
            SetAuth();
            var resp = await _http.PostAsJsonAsync("api/ratings/agent", request);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<AgentRatingStatsDto>();
        }
        catch { return null; }
    }

    public async Task<AgentRatingStatsDto?> GetAgentStatsAsync(int publisherId)
    {
        try
        {
            return await _http.GetFromJsonAsync<AgentRatingStatsDto>(
                $"api/ratings/agent/{publisherId}/stats");
        }
        catch { return null; }
    }

    public async Task<List<AgentRankingItemDto>> GetRankingAsync(string? ciudad = null, int top = 20)
    {
        try
        {
            var url = $"api/ratings/ranking?top={top}";
            if (!string.IsNullOrEmpty(ciudad))
                url += $"&ciudad={Uri.EscapeDataString(ciudad)}";

            return await _http.GetFromJsonAsync<List<AgentRankingItemDto>>(url) ?? [];
        }
        catch { return []; }
    }
}

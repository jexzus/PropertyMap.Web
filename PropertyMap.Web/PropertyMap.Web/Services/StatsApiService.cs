using System.Net.Http.Headers;
using System.Net.Http.Json;
using PropertyMap.Core.DTOs.Stats;

namespace PropertyMap.Web.Services;

public class StatsApiService : IStatsApiService
{
    private readonly HttpClient _http;
    private readonly MemoryTokenStore _tokenStore;

    public StatsApiService(IHttpClientFactory httpFactory, MemoryTokenStore tokenStore)
    {
        _http = httpFactory.CreateClient("api");
        _tokenStore = tokenStore;
    }

    public async Task<List<ListingStatsDto>> GetMineAsync()
    {
        try
        {
            _http.DefaultRequestHeaders.Authorization = _tokenStore.AccessToken is null
                ? null
                : new AuthenticationHeaderValue("Bearer", _tokenStore.AccessToken);
            return await _http.GetFromJsonAsync<List<ListingStatsDto>>("api/stats/mine") ?? [];
        }
        catch { return []; }
    }
}

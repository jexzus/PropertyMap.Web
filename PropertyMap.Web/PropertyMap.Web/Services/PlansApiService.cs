using System.Net.Http.Headers;
using System.Net.Http.Json;
using PropertyMap.Core.DTOs.Plans;

namespace PropertyMap.Web.Services;

public class PlansApiService : IPlansApiService
{
    private readonly HttpClient _http;
    private readonly MemoryTokenStore _tokenStore;

    public PlansApiService(IHttpClientFactory httpFactory, MemoryTokenStore tokenStore)
    {
        _http = httpFactory.CreateClient("api");
        _tokenStore = tokenStore;
    }

    private void SetAuth() =>
        _http.DefaultRequestHeaders.Authorization = _tokenStore.AccessToken is null
            ? null
            : new AuthenticationHeaderValue("Bearer", _tokenStore.AccessToken);

    public async Task<List<PlanDto>> GetActiveAsync()
    {
        try { return await _http.GetFromJsonAsync<List<PlanDto>>("api/plans") ?? []; }
        catch { return []; }
    }

    public async Task<SubscriptionDto?> GetMineAsync()
    {
        try
        {
            SetAuth();
            var resp = await _http.GetAsync("api/subscriptions/mine");
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<SubscriptionDto>();
        }
        catch { return null; }
    }

    public async Task<SubscriptionDto?> SubscribeAsync(int planId)
    {
        try
        {
            SetAuth();
            var resp = await _http.PostAsJsonAsync("api/subscriptions", new SubscribeRequest(planId));
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<SubscriptionDto>();
        }
        catch { return null; }
    }
}

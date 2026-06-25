using System.Net.Http.Headers;
using System.Net.Http.Json;
using PropertyMap.Core.DTOs.Alerts;

namespace PropertyMap.Web.Services;

public class AlertsApiService : IAlertsApiService
{
    private readonly HttpClient _http;
    private readonly MemoryTokenStore _tokenStore;

    public AlertsApiService(IHttpClientFactory httpFactory, MemoryTokenStore tokenStore)
    {
        _http = httpFactory.CreateClient("api");
        _tokenStore = tokenStore;
    }

    private void SetAuth() =>
        _http.DefaultRequestHeaders.Authorization = _tokenStore.AccessToken is null
            ? null
            : new AuthenticationHeaderValue("Bearer", _tokenStore.AccessToken);

    public async Task<List<AlertDto>> GetMyAlertsAsync()
    {
        try
        {
            SetAuth();
            return await _http.GetFromJsonAsync<List<AlertDto>>("api/alerts") ?? [];
        }
        catch { return []; }
    }

    public async Task<bool> CreateAsync(CreateAlertRequest request)
    {
        try
        {
            SetAuth();
            var resp = await _http.PostAsJsonAsync("api/alerts", request);
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<bool> ToggleAsync(int id)
    {
        try
        {
            SetAuth();
            var resp = await _http.PatchAsync($"api/alerts/{id}/toggle", null);
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<bool> DeleteAsync(int id)
    {
        try
        {
            SetAuth();
            var resp = await _http.DeleteAsync($"api/alerts/{id}");
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }
}

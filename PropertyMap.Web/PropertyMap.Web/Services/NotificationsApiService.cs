using System.Net.Http.Headers;
using System.Net.Http.Json;
using PropertyMap.Core.DTOs.Notifications;

namespace PropertyMap.Web.Services;

public class NotificationsApiService : INotificationsApiService
{
    private readonly HttpClient _http;
    private readonly MemoryTokenStore _tokenStore;

    public NotificationsApiService(IHttpClientFactory httpFactory, MemoryTokenStore tokenStore)
    {
        _http = httpFactory.CreateClient("api");
        _tokenStore = tokenStore;
    }

    private void SetAuth() =>
        _http.DefaultRequestHeaders.Authorization = _tokenStore.AccessToken is null
            ? null
            : new AuthenticationHeaderValue("Bearer", _tokenStore.AccessToken);

    public async Task<List<NotificationDto>> GetMyNotificationsAsync(int take = 20)
    {
        try
        {
            SetAuth();
            return await _http.GetFromJsonAsync<List<NotificationDto>>($"api/notifications?take={take}") ?? [];
        }
        catch { return []; }
    }

    public async Task<int> GetUnreadCountAsync()
    {
        try
        {
            SetAuth();
            return await _http.GetFromJsonAsync<int>("api/notifications/unread-count");
        }
        catch { return 0; }
    }

    public async Task MarkAsReadAsync(int id)
    {
        try
        {
            SetAuth();
            await _http.PatchAsync($"api/notifications/{id}/read", null);
        }
        catch { }
    }

    public async Task MarkAllAsReadAsync()
    {
        try
        {
            SetAuth();
            await _http.PatchAsync("api/notifications/read-all", null);
        }
        catch { }
    }
}

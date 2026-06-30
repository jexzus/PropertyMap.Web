using System.Net.Http.Headers;
using System.Net.Http.Json;
using PropertyMap.Core.DTOs.Admin;

namespace PropertyMap.Web.Services;

public class AuditLogApiService : IAuditLogApiService
{
    private readonly HttpClient _http;
    private readonly MemoryTokenStore _tokenStore;

    public AuditLogApiService(IHttpClientFactory httpFactory, MemoryTokenStore tokenStore)
    {
        _http = httpFactory.CreateClient("api");
        _tokenStore = tokenStore;
    }

    public async Task<List<AuditLogDto>> GetRecentAsync()
    {
        _http.DefaultRequestHeaders.Authorization = _tokenStore.AccessToken is null
            ? null
            : new AuthenticationHeaderValue("Bearer", _tokenStore.AccessToken);
        return await _http.GetFromJsonAsync<List<AuditLogDto>>("api/admin/audit-logs") ?? [];
    }
}

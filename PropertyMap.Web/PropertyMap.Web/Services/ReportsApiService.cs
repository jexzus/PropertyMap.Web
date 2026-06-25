using System.Net.Http.Headers;
using System.Net.Http.Json;
using PropertyMap.Core.DTOs.Reports;

namespace PropertyMap.Web.Services;

public class ReportsApiService : IReportsApiService
{
    private readonly HttpClient _http;
    private readonly MemoryTokenStore _tokenStore;

    public ReportsApiService(IHttpClientFactory httpFactory, MemoryTokenStore tokenStore)
    {
        _http = httpFactory.CreateClient("api");
        _tokenStore = tokenStore;
    }

    public async Task<bool> CreateAsync(CreateReportRequest request)
    {
        try
        {
            _http.DefaultRequestHeaders.Authorization = _tokenStore.AccessToken is null
                ? null
                : new AuthenticationHeaderValue("Bearer", _tokenStore.AccessToken);
            var resp = await _http.PostAsJsonAsync("api/reports", request);
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }
}

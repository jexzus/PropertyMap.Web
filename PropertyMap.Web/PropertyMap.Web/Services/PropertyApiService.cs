using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using PropertyMap.Core.DTOs.Properties;
using PropertyMap.Core.DTOs.Publisher;
using PropertyMap.Core.Enums;

namespace PropertyMap.Web.Services;

public class PropertyApiService : IPropertyApiService
{
    private readonly HttpClient _http;
    private readonly MemoryTokenStore _tokenStore;

    public PropertyApiService(IHttpClientFactory httpFactory, MemoryTokenStore tokenStore)
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

    public async Task<int> CreateListingAsync(CreateListingRequest request)
    {
        SetAuth();
        var resp = await _http.PostAsJsonAsync("api/properties", request);
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<CreatedIdDto>();
        return body!.Id;
    }

    public async Task<List<string>> UploadImagesAsync(int listingId,
        IEnumerable<(byte[] Data, string FileName, string ContentType)> files)
    {
        SetAuth();
        using var form = new MultipartFormDataContent();
        foreach (var (data, fileName, contentType) in files)
        {
            var content = new ByteArrayContent(data);
            content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            form.Add(content, "files", fileName);
        }
        var resp = await _http.PostAsync($"api/properties/{listingId}/images", form);
        if (!resp.IsSuccessStatusCode) return [];
        var body = await resp.Content.ReadFromJsonAsync<UploadUrlsDto>();
        return body?.Urls ?? [];
    }

    public async Task<List<MyListingDto>> GetMyListingsAsync()
    {
        SetAuth();
        var result = await _http.GetFromJsonAsync<List<MyListingDto>>("api/properties/mine");
        return result ?? [];
    }

    public async Task<PublisherProfileResponse?> GetPublisherProfileAsync()
    {
        SetAuth();
        var resp = await _http.GetAsync("api/publisher/profile");
        if (resp.StatusCode == HttpStatusCode.NotFound) return null;
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<PublisherProfileResponse>();
    }

    public async Task<int> EnsurePublisherProfileAsync(string nombre, string telefono)
    {
        var existing = await GetPublisherProfileAsync();
        if (existing is not null) return existing.Id;

        SetAuth();
        var resp = await _http.PostAsJsonAsync("api/publisher/profile",
            new PublisherProfileRequest(nombre, telefono, TipoPublicador.Particular));
        resp.EnsureSuccessStatusCode();
        var created = await resp.Content.ReadFromJsonAsync<PublisherProfileResponse>();
        return created!.Id;
    }

    public async Task<bool> ToggleDestacadoAsync(int listingId)
    {
        try
        {
            SetAuth();
            var resp = await _http.PatchAsync($"api/properties/{listingId}/destacar", null);
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    private record CreatedIdDto(int Id);
    private record UploadUrlsDto(List<string> Urls);
}

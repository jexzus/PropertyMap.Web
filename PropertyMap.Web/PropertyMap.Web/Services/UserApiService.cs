using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Forms;
using PropertyMap.Core.DTOs.User;

namespace PropertyMap.Web.Services;

public class UserApiService : IUserApiService
{
    private readonly HttpClient _http;
    private readonly MemoryTokenStore _tokenStore;

    public UserApiService(IHttpClientFactory httpFactory, MemoryTokenStore tokenStore)
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

    public async Task<UserProfileResponse?> GetProfileAsync()
    {
        try
        {
            SetAuth();
            return await _http.GetFromJsonAsync<UserProfileResponse>("api/user/profile");
        }
        catch { return null; }
    }

    public async Task<(bool Success, string? Error)> UpdateProfileAsync(string nombre, string apellido)
    {
        try
        {
            SetAuth();
            var resp = await _http.PutAsJsonAsync("api/user/profile",
                new UpdateProfileRequest(nombre, apellido));
            if (!resp.IsSuccessStatusCode)
            {
                var err = await resp.Content.ReadFromJsonAsync<ErrorDto>();
                return (false, err?.Message ?? "Error al guardar.");
            }
            return (true, null);
        }
        catch (Exception ex) { return (false, $"No se pudo conectar: {ex.Message}"); }
    }

    public async Task<(bool Success, string? AvatarUrl, string? Error)> UploadAvatarAsync(IBrowserFile file)
    {
        try
        {
            SetAuth();
            using var ms = new MemoryStream();
            await file.OpenReadStream(5 * 1024 * 1024).CopyToAsync(ms);
            var bytes = ms.ToArray();

            using var form = new MultipartFormDataContent();
            var content = new ByteArrayContent(bytes);
            content.Headers.ContentType = new MediaTypeHeaderValue(
                file.ContentType is { Length: > 0 } ct ? ct : "image/jpeg");
            form.Add(content, "file", file.Name);

            var resp = await _http.PostAsync("api/user/avatar", form);
            if (!resp.IsSuccessStatusCode) return (false, null, "Error al subir el avatar.");

            var body = await resp.Content.ReadFromJsonAsync<AvatarUrlDto>();
            return (true, body?.AvatarUrl, null);
        }
        catch (Exception ex) { return (false, null, $"No se pudo subir: {ex.Message}"); }
    }

    private record ErrorDto(string Message);
    private record AvatarUrlDto(string AvatarUrl);
}

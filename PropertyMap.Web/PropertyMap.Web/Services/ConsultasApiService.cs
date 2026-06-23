using System.Net.Http.Headers;
using System.Net.Http.Json;
using PropertyMap.Core.DTOs.Consultas;

namespace PropertyMap.Web.Services;

public class ConsultasApiService : IConsultasApiService
{
    private readonly HttpClient _http;
    private readonly MemoryTokenStore _tokenStore;

    public ConsultasApiService(IHttpClientFactory httpFactory, MemoryTokenStore tokenStore)
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

    public async Task<ConsultaDetailDto?> CreateOrContinueAsync(int listingId, string mensaje)
    {
        try
        {
            SetAuth();
            var resp = await _http.PostAsJsonAsync("api/consultas",
                new CreateConsultaRequest(listingId, mensaje));
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<ConsultaDetailDto>();
        }
        catch { return null; }
    }

    public async Task<List<ConsultaSummaryDto>> GetMyConsultasAsync()
    {
        try
        {
            SetAuth();
            return await _http.GetFromJsonAsync<List<ConsultaSummaryDto>>("api/consultas") ?? [];
        }
        catch { return []; }
    }

    public async Task<List<ConsultaSummaryDto>> GetPublisherConsultasAsync()
    {
        try
        {
            SetAuth();
            return await _http.GetFromJsonAsync<List<ConsultaSummaryDto>>("api/consultas/publisher") ?? [];
        }
        catch { return []; }
    }

    public async Task<ConsultaDetailDto?> GetDetailAsync(int consultaId)
    {
        try
        {
            SetAuth();
            return await _http.GetFromJsonAsync<ConsultaDetailDto>($"api/consultas/{consultaId}");
        }
        catch { return null; }
    }

    public async Task<ConsultaMensajeDto?> ReplyAsync(int consultaId, string mensaje)
    {
        try
        {
            SetAuth();
            var resp = await _http.PostAsJsonAsync(
                $"api/consultas/{consultaId}/mensajes",
                new SendMensajeRequest(mensaje));
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<ConsultaMensajeDto>();
        }
        catch { return null; }
    }
}

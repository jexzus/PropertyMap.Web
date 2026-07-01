using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using PropertyMap.Core.DTOs.Auth;
using PropertyMap.Web.Auth;

namespace PropertyMap.Web.Services;

public class AuthService : IAuthService
{
    private const string StorageKey = "pm-auth";
    private readonly HttpClient _http;
    private readonly MemoryTokenStore _tokenStore;
    private readonly BlazorAuthStateProvider _authProvider;
    private readonly ProtectedLocalStorage _storage;

    public AuthService(
        IHttpClientFactory httpFactory,
        MemoryTokenStore tokenStore,
        BlazorAuthStateProvider authProvider,
        ProtectedLocalStorage storage)
    {
        _http = httpFactory.CreateClient("api");
        _tokenStore = tokenStore;
        _authProvider = authProvider;
        _storage = storage;
    }

    public async Task<(bool Success, string? Error)> LoginAsync(string email, string password)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("api/auth/login", new LoginRequest(email, password));
            if (!resp.IsSuccessStatusCode)
            {
                var err = await resp.Content.ReadFromJsonAsync<ErrorDto>();
                return (false, err?.Message ?? "Credenciales incorrectas.");
            }
            var auth = await resp.Content.ReadFromJsonAsync<AuthResponse>();
            if (auth is null) return (false, "Respuesta inesperada del servidor.");

            _tokenStore.SetFromResponse(auth);
            await PersistAsync(auth);
            _authProvider.NotifyStateChanged();
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, $"No se pudo conectar: {ex.Message}");
        }
    }

    public async Task<(bool Success, string? Error)> RegisterAsync(
        string nombre, string apellido, string email, string password, string confirmPassword)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("api/auth/register",
                new RegisterRequest(nombre, apellido, email, password, confirmPassword));
            if (!resp.IsSuccessStatusCode)
            {
                var err = await resp.Content.ReadFromJsonAsync<ErrorDto>();
                return (false, err?.Message ?? "Error al registrarse.");
            }
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, $"No se pudo conectar: {ex.Message}");
        }
    }

    public async Task<(bool Success, string? Error)> VerifyEmailAsync(string email, string token)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("api/auth/verify-email",
                new PropertyMap.Core.DTOs.Auth.VerifyEmailRequest(email, token));
            if (!resp.IsSuccessStatusCode)
            {
                var err = await resp.Content.ReadFromJsonAsync<ErrorDto>();
                return (false, err?.Message ?? "Código inválido o expirado.");
            }
            return (true, null);
        }
        catch (Exception ex) { return (false, $"No se pudo conectar: {ex.Message}"); }
    }

    public async Task<(bool Success, string? Error)> ForgotPasswordAsync(string email)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("api/auth/forgot-password",
                new PropertyMap.Core.DTOs.Auth.ForgotPasswordRequest(email));
            if (!resp.IsSuccessStatusCode)
            {
                var err = await resp.Content.ReadFromJsonAsync<ErrorDto>();
                return (false, err?.Message ?? "Error al procesar la solicitud.");
            }
            return (true, null);
        }
        catch (Exception ex) { return (false, $"No se pudo conectar: {ex.Message}"); }
    }

    public async Task<(bool Success, string? Error)> ResetPasswordAsync(
        string email, string token, string newPassword, string confirmPassword)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("api/auth/reset-password",
                new PropertyMap.Core.DTOs.Auth.ResetPasswordRequest(email, token, newPassword, confirmPassword));
            if (!resp.IsSuccessStatusCode)
            {
                var err = await resp.Content.ReadFromJsonAsync<ErrorDto>();
                return (false, err?.Message ?? "Error al restablecer la contraseña.");
            }
            return (true, null);
        }
        catch (Exception ex) { return (false, $"No se pudo conectar: {ex.Message}"); }
    }

    public async Task LogoutAsync()
    {
        _tokenStore.Clear();
        try { await _storage.DeleteAsync(StorageKey); } catch { }
        _authProvider.NotifyStateChanged();
    }

    public async Task<bool> TryRestoreSessionAsync()
    {
        try
        {
            var result = await _storage.GetAsync<AuthResponse>(StorageKey);
            if (!result.Success || result.Value is null) return false;
            var stored = result.Value;
            if (stored.AccessTokenExpiry <= DateTime.UtcNow) return false;
            _tokenStore.SetFromResponse(stored);
            _authProvider.NotifyStateChanged();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task PersistAsync(AuthResponse auth)
    {
        try { await _storage.SetAsync(StorageKey, auth); } catch { }
    }

    private record ErrorDto(string Message);
}

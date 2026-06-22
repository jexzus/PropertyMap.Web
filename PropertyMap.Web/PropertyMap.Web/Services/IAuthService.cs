namespace PropertyMap.Web.Services;

public interface IAuthService
{
    Task<(bool Success, string? Error)> LoginAsync(string email, string password);
    Task<(bool Success, string? Error)> RegisterAsync(
        string nombre, string apellido, string email, string password, string confirmPassword);
    Task LogoutAsync();
    Task<bool> TryRestoreSessionAsync();
}

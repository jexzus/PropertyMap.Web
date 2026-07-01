namespace PropertyMap.Web.Services;

public interface IAuthService
{
    Task<(bool Success, string? Error)> LoginAsync(string email, string password);
    Task<(bool Success, string? Error)> RegisterAsync(
        string nombre, string apellido, string email, string password, string confirmPassword);
    Task<(bool Success, string? Error)> VerifyEmailAsync(string email, string token);
    Task<(bool Success, string? Error)> ForgotPasswordAsync(string email);
    Task<(bool Success, string? Error)> ResetPasswordAsync(string email, string token, string newPassword, string confirmPassword);
    Task LogoutAsync();
    Task<bool> TryRestoreSessionAsync();
}

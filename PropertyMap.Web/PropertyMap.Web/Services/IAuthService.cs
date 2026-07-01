namespace PropertyMap.Web.Services;

public interface IAuthService
{
    Task<(bool Success, string? Error)> LoginAsync(string email, string password);
    Task<(bool Success, string? Error)> RegisterAsync(
        string nombre, string apellido, string email, string password, string confirmPassword);
    Task<(bool Success, string? Error)> VerifyEmailAsync(string email, string token);
    Task<(bool Success, string? Error)> ForgotPasswordAsync(string email);
    Task<(bool Success, string? Error)> ResetPasswordAsync(string email, string token, string newPassword, string confirmPassword);
    // Nuevo flujo
    Task<(bool Success, string? Error)> PreRegistroAsync(string email);
    Task<(bool Success, string? Error)> ConfirmarPreRegistroAsync(string email, string codigo);
    Task<(bool Success, string? Error)> RegistrarAsync(string email, string nombre, string apellido, string password, string confirm);
    Task<(bool Success, string? Error)> SolicitarRecuperacionAsync(string email);
    Task<(bool Success, string? Error)> CambiarContrasenaAsync(string email, string codigo, string nuevaContrasena);
    Task LogoutAsync();
    Task<bool> TryRestoreSessionAsync();
}

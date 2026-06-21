namespace PropertyMap.Core.DTOs.Auth;

public record RegisterRequest(
    string Nombre,
    string Apellido,
    string Email,
    string Password,
    string ConfirmPassword
);

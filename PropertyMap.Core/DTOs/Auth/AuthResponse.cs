namespace PropertyMap.Core.DTOs.Auth;

public record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiry,
    string UserId,
    string Email,
    string NombreCompleto,
    IList<string> Roles
);

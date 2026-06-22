namespace PropertyMap.Core.DTOs.User;

public record UserProfileResponse(
    string Id,
    string Nombre,
    string Apellido,
    string Email,
    string? AvatarUrl);

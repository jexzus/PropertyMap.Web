namespace PropertyMap.Core.DTOs.Auth;

public record RefreshTokenRequest(string AccessToken, string RefreshToken);

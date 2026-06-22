using PropertyMap.Core.DTOs.Auth;

namespace PropertyMap.Web.Services;

public class MemoryTokenStore
{
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? AccessTokenExpiry { get; set; }
    public string? UserId { get; set; }
    public string? Email { get; set; }
    public string? NombreCompleto { get; set; }
    public IList<string> Roles { get; set; } = [];

    public bool IsAuthenticated =>
        !string.IsNullOrEmpty(AccessToken) && AccessTokenExpiry > DateTime.UtcNow;

    public void SetFromResponse(AuthResponse r)
    {
        AccessToken = r.AccessToken;
        RefreshToken = r.RefreshToken;
        AccessTokenExpiry = r.AccessTokenExpiry;
        UserId = r.UserId;
        Email = r.Email;
        NombreCompleto = r.NombreCompleto;
        Roles = r.Roles;
    }

    public void Clear()
    {
        AccessToken = null;
        RefreshToken = null;
        AccessTokenExpiry = null;
        UserId = null;
        Email = null;
        NombreCompleto = null;
        Roles = [];
    }
}

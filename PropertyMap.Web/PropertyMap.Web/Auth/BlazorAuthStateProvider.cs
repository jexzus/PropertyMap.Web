using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using PropertyMap.Web.Services;

namespace PropertyMap.Web.Auth;

public class BlazorAuthStateProvider : AuthenticationStateProvider
{
    private static readonly AuthenticationState Anonymous =
        new(new ClaimsPrincipal(new ClaimsIdentity()));

    private readonly MemoryTokenStore _tokenStore;

    public BlazorAuthStateProvider(MemoryTokenStore tokenStore)
    {
        _tokenStore = tokenStore;
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        if (!_tokenStore.IsAuthenticated)
            return Task.FromResult(Anonymous);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, _tokenStore.UserId ?? ""),
            new(ClaimTypes.Email, _tokenStore.Email ?? ""),
            new(ClaimTypes.Name, _tokenStore.NombreCompleto ?? ""),
        };
        foreach (var role in _tokenStore.Roles)
            claims.Add(new(ClaimTypes.Role, role));

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "jwt"));
        return Task.FromResult(new AuthenticationState(principal));
    }

    public void NotifyStateChanged() =>
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
}

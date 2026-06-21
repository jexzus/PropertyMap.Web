using System.Security.Claims;
using PropertyMap.Core.Entities;

namespace PropertyMap.Core.Interfaces;

public interface ITokenService
{
    string GenerateAccessToken(ApplicationUser user, IList<string> roles);
    string GenerateRefreshToken();
    ClaimsPrincipal? ValidateExpiredToken(string token);
}

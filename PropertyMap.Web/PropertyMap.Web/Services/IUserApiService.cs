using Microsoft.AspNetCore.Components.Forms;
using PropertyMap.Core.DTOs.User;

namespace PropertyMap.Web.Services;

public interface IUserApiService
{
    Task<UserProfileResponse?> GetProfileAsync();
    Task<(bool Success, string? Error)> UpdateProfileAsync(string nombre, string apellido);
    Task<(bool Success, string? AvatarUrl, string? Error)> UploadAvatarAsync(IBrowserFile file);
}

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PropertyMap.Core.DTOs.User;
using PropertyMap.Core.Entities;
using PropertyMap.Core.Interfaces;

namespace PropertyMap.Api.Controllers;

[ApiController]
[Route("api/user")]
[Authorize]
public class UserController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IImageService _images;

    public UserController(UserManager<ApplicationUser> userManager, IImageService images)
    {
        _userManager = userManager;
        _images = images;
    }

    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return NotFound();

        return Ok(new UserProfileResponse(user.Id, user.Nombre, user.Apellido, user.Email!, user.AvatarUrl));
    }

    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile(UpdateProfileRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Nombre) || string.IsNullOrWhiteSpace(request.Apellido))
            return BadRequest(new { message = "Nombre y apellido son requeridos." });

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return NotFound();

        user.Nombre = request.Nombre.Trim();
        user.Apellido = request.Apellido.Trim();
        await _userManager.UpdateAsync(user);

        return Ok(new UserProfileResponse(user.Id, user.Nombre, user.Apellido, user.Email!, user.AvatarUrl));
    }

    [HttpPost("avatar")]
    public async Task<IActionResult> UploadAvatar(IFormFile file)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "Archivo requerido." });

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return NotFound();

        try
        {
            var avatarUrl = await _images.SaveAvatarAsync(userId, file);
            user.AvatarUrl = avatarUrl;
            await _userManager.UpdateAsync(user);
            return Ok(new { avatarUrl });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}

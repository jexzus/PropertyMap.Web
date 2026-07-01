using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PropertyMap.Core.DTOs.Publisher;
using PropertyMap.Core.Entities;
using PropertyMap.Core.Enums;
using PropertyMap.Core.Interfaces;

namespace PropertyMap.Api.Controllers;

[ApiController]
[Route("api/publisher")]
[Authorize]
public class PublisherController : ControllerBase
{
    private readonly IPublisherRepository _publishers;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IListingRepository _listings;

    public PublisherController(
        IPublisherRepository publishers,
        UserManager<ApplicationUser> userManager,
        IListingRepository listings)
    {
        _publishers = publishers;
        _userManager = userManager;
        _listings = listings;
    }

    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var publisher = await _publishers.GetByUserIdAsync(userId);
        if (publisher == null) return NotFound(new { message = "No tenés un perfil de publisher aún." });

        var total = await _listings.GetListingsByPublisherAsync(publisher.Id);
        return Ok(new PublisherProfileResponse(
            publisher.Id,
            publisher.Nombre,
            publisher.Email,
            publisher.Telefono,
            publisher.LogoUrl,
            publisher.Tipo,
            total.Count()
        ));
    }

    // Crea el perfil automáticamente con datos mínimos si no existe aún.
    [HttpPost("profile/auto")]
    public async Task<IActionResult> AutoCreateProfile()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return Unauthorized();

        var existing = await _publishers.GetByUserIdAsync(userId);
        if (existing != null) return Ok(new { message = "Ya tenés perfil." });

        var publisher = new Publisher
        {
            Nombre = $"{((ApplicationUser)user).Nombre} {((ApplicationUser)user).Apellido}".Trim(),
            Email = user.Email!,
            Telefono = "",
            Tipo = TipoPublicador.Particular,
            UserId = userId
        };
        await _publishers.AddAsync(publisher);

        if (!await _userManager.IsInRoleAsync(user, "Publisher"))
            await _userManager.AddToRoleAsync(user, "Publisher");

        return Ok(new { message = "Perfil creado." });
    }

    [HttpPost("profile")]
    public async Task<IActionResult> CreateProfile(PublisherProfileRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return Unauthorized();

        var existing = await _publishers.GetByUserIdAsync(userId);
        if (existing != null)
            return Conflict(new { message = "Ya tenés un perfil de publisher." });

        var publisher = new Publisher
        {
            Nombre = request.Nombre,
            Email = user.Email!,
            Telefono = request.Telefono,
            Tipo = request.Tipo,
            UserId = userId
        };

        var created = await _publishers.AddAsync(publisher);

        if (!await _userManager.IsInRoleAsync(user, "Publisher"))
            await _userManager.AddToRoleAsync(user, "Publisher");

        return CreatedAtAction(nameof(GetProfile), null, new PublisherProfileResponse(
            created.Id,
            created.Nombre,
            created.Email,
            created.Telefono,
            created.LogoUrl,
            created.Tipo,
            0
        ));
    }

    [HttpPut("profile")]
    [Authorize(Roles = "Publisher")]
    public async Task<IActionResult> UpdateProfile(PublisherProfileRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var publisher = await _publishers.GetByUserIdAsync(userId);
        if (publisher == null) return NotFound();

        publisher.Nombre = request.Nombre;
        publisher.Telefono = request.Telefono;
        publisher.Tipo = request.Tipo;
        await _publishers.UpdateAsync(publisher);

        return NoContent();
    }
}

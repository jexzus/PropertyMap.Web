using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PropertyMap.Core.DTOs.User;
using PropertyMap.Core.Entities;
using PropertyMap.Core.Interfaces;

namespace PropertyMap.Api.Controllers;

[ApiController]
[Route("api/favorites")]
[Authorize]
public class FavoritesController : ControllerBase
{
    private readonly IFavoriteRepository _favorites;

    public FavoritesController(IFavoriteRepository favorites)
    {
        _favorites = favorites;
    }

    [HttpGet]
    public async Task<IActionResult> GetMyFavorites()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var listings = await _favorites.GetByUserAsync(userId);
        return Ok(listings);
    }

    [HttpPost("{listingId:int}")]
    public async Task<IActionResult> AddFavorite(int listingId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        await _favorites.AddAsync(new PropertyFavorite
        {
            PropertyListingId = listingId,
            UserId = userId
        });
        return Ok();
    }

    [HttpDelete("{listingId:int}")]
    public async Task<IActionResult> RemoveFavorite(int listingId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        await _favorites.RemoveAsync(listingId, userId);
        return Ok();
    }

    [HttpGet("{listingId:int}/status")]
    [AllowAnonymous]
    public async Task<IActionResult> GetStatus(int listingId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var isFavorited = userId != null && await _favorites.IsFavoritedAsync(listingId, userId);
        var count = await _favorites.GetCountAsync(listingId);
        return Ok(new FavoriteStatusResponse(isFavorited, count));
    }
}

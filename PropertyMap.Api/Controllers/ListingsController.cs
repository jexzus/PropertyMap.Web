using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using PropertyMap.Core.Interfaces;

namespace PropertyMap.Api.Controllers;

[ApiController]
[Route("api/listings")]
public class ListingsController : ControllerBase
{
    private readonly IListingRepository _listings;
    private readonly IViewTrackingService _viewTracking;

    public ListingsController(IListingRepository listings, IViewTrackingService viewTracking)
    {
        _listings = listings;
        _viewTracking = viewTracking;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var listings = await _listings.GetActiveListingsAsync();
        return Ok(listings);
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search(
        [FromQuery] string? q,
        [FromQuery] string? operacion,
        [FromQuery] string? tipoPropiedad,
        [FromQuery] decimal? precioMax,
        [FromQuery] int? dormitoriosMin,
        [FromQuery] int? banosMin,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var result = await _listings.SearchAsync(
            q, operacion, tipoPropiedad, precioMax, dormitoriosMin, banosMin, page, pageSize);

        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var listing = await _listings.GetByIdAsDetailAsync(id);
        if (listing == null) return NotFound();

        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            await _viewTracking.TrackViewAsync(id, userId, ip, DateOnly.FromDateTime(DateTime.UtcNow));
        }
        catch { }

        return Ok(listing);
    }

    [HttpGet("map")]
    public async Task<IActionResult> GetForMap()
    {
        var listings = await _listings.GetActiveListingsForMapAsync();
        return Ok(listings);
    }
}

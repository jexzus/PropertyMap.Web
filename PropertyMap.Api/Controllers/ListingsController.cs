using Microsoft.AspNetCore.Mvc;
using PropertyMap.Core.Interfaces;

namespace PropertyMap.Api.Controllers;

[ApiController]
[Route("api/listings")]
public class ListingsController : ControllerBase
{
    private readonly IListingRepository _listings;

    public ListingsController(IListingRepository listings)
    {
        _listings = listings;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var listings = await _listings.GetActiveListingsAsync();
        return Ok(listings);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var listing = await _listings.GetByIdAsDetailAsync(id);
        if (listing == null) return NotFound();
        return Ok(listing);
    }

    [HttpGet("map")]
    public async Task<IActionResult> GetForMap()
    {
        var listings = await _listings.GetActiveListingsForMapAsync();
        return Ok(listings);
    }
}

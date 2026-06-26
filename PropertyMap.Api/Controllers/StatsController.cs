using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PropertyMap.Core.Interfaces;

namespace PropertyMap.Api.Controllers;

[ApiController]
[Route("api/stats")]
[Authorize(Roles = "Publisher")]
public class StatsController : ControllerBase
{
    private readonly IListingStatsRepository _stats;
    private readonly IPublisherRepository _publishers;

    public StatsController(IListingStatsRepository stats, IPublisherRepository publishers)
    {
        _stats = stats;
        _publishers = publishers;
    }

    [HttpGet("mine")]
    public async Task<IActionResult> GetMine()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var publisher = await _publishers.GetByUserIdAsync(userId);
        if (publisher is null) return Ok(Array.Empty<object>());

        return Ok(await _stats.GetForPublisherAsync(publisher.Id));
    }

    [HttpGet("listings/{id:int}")]
    public async Task<IActionResult> GetForListing(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var publisher = await _publishers.GetByUserIdAsync(userId);
        if (publisher is null) return Forbid();

        var stats = await _stats.GetForListingAsync(id, publisher.Id);
        if (stats is null) return NotFound();

        return Ok(stats);
    }
}

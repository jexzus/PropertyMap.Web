using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PropertyMap.Core.DTOs.Stats;
using PropertyMap.Core.Interfaces;

namespace PropertyMap.Api.Controllers;

[ApiController]
[Route("api/stats")]
[Authorize(Roles = "Publisher")]
public class StatsController : ControllerBase
{
    private readonly IListingStatsRepository _stats;
    private readonly IPublisherRepository _publishers;
    private readonly ISubscriptionRepository _subscriptions;

    public StatsController(IListingStatsRepository stats, IPublisherRepository publishers, ISubscriptionRepository subscriptions)
    {
        _stats = stats;
        _publishers = publishers;
        _subscriptions = subscriptions;
    }

    [HttpGet("mine")]
    public async Task<IActionResult> GetMine()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var publisher = await _publishers.GetByUserIdAsync(userId);
        if (publisher is null) return Ok(Array.Empty<object>());

        var stats = await _stats.GetForPublisherAsync(publisher.Id);
        var avanzadas = await TienePlanAvanzadoAsync(userId);

        return Ok(stats.Select(s => AplicarGating(s, avanzadas)));
    }

    [HttpGet("listings/{id:int}")]
    public async Task<IActionResult> GetForListing(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var publisher = await _publishers.GetByUserIdAsync(userId);
        if (publisher is null) return Forbid();

        var stats = await _stats.GetForListingAsync(id, publisher.Id);
        if (stats is null) return NotFound();

        var avanzadas = await TienePlanAvanzadoAsync(userId);
        return Ok(AplicarGating(stats, avanzadas));
    }

    private async Task<bool> TienePlanAvanzadoAsync(string userId)
    {
        var subscription = await _subscriptions.GetByUserIdAsync(userId);
        return subscription?.Plan.EstadisticasAvanzadas ?? false;
    }

    private static ListingStatsDto AplicarGating(ListingStatsDto stats, bool avanzadas) =>
        avanzadas ? stats : stats with { Favoritos = 0, Consultas = 0, Conversiones = 0 };
}

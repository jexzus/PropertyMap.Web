// PropertyMap.Api/Controllers/RatingsController.cs
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PropertyMap.Core.DTOs.Ratings;
using PropertyMap.Core.Entities;
using PropertyMap.Core.Enums;
using PropertyMap.Core.Interfaces;

namespace PropertyMap.Api.Controllers;

[ApiController]
[Route("api/ratings")]
public class RatingsController : ControllerBase
{
    private readonly IPropertyRatingRepository _propertyRatings;
    private readonly IAgentRatingRepository _agentRatings;
    private readonly IListingRepository _listings;

    public RatingsController(
        IPropertyRatingRepository propertyRatings,
        IAgentRatingRepository agentRatings,
        IListingRepository listings)
    {
        _propertyRatings = propertyRatings;
        _agentRatings = agentRatings;
        _listings = listings;
    }

    [HttpPost("property")]
    [Authorize]
    public async Task<IActionResult> RateProperty([FromBody] RatePropertyRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        var listing = await _listings.GetByIdAsync(request.ListingId);
        if (listing is null) return NotFound("La propiedad no existe.");
        if (listing.Operacion != TipoOperacion.AlquilerTemporario)
            return BadRequest("Solo se pueden valorar propiedades de AlquilerTemporario.");

        if (!await _propertyRatings.HasConsultaAsync(request.ListingId, userId))
            return Forbid();

        await _propertyRatings.AddOrUpdateAsync(new PropertyRating
        {
            PropertyListingId    = request.ListingId,
            UserId               = userId,
            PuntajeUbicacion     = request.PuntajeUbicacion,
            PuntajeEstado        = request.PuntajeEstado,
            PuntajePrecioCalidad = request.PuntajePrecioCalidad,
            Comentario           = request.Comentario,
            FechaValoracion      = DateTime.UtcNow
        });

        return Ok(await _propertyRatings.GetStatsAsync(request.ListingId));
    }

    [HttpGet("property/{listingId:int}/stats")]
    public async Task<IActionResult> GetPropertyStats(int listingId) =>
        Ok(await _propertyRatings.GetStatsAsync(listingId));

    [HttpPost("agent")]
    [Authorize]
    public async Task<IActionResult> RateAgent([FromBody] RateAgentRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        if (!await _agentRatings.HasConsultaWithPublisherAsync(request.PublisherId, userId))
            return Forbid();

        await _agentRatings.AddOrUpdateAsync(new AgentRating
        {
            PublisherId            = request.PublisherId,
            UserId                 = userId,
            PuntajeAtencion        = request.PuntajeAtencion,
            PuntajeRapidez         = request.PuntajeRapidez,
            PuntajeTransparencia   = request.PuntajeTransparencia,
            PuntajeProfesionalismo = request.PuntajeProfesionalismo,
            Comentario             = request.Comentario,
            FechaValoracion        = DateTime.UtcNow
        });

        return Ok(await _agentRatings.GetStatsAsync(request.PublisherId));
    }

    [HttpGet("agent/{publisherId:int}/stats")]
    public async Task<IActionResult> GetAgentStats(int publisherId) =>
        Ok(await _agentRatings.GetStatsAsync(publisherId));

    [HttpGet("ranking")]
    public async Task<IActionResult> GetRanking(
        [FromQuery] string? ciudad = null,
        [FromQuery] int top = 20)
    {
        if (top < 1 || top > 100) top = 20;
        return Ok(await _agentRatings.GetRankingAsync(ciudad, top));
    }
}

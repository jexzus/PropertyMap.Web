using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PropertyMap.Core.DTOs.Plans;
using PropertyMap.Core.Interfaces;

namespace PropertyMap.Api.Controllers;

[ApiController]
[Route("api/subscriptions")]
[Authorize]
public class SubscriptionsController : ControllerBase
{
    private readonly ISubscriptionRepository _subscriptions;
    private readonly IPlanRepository _plans;

    public SubscriptionsController(ISubscriptionRepository subscriptions, IPlanRepository plans)
    {
        _subscriptions = subscriptions;
        _plans = plans;
    }

    [HttpGet("mine")]
    public async Task<IActionResult> GetMine()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var sub = await _subscriptions.GetByUserIdAsync(userId);
        if (sub is null) return NotFound(new { message = "No tenés una suscripción activa." });

        return Ok(new SubscriptionDto(
            sub.Id, sub.PlanId, sub.Plan.Nombre, sub.Estado,
            sub.FechaInicio, sub.FechaVencimiento, sub.AutoRenovar));
    }

    [HttpPost]
    public async Task<IActionResult> Subscribe(SubscribeRequest request)
    {
        var plan = await _plans.GetByIdAsync(request.PlanId);
        if (plan is null || !plan.Activo) return NotFound(new { message = "El plan no existe o no está disponible." });

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var vencimiento = DateTime.UtcNow.AddMonths(1);
        var sub = await _subscriptions.CreateOrReplaceAsync(userId, plan.Id, vencimiento);

        return Ok(new SubscriptionDto(
            sub.Id, sub.PlanId, sub.Plan.Nombre, sub.Estado,
            sub.FechaInicio, sub.FechaVencimiento, sub.AutoRenovar));
    }
}

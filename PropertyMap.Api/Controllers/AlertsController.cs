using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PropertyMap.Core.DTOs.Alerts;
using PropertyMap.Core.Entities;
using PropertyMap.Core.Interfaces;

namespace PropertyMap.Api.Controllers;

[ApiController]
[Route("api/alerts")]
[Authorize]
public class AlertsController : ControllerBase
{
    private readonly IAlertRepository _alerts;

    public AlertsController(IAlertRepository alerts)
    {
        _alerts = alerts;
    }

    [HttpGet]
    public async Task<IActionResult> GetMine()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return Ok(await _alerts.GetByUserAsync(userId));
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateAlertRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var created = await _alerts.AddAsync(new Alert
        {
            UserId = userId,
            Nombre = request.Nombre,
            Operacion = request.Operacion,
            TipoPropiedad = request.TipoPropiedad,
            Ciudad = request.Ciudad,
            PrecioMax = request.PrecioMax,
            Moneda = request.Moneda,
            DormitoriosMin = request.DormitoriosMin,
            Activa = true,
            FechaCreacion = DateTime.UtcNow
        });
        return CreatedAtAction(nameof(GetMine), new { id = created.Id });
    }

    [HttpPatch("{id:int}/toggle")]
    public async Task<IActionResult> Toggle(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var alert = await _alerts.GetByIdAsync(id);
        if (alert is null || alert.UserId != userId) return NotFound();

        alert.Activa = !alert.Activa;
        await _alerts.UpdateAsync(alert);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var alert = await _alerts.GetByIdAsync(id);
        if (alert is null || alert.UserId != userId) return NotFound();

        await _alerts.DeleteAsync(id);
        return NoContent();
    }
}

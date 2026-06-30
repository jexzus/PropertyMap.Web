using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PropertyMap.Core.DTOs.Admin;
using PropertyMap.Core.DTOs.Reports;
using PropertyMap.Core.Entities;
using PropertyMap.Core.Enums;
using PropertyMap.Core.Interfaces;

namespace PropertyMap.Api.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly IListingRepository _listings;
    private readonly IReportRepository _reports;
    private readonly IAlertMatchingService _alertMatching;
    private readonly IAuditLogRepository _auditLog;

    public AdminController(
        IListingRepository listings,
        IReportRepository reports,
        IAlertMatchingService alertMatching,
        IAuditLogRepository auditLog)
    {
        _listings = listings;
        _reports = reports;
        _alertMatching = alertMatching;
        _auditLog = auditLog;
    }

    [HttpGet("listings/pending")]
    public async Task<IActionResult> GetPending()
    {
        var pending = await _listings.GetPendingListingsAsync();
        return Ok(pending);
    }

    [HttpPatch("listings/{id:int}/review")]
    public async Task<IActionResult> Review(int id, ReviewListingRequest request)
    {
        var listing = await _listings.GetByIdAsync(id);
        if (listing == null) return NotFound();

        if (listing.Estado != EstadoPublicacion.PendienteAprobacion)
            return BadRequest(new { message = "El listado no está pendiente de aprobación." });

        listing.Estado = request.Aprobar
            ? EstadoPublicacion.Publicada
            : EstadoPublicacion.Borrador;
        listing.FechaActualizacion = DateTime.UtcNow;

        await _listings.UpdateAsync(listing);

        if (request.Aprobar)
            await _alertMatching.NotifyMatchingAlertsAsync(listing);

        try
        {
            await _auditLog.AddAsync(new AuditLog
            {
                UserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
                Accion = request.Aprobar ? "AprobarListing" : "RechazarListing",
                Entidad = "PropertyListing",
                EntidadId = id.ToString(),
                Detalles = request.Aprobar ? null : request.MotivoRechazo,
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
            });
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error al registrar audit log para listing {id}: {ex.Message}");
        }

        return Ok(new
        {
            message = request.Aprobar
                ? "Propiedad aprobada y publicada."
                : $"Propiedad rechazada. Motivo: {request.MotivoRechazo ?? "no especificado"}"
        });
    }

    [HttpGet("listings")]
    public async Task<IActionResult> GetAll()
    {
        var listings = await _listings.GetActiveListingsAsync();
        return Ok(listings);
    }

    [HttpGet("reports")]
    public async Task<IActionResult> GetReports() =>
        Ok(await _reports.GetPendingAsync());

    [HttpPatch("reports/{id:int}/review")]
    public async Task<IActionResult> ReviewReport(int id, ReviewReportRequest request)
    {
        var report = await _reports.GetByIdAsync(id);
        if (report is null) return NotFound();

        report.Estado = request.NuevoEstado;
        await _reports.UpdateAsync(report);

        if (request.NuevoEstado == EstadoReporte.Resuelto)
        {
            var listing = await _listings.GetByIdAsync(report.PropertyListingId);
            if (listing is not null && listing.Estado == EstadoPublicacion.Publicada)
            {
                listing.Estado = EstadoPublicacion.Pausada;
                listing.FechaActualizacion = DateTime.UtcNow;
                await _listings.UpdateAsync(listing);
            }
        }

        try
        {
            await _auditLog.AddAsync(new AuditLog
            {
                UserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
                Accion = request.NuevoEstado == EstadoReporte.Resuelto ? "ResolverReporte" : "RechazarReporte",
                Entidad = "Report",
                EntidadId = id.ToString(),
                Detalles = null,
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
            });
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error al registrar audit log para reporte {id}: {ex.Message}");
        }

        return NoContent();
    }

    [HttpGet("audit-logs")]
    public async Task<IActionResult> GetAuditLogs() =>
        Ok(await _auditLog.GetRecentAsync());
}

using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PropertyMap.Core.DTOs.Reports;
using PropertyMap.Core.Entities;
using PropertyMap.Core.Interfaces;

namespace PropertyMap.Api.Controllers;

[ApiController]
[Route("api/reports")]
[Authorize]
public class ReportsController : ControllerBase
{
    private readonly IReportRepository _reports;
    private readonly IListingRepository _listings;
    private readonly IEmailService _email;
    private readonly UserManager<ApplicationUser> _userManager;

    public ReportsController(
        IReportRepository reports,
        IListingRepository listings,
        IEmailService email,
        UserManager<ApplicationUser> userManager)
    {
        _reports = reports;
        _listings = listings;
        _email = email;
        _userManager = userManager;
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateReportRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        var listing = await _listings.GetByIdAsync(request.PropertyListingId);
        if (listing is null) return NotFound(new { message = "La propiedad no existe." });

        await _reports.AddAsync(new Report
        {
            PropertyListingId = request.PropertyListingId,
            UserId = userId,
            Motivo = request.Motivo,
            Descripcion = request.Descripcion,
            FechaReporte = DateTime.UtcNow
        });

        var user = await _userManager.FindByIdAsync(userId);
        if (user?.Email is not null)
            await _email.SendReportConfirmationAsync(user.Email, user.Nombre, listing.Titulo);

        return Ok();
    }
}

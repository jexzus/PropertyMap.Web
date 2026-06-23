using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PropertyMap.Core.DTOs.Consultas;
using PropertyMap.Core.Entities;
using PropertyMap.Core.Enums;
using PropertyMap.Core.Interfaces;

namespace PropertyMap.Api.Controllers;

[ApiController]
[Route("api/consultas")]
[Authorize]
public class ConsultasController : ControllerBase
{
    private readonly IConsultaRepository _consultas;
    private readonly IListingRepository _listings;
    private readonly IEmailService _email;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<ConsultasController> _logger;

    public ConsultasController(
        IConsultaRepository consultas,
        IListingRepository listings,
        IEmailService email,
        UserManager<ApplicationUser> userManager,
        ILogger<ConsultasController> logger)
    {
        _consultas = consultas;
        _listings = listings;
        _email = email;
        _userManager = userManager;
        _logger = logger;
    }

    // POST /api/consultas — user creates or continues a thread
    [HttpPost]
    public async Task<IActionResult> CreateOrContinue([FromBody] CreateConsultaRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        if (string.IsNullOrWhiteSpace(request.Mensaje))
            return BadRequest("El mensaje no puede estar vacío.");

        var listing = await _listings.GetByIdAsync(request.ListingId);
        if (listing is null)
            return NotFound("La propiedad no existe.");
        if (listing.Publisher?.UserId == userId)
            return BadRequest("No podés consultar tu propia propiedad.");

        var consulta = await _consultas.GetOrCreateAsync(request.ListingId, userId);

        var msg = new ConsultaMensaje
        {
            ConsultaId = consulta.Id,
            SenderId = userId,
            EsDelPublisher = false,
            Mensaje = request.Mensaje,
            FechaEnvio = DateTime.UtcNow
        };
        await _consultas.AddMessageAsync(msg);

        try
        {
            var publisherUserId = consulta.PropertyListing.Publisher?.UserId;
            if (publisherUserId is not null)
            {
                var publisher = await _userManager.FindByIdAsync(publisherUserId);
                var user = await _userManager.FindByIdAsync(userId);

                await _consultas.CreateNotificationAsync(new Notification
                {
                    UserId = publisherUserId,
                    Tipo = TipoNotificacion.NuevaConsulta,
                    Titulo = "Nueva consulta",
                    Mensaje = $"{user!.Nombre} {user.Apellido} te envió una consulta sobre {consulta.PropertyListing.Titulo}",
                    UrlAccion = $"/publisher/consultas/{consulta.Id}"
                });

                await _email.SendNuevaConsultaAsync(
                    publisher!.Email!,
                    $"{publisher.Nombre} {publisher.Apellido}",
                    consulta.PropertyListing.Titulo,
                    $"{user.Nombre} {user.Apellido}",
                    request.Mensaje);
            }
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to send notification for consulta {Id}", consulta.Id); }

        var detail = await _consultas.GetByIdAsync(consulta.Id, userId);
        return Ok(detail);
    }

    // GET /api/consultas — user's inbox
    [HttpGet]
    public async Task<IActionResult> GetMyConsultas()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return Ok(await _consultas.GetByUserAsync(userId));
    }

    // GET /api/consultas/publisher — publisher's inbox
    [HttpGet("publisher")]
    [Authorize(Roles = "Publisher")]
    public async Task<IActionResult> GetPublisherConsultas()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return Ok(await _consultas.GetByPublisherAsync(userId));
    }

    // GET /api/consultas/{id}
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetDetail(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var detail = await _consultas.GetByIdAsync(id, userId);
        if (detail is null) return Forbid();
        return Ok(detail);
    }

    // POST /api/consultas/{id}/mensajes — publisher replies
    [HttpPost("{id:int}/mensajes")]
    [Authorize(Roles = "Publisher")]
    public async Task<IActionResult> PublisherReply(int id, [FromBody] SendMensajeRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        if (string.IsNullOrWhiteSpace(request.Mensaje))
            return BadRequest("El mensaje no puede estar vacío.");

        if (!await _consultas.CanPublisherReplyAsync(id, userId))
            return Forbid();

        var msg = new ConsultaMensaje
        {
            ConsultaId = id,
            SenderId = userId,
            EsDelPublisher = true,
            Mensaje = request.Mensaje,
            FechaEnvio = DateTime.UtcNow
        };
        var msgDto = await _consultas.AddMessageAsync(msg);

        try
        {
            var ownerUserId = await _consultas.GetConsultaOwnerUserIdAsync(id);
            if (ownerUserId is not null)
            {
                var owner = await _userManager.FindByIdAsync(ownerUserId);
                var publisher = await _userManager.FindByIdAsync(userId);
                var detail = await _consultas.GetByIdAsync(id, userId);

                await _consultas.CreateNotificationAsync(new Notification
                {
                    UserId = ownerUserId,
                    Tipo = TipoNotificacion.NuevaRespuesta,
                    Titulo = "Nueva respuesta",
                    Mensaje = $"{publisher!.Nombre} {publisher.Apellido} respondió tu consulta sobre {detail!.PropertyTitulo}",
                    UrlAccion = $"/account/consultas/{id}"
                });

                await _email.SendNuevaRespuestaAsync(
                    owner!.Email!,
                    $"{owner.Nombre} {owner.Apellido}",
                    detail.PropertyTitulo,
                    $"{publisher.Nombre} {publisher.Apellido}",
                    request.Mensaje);
            }
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Failed to send reply notification for consulta {Id}", id); }

        return Ok(msgDto);
    }
}

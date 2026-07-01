using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PropertyMap.Core.DTOs.Properties;
using PropertyMap.Core.Entities;
using PropertyMap.Core.Enums;
using PropertyMap.Core.Interfaces;

namespace PropertyMap.Api.Controllers;

[ApiController]
[Route("api/properties")]
public class PropertiesController : ControllerBase
{
    private readonly IListingRepository _listings;
    private readonly IPublisherRepository _publishers;
    private readonly ILocationRepository _locations;
    private readonly IImageService _images;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ISubscriptionRepository _subscriptions;

    public PropertiesController(
        IListingRepository listings,
        IPublisherRepository publishers,
        ILocationRepository locations,
        IImageService images,
        UserManager<ApplicationUser> userManager,
        ISubscriptionRepository subscriptions)
    {
        _listings = listings;
        _publishers = publishers;
        _locations = locations;
        _images = images;
        _userManager = userManager;
        _subscriptions = subscriptions;
    }

    [HttpGet("mine")]
    [Authorize]
    public async Task<IActionResult> GetMine()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var publisher = await _publishers.GetByUserIdAsync(userId);
        if (publisher == null) return Ok(Array.Empty<MyListingDto>());

        var listings = await _listings.GetMyListingsAsync(publisher.Id);
        return Ok(listings);
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Create(CreateListingRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var publisher = await _publishers.GetByUserIdAsync(userId);
        if (publisher == null)
            return BadRequest(new { message = "No tenés un perfil de publisher. Creá uno primero en /api/publisher/profile." });

        var subscription = await _subscriptions.GetByUserIdAsync(userId);
        int? maxPublicaciones = subscription is null ? 3 : subscription.Plan.MaxPublicaciones;

        if (maxPublicaciones is not null)
        {
            var listingsByPublisher = await _listings.GetListingsByPublisherAsync(publisher.Id);
            var publicacionesActuales = listingsByPublisher.Count(l =>
                l.Estado == EstadoPublicacion.Publicada || l.Estado == EstadoPublicacion.PendienteAprobacion);

            if (publicacionesActuales >= maxPublicaciones)
                return BadRequest(new { message = $"Tu plan permite hasta {maxPublicaciones} publicaciones activas. Pausá una o mejorá tu plan en /planes." });
        }

        var location = await _locations.FindByCoordinatesAsync(request.Lat, request.Lng)
                       ?? await _locations.AddAsync(new Location
                       {
                           Latitud = request.Lat,
                           Longitud = request.Lng,
                           DireccionTexto = request.DireccionTexto,
                           Ciudad = request.Ciudad,
                           Provincia = request.Provincia
                       });

        var listing = new PropertyListing
        {
            PublisherId = publisher.Id,
            LocationId = location.Id,
            Titulo = request.Titulo,
            Descripcion = request.Descripcion,
            Precio = request.Precio,
            Moneda = request.Moneda,
            TipoPropiedad = request.TipoPropiedad,
            Operacion = request.Operacion,
            Superficie = request.Superficie,
            SuperficieCubierta = request.SuperficieCubierta,
            Ambientes = request.Ambientes,
            Dormitorios = request.Dormitorios,
            Banos = request.Banos,
            Antiguedad = request.Antiguedad,
            Cochera = request.Cochera,
            Amenities = request.Amenities,
            Estado = EstadoPublicacion.Publicada,
            FechaPublicacion = DateTime.UtcNow,
            FechaActualizacion = DateTime.UtcNow
        };

        var created = await _listings.AddAsync(listing);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, new { id = created.Id });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var listing = await _listings.GetByIdAsync(id);
        if (listing == null) return NotFound();

        if (listing.Estado != EstadoPublicacion.Publicada)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return NotFound();
            var publisher = await _publishers.GetByUserIdAsync(userId);
            if (publisher == null || publisher.Id != listing.PublisherId) return NotFound();
        }

        var detail = await _listings.GetByIdAsDetailAsync(id);
        return Ok(detail);
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Publisher")]
    public async Task<IActionResult> Update(int id, UpdateListingRequest request)
    {
        var listing = await _listings.GetByIdAsync(id);
        if (listing == null) return NotFound();

        if (!await IsOwner(listing)) return Forbid();

        if (listing.Location.Latitud != request.Lat || listing.Location.Longitud != request.Lng
            || listing.Location.DireccionTexto != request.DireccionTexto)
        {
            var location = await _locations.FindByCoordinatesAsync(request.Lat, request.Lng)
                           ?? await _locations.AddAsync(new Location
                           {
                               Latitud = request.Lat,
                               Longitud = request.Lng,
                               DireccionTexto = request.DireccionTexto,
                               Ciudad = request.Ciudad,
                               Provincia = request.Provincia
                           });
            listing.LocationId = location.Id;
        }

        listing.Titulo = request.Titulo;
        listing.Descripcion = request.Descripcion;
        listing.Precio = request.Precio;
        listing.Moneda = request.Moneda;
        listing.TipoPropiedad = request.TipoPropiedad;
        listing.Operacion = request.Operacion;
        listing.Superficie = request.Superficie;
        listing.SuperficieCubierta = request.SuperficieCubierta;
        listing.Ambientes = request.Ambientes;
        listing.Dormitorios = request.Dormitorios;
        listing.Banos = request.Banos;
        listing.Antiguedad = request.Antiguedad;
        listing.Cochera = request.Cochera;
        listing.Amenities = request.Amenities;
        listing.FechaActualizacion = DateTime.UtcNow;

        if (listing.Estado == EstadoPublicacion.Publicada)
            listing.Estado = EstadoPublicacion.PendienteAprobacion;

        await _listings.UpdateAsync(listing);
        return NoContent();
    }

    [HttpPatch("{id:int}/status")]
    [Authorize]
    public async Task<IActionResult> UpdateStatus(int id, UpdateListingStatusRequest request)
    {
        var listing = await _listings.GetByIdAsync(id);
        if (listing == null) return NotFound();

        var isAdmin = User.IsInRole("Admin");
        if (!isAdmin && !await IsOwner(listing)) return Forbid();

        if (!isAdmin)
        {
            var allowed = new[] { EstadoPublicacion.Pausada, EstadoPublicacion.PendienteAprobacion };
            if (!allowed.Contains(request.NuevoEstado))
                return BadRequest(new { message = "Solo podés pausar o enviar a revisión tu propiedad." });
        }

        listing.Estado = request.NuevoEstado;
        listing.FechaActualizacion = DateTime.UtcNow;
        await _listings.UpdateAsync(listing);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Publisher")]
    public async Task<IActionResult> Delete(int id)
    {
        var listing = await _listings.GetByIdAsync(id);
        if (listing == null) return NotFound();

        if (!await IsOwner(listing)) return Forbid();

        listing.Estado = EstadoPublicacion.Eliminada;
        listing.FechaActualizacion = DateTime.UtcNow;
        await _listings.UpdateAsync(listing);

        return NoContent();
    }

    [HttpPost("{id:int}/images")]
    [Authorize(Roles = "Publisher")]
    [RequestSizeLimit(50 * 1024 * 1024)]
    public async Task<IActionResult> UploadImages(int id, IFormFileCollection files)
    {
        var listing = await _listings.GetByIdAsync(id);
        if (listing == null) return NotFound();

        if (!await IsOwner(listing)) return Forbid();

        if (files.Count == 0)
            return BadRequest(new { message = "No se recibieron archivos." });

        var urls = await _images.SaveImagesAsync(files, id);
        if (urls.Count == 0)
            return BadRequest(new { message = "Ningún archivo era válido. Formatos aceptados: jpg, png, webp. Máximo 10 MB por imagen." });

        var existingCount = listing.Images?.Count ?? 0;
        foreach (var (url, idx) in urls.Select((u, i) => (u, i)))
        {
            listing.Images!.Add(new PropertyImage
            {
                Url = url,
                Orden = existingCount + idx,
                EsPrincipal = existingCount == 0 && idx == 0
            });
        }

        await _listings.UpdateAsync(listing);
        return Ok(new { urls });
    }

    [HttpDelete("{id:int}/images")]
    [Authorize(Roles = "Publisher")]
    public async Task<IActionResult> DeleteImage(int id, [FromQuery] string url)
    {
        var listing = await _listings.GetByIdAsync(id);
        if (listing == null) return NotFound();

        if (!await IsOwner(listing)) return Forbid();

        var image = listing.Images?.FirstOrDefault(i => i.Url == url);
        if (image == null) return NotFound(new { message = "Imagen no encontrada." });

        listing.Images!.Remove(image);

        if (image.EsPrincipal && listing.Images!.Any())
        {
            var first = listing.Images!.OrderBy(i => i.Orden).First();
            first.EsPrincipal = true;
        }

        await _listings.UpdateAsync(listing);
        await _images.DeleteImageAsync(url);

        return NoContent();
    }

    [HttpPatch("{id:int}/destacar")]
    [Authorize(Roles = "Publisher")]
    public async Task<IActionResult> ToggleDestacado(int id)
    {
        var listing = await _listings.GetByIdAsync(id);
        if (listing == null) return NotFound();

        if (!await IsOwner(listing)) return Forbid();

        if (!listing.Destacado)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var subscription = await _subscriptions.GetByUserIdAsync(userId);
            var limite = subscription?.Plan.DestacadosIncluidos ?? 0;

            var listingsByPublisher = await _listings.GetListingsByPublisherAsync(listing.PublisherId);
            var destacadosActuales = listingsByPublisher.Count(l => l.Destacado);

            if (destacadosActuales >= limite)
                return BadRequest(new { message = $"Tu plan permite hasta {limite} propiedades destacadas. Desactivá una o mejorá tu plan en /planes." });
        }

        listing.Destacado = !listing.Destacado;
        listing.FechaActualizacion = DateTime.UtcNow;
        await _listings.UpdateAsync(listing);

        return Ok(new { destacado = listing.Destacado });
    }

    private async Task<bool> IsOwner(PropertyListing listing)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var publisher = await _publishers.GetByUserIdAsync(userId);
        return publisher != null && publisher.Id == listing.PublisherId;
    }
}

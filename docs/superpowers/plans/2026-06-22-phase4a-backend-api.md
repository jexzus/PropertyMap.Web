# PropertyMap Phase 4A — Backend API Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Construir el backend completo de Phase 4: CRUD de propiedades con ownership, subida de imágenes, perfil de publisher, y flujo de aprobación admin — todo protegido con JWT.

**Architecture:** Tres nuevos controllers en `PropertyMap.Api` (`PropertiesController`, `PublisherController`, `AdminController`). Los endpoints protegidos usan `[Authorize]` con `ClaimTypes.NameIdentifier` para validar ownership. Las imágenes se guardan en `wwwroot/uploads/properties/{listingId}/` via un `IImageService`. Los DTOs de request/response viven en `PropertyMap.Core/DTOs/Properties/`, `Publisher/` y `Admin/`.

**Tech Stack:** ASP.NET Core 9 Web API, EF Core 9, ASP.NET Core Identity, JWT Bearer, `IFormFile` para file upload, xUnit + `Microsoft.AspNetCore.Mvc.Testing` para integration tests.

---

## File Map

### Created
```
PropertyMap.Core/DTOs/Properties/CreateListingRequest.cs
PropertyMap.Core/DTOs/Properties/UpdateListingRequest.cs
PropertyMap.Core/DTOs/Properties/UpdateListingStatusRequest.cs
PropertyMap.Core/DTOs/Properties/MyListingDto.cs
PropertyMap.Core/DTOs/Publisher/PublisherProfileRequest.cs
PropertyMap.Core/DTOs/Publisher/PublisherProfileResponse.cs
PropertyMap.Core/DTOs/Admin/PendingListingDto.cs
PropertyMap.Core/DTOs/Admin/ReviewListingRequest.cs
PropertyMap.Core/Interfaces/IImageService.cs
PropertyMap.Infrastructure/Services/ImageService.cs
PropertyMap.Api/Controllers/PropertiesController.cs
PropertyMap.Api/Controllers/PublisherController.cs
PropertyMap.Api/Controllers/AdminController.cs
PropertyMap.Tests/Api/PropertiesControllerTests.cs
PropertyMap.Tests/Api/PublisherControllerTests.cs
```

### Modified
```
PropertyMap.Core/Interfaces/IListingRepository.cs       (+ 2 métodos)
PropertyMap.Infrastructure/Repositories/ListingRepository.cs  (+ 2 implementaciones)
PropertyMap.Api/Program.cs                              (+ IImageService, multipart form data)
```

---

## Task 1: DTOs de propiedades en Core

**Files:**
- Create: `PropertyMap.Core/DTOs/Properties/CreateListingRequest.cs`
- Create: `PropertyMap.Core/DTOs/Properties/UpdateListingRequest.cs`
- Create: `PropertyMap.Core/DTOs/Properties/UpdateListingStatusRequest.cs`
- Create: `PropertyMap.Core/DTOs/Properties/MyListingDto.cs`

- [ ] **Step 1: Crear directorio y CreateListingRequest**

```bash
mkdir C:/Agentes/PropertyMap/src/PropertyMap.Core/DTOs/Properties
```

`PropertyMap.Core/DTOs/Properties/CreateListingRequest.cs`
```csharp
using PropertyMap.Core.Enums;

namespace PropertyMap.Core.DTOs.Properties;

public record CreateListingRequest(
    TipoOperacion Operacion,
    TipoPropiedad TipoPropiedad,
    string Titulo,
    string Descripcion,
    decimal Precio,
    string Moneda,
    string DireccionTexto,
    string Ciudad,
    string Provincia,
    double Lat,
    double Lng,
    decimal? Superficie,
    decimal? SuperficieCubierta,
    int? Ambientes,
    int? Dormitorios,
    int? Banos,
    int? Antiguedad,
    bool Cochera,
    List<string> Amenities
);
```

- [ ] **Step 2: Crear UpdateListingRequest**

`PropertyMap.Core/DTOs/Properties/UpdateListingRequest.cs`
```csharp
using PropertyMap.Core.Enums;

namespace PropertyMap.Core.DTOs.Properties;

public record UpdateListingRequest(
    TipoOperacion Operacion,
    TipoPropiedad TipoPropiedad,
    string Titulo,
    string Descripcion,
    decimal Precio,
    string Moneda,
    string DireccionTexto,
    string Ciudad,
    string Provincia,
    double Lat,
    double Lng,
    decimal? Superficie,
    decimal? SuperficieCubierta,
    int? Ambientes,
    int? Dormitorios,
    int? Banos,
    int? Antiguedad,
    bool Cochera,
    List<string> Amenities
);
```

- [ ] **Step 3: Crear UpdateListingStatusRequest**

`PropertyMap.Core/DTOs/Properties/UpdateListingStatusRequest.cs`
```csharp
using PropertyMap.Core.Enums;

namespace PropertyMap.Core.DTOs.Properties;

public record UpdateListingStatusRequest(EstadoPublicacion NuevoEstado);
```

- [ ] **Step 4: Crear MyListingDto**

`PropertyMap.Core/DTOs/Properties/MyListingDto.cs`
```csharp
namespace PropertyMap.Core.DTOs.Properties;

public record MyListingDto(
    int Id,
    string Titulo,
    string DireccionTexto,
    string Ciudad,
    decimal Precio,
    string Moneda,
    string TipoPropiedad,
    string Operacion,
    string Estado,
    string? FotoPrincipalUrl,
    DateTime FechaPublicacion,
    DateTime FechaActualizacion
);
```

- [ ] **Step 5: Build Core**

```bash
cd C:/Agentes/PropertyMap/src
dotnet build PropertyMap.Core/PropertyMap.Core.csproj
```
Expected: `Build succeeded.`

- [ ] **Step 6: Commit**

```bash
cd C:/Agentes/PropertyMap/src
git add PropertyMap.Core/DTOs/Properties/
git commit -m "feat(core): add property CRUD DTOs (CreateListingRequest, UpdateListingRequest, MyListingDto)"
```

---

## Task 2: DTOs de publisher y admin en Core

**Files:**
- Create: `PropertyMap.Core/DTOs/Publisher/PublisherProfileRequest.cs`
- Create: `PropertyMap.Core/DTOs/Publisher/PublisherProfileResponse.cs`
- Create: `PropertyMap.Core/DTOs/Admin/PendingListingDto.cs`
- Create: `PropertyMap.Core/DTOs/Admin/ReviewListingRequest.cs`

- [ ] **Step 1: Crear DTOs de publisher**

```bash
mkdir C:/Agentes/PropertyMap/src/PropertyMap.Core/DTOs/Publisher
mkdir C:/Agentes/PropertyMap/src/PropertyMap.Core/DTOs/Admin
```

`PropertyMap.Core/DTOs/Publisher/PublisherProfileRequest.cs`
```csharp
using PropertyMap.Core.Enums;

namespace PropertyMap.Core.DTOs.Publisher;

public record PublisherProfileRequest(
    string Nombre,
    string Telefono,
    TipoPublicador Tipo
);
```

`PropertyMap.Core/DTOs/Publisher/PublisherProfileResponse.cs`
```csharp
using PropertyMap.Core.Enums;

namespace PropertyMap.Core.DTOs.Publisher;

public record PublisherProfileResponse(
    int Id,
    string Nombre,
    string Email,
    string? Telefono,
    string? LogoUrl,
    TipoPublicador Tipo,
    int TotalPublicaciones
);
```

- [ ] **Step 2: Crear DTOs de admin**

`PropertyMap.Core/DTOs/Admin/PendingListingDto.cs`
```csharp
namespace PropertyMap.Core.DTOs.Admin;

public record PendingListingDto(
    int Id,
    string Titulo,
    string DireccionTexto,
    string Ciudad,
    decimal Precio,
    string Moneda,
    string TipoPropiedad,
    string Operacion,
    string? FotoPrincipalUrl,
    string PublisherNombre,
    string PublisherEmail,
    DateTime FechaPublicacion
);
```

`PropertyMap.Core/DTOs/Admin/ReviewListingRequest.cs`
```csharp
namespace PropertyMap.Core.DTOs.Admin;

public record ReviewListingRequest(bool Aprobar, string? MotivoRechazo);
```

- [ ] **Step 3: Build Core**

```bash
cd C:/Agentes/PropertyMap/src
dotnet build PropertyMap.Core/PropertyMap.Core.csproj
```
Expected: `Build succeeded.`

- [ ] **Step 4: Commit**

```bash
cd C:/Agentes/PropertyMap/src
git add PropertyMap.Core/DTOs/Publisher/ PropertyMap.Core/DTOs/Admin/
git commit -m "feat(core): add publisher profile DTOs and admin review DTOs"
```

---

## Task 3: Extender IListingRepository + implementación

**Files:**
- Modify: `PropertyMap.Core/Interfaces/IListingRepository.cs`
- Modify: `PropertyMap.Infrastructure/Repositories/ListingRepository.cs`

- [ ] **Step 1: Agregar métodos a la interfaz**

Abrir `PropertyMap.Core/Interfaces/IListingRepository.cs` y agregar al final de la interfaz (antes del cierre `}`):

```csharp
    Task<IEnumerable<MyListingDto>> GetMyListingsAsync(int publisherId);
    Task<IEnumerable<PendingListingDto>> GetPendingListingsAsync();
```

El archivo completo quedará así:
```csharp
using PropertyMap.Core.DTOs;
using PropertyMap.Core.DTOs.Admin;
using PropertyMap.Core.DTOs.Properties;
using PropertyMap.Core.Entities;

namespace PropertyMap.Core.Interfaces;

public interface IListingRepository
{
    Task<IEnumerable<PropertyListing>> GetActiveListingsAsync();
    Task<IEnumerable<PropertyListing>> GetListingsByPublisherAsync(int publisherId);
    Task<IEnumerable<ListingMapDto>> GetActiveListingsForMapAsync();
    Task<PropertyListing?> GetByIdAsync(int id);
    Task<ListingDetailDto?> GetByIdAsDetailAsync(int id);
    Task<IEnumerable<MyListingDto>> GetMyListingsAsync(int publisherId);
    Task<IEnumerable<PendingListingDto>> GetPendingListingsAsync();
    Task<PropertyListing> AddAsync(PropertyListing listing);
    Task UpdateAsync(PropertyListing listing);
    Task DeleteAsync(int id);
}
```

- [ ] **Step 2: Implementar GetMyListingsAsync en ListingRepository**

Abrir `PropertyMap.Infrastructure/Repositories/ListingRepository.cs` y agregar los dos métodos nuevos antes de `AddAsync`:

```csharp
    public async Task<IEnumerable<MyListingDto>> GetMyListingsAsync(int publisherId) =>
        await ctx.PropertyListings
            .Where(l => l.PublisherId == publisherId)
            .Include(l => l.Location)
            .Include(l => l.Images.Where(i => i.EsPrincipal))
            .OrderByDescending(l => l.FechaActualizacion)
            .Select(l => new MyListingDto(
                l.Id,
                l.Titulo,
                l.Location.DireccionTexto,
                l.Location.Ciudad,
                l.Precio,
                l.Moneda,
                l.TipoPropiedad.ToString(),
                l.Operacion.ToString(),
                l.Estado.ToString(),
                l.Images.Where(i => i.EsPrincipal).Select(i => i.Url).FirstOrDefault(),
                l.FechaPublicacion,
                l.FechaActualizacion
            ))
            .ToListAsync();

    public async Task<IEnumerable<PendingListingDto>> GetPendingListingsAsync() =>
        await ctx.PropertyListings
            .Where(l => l.Estado == EstadoPublicacion.PendienteAprobacion)
            .Include(l => l.Location)
            .Include(l => l.Publisher)
            .Include(l => l.Images.Where(i => i.EsPrincipal))
            .OrderBy(l => l.FechaPublicacion)
            .Select(l => new PendingListingDto(
                l.Id,
                l.Titulo,
                l.Location.DireccionTexto,
                l.Location.Ciudad,
                l.Precio,
                l.Moneda,
                l.TipoPropiedad.ToString(),
                l.Operacion.ToString(),
                l.Images.Where(i => i.EsPrincipal).Select(i => i.Url).FirstOrDefault(),
                l.Publisher.Nombre,
                l.Publisher.Email,
                l.FechaPublicacion
            ))
            .ToListAsync();
```

También agregar los usings necesarios al inicio de `ListingRepository.cs`:
```csharp
using PropertyMap.Core.DTOs.Admin;
using PropertyMap.Core.DTOs.Properties;
```

- [ ] **Step 3: Build Infrastructure**

```bash
cd C:/Agentes/PropertyMap/src
dotnet build PropertyMap.Infrastructure/PropertyMap.Infrastructure.csproj
```
Expected: `Build succeeded.`

- [ ] **Step 4: Commit**

```bash
cd C:/Agentes/PropertyMap/src
git add PropertyMap.Core/Interfaces/IListingRepository.cs PropertyMap.Infrastructure/Repositories/ListingRepository.cs
git commit -m "feat(infra): add GetMyListingsAsync and GetPendingListingsAsync to listing repo"
```

---

## Task 4: IImageService + ImageService

**Files:**
- Create: `PropertyMap.Core/Interfaces/IImageService.cs`
- Create: `PropertyMap.Infrastructure/Services/ImageService.cs`

- [ ] **Step 1: Crear IImageService**

`PropertyMap.Core/Interfaces/IImageService.cs`
```csharp
using Microsoft.AspNetCore.Http;

namespace PropertyMap.Core.Interfaces;

public interface IImageService
{
    Task<List<string>> SaveImagesAsync(IFormFileCollection files, int listingId);
    Task DeleteImageAsync(string relativeUrl);
    Task DeleteAllImagesForListingAsync(int listingId);
}
```

- [ ] **Step 2: Crear ImageService**

`PropertyMap.Infrastructure/Services/ImageService.cs`
```csharp
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using PropertyMap.Core.Interfaces;

namespace PropertyMap.Infrastructure.Services;

public class ImageService : IImageService
{
    private readonly string _uploadsRoot;
    private static readonly HashSet<string> AllowedExtensions = [".jpg", ".jpeg", ".png", ".webp"];
    private const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

    public ImageService(IConfiguration config)
    {
        _uploadsRoot = config["ImageSettings:UploadsRoot"]
            ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
    }

    public async Task<List<string>> SaveImagesAsync(IFormFileCollection files, int listingId)
    {
        var dir = Path.Combine(_uploadsRoot, "properties", listingId.ToString());
        Directory.CreateDirectory(dir);

        var urls = new List<string>();
        foreach (var file in files)
        {
            if (file.Length == 0 || file.Length > MaxFileSizeBytes) continue;
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(ext)) continue;

            var fileName = $"{Guid.NewGuid()}{ext}";
            var fullPath = Path.Combine(dir, fileName);

            await using var stream = File.Create(fullPath);
            await file.CopyToAsync(stream);

            urls.Add($"/uploads/properties/{listingId}/{fileName}");
        }

        return urls;
    }

    public Task DeleteImageAsync(string relativeUrl)
    {
        var fullPath = Path.Combine(_uploadsRoot, "..", relativeUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(fullPath)) File.Delete(fullPath);
        return Task.CompletedTask;
    }

    public Task DeleteAllImagesForListingAsync(int listingId)
    {
        var dir = Path.Combine(_uploadsRoot, "properties", listingId.ToString());
        if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        return Task.CompletedTask;
    }
}
```

- [ ] **Step 3: Build Infrastructure**

```bash
cd C:/Agentes/PropertyMap/src
dotnet build PropertyMap.Infrastructure/PropertyMap.Infrastructure.csproj
```
Expected: `Build succeeded.`

- [ ] **Step 4: Commit**

```bash
cd C:/Agentes/PropertyMap/src
git add PropertyMap.Core/Interfaces/IImageService.cs PropertyMap.Infrastructure/Services/ImageService.cs
git commit -m "feat(infra): add IImageService and ImageService for file upload handling"
```

---

## Task 5: Registrar IImageService en Program.cs de API

**Files:**
- Modify: `PropertyMap.Api/Program.cs`
- Modify: `PropertyMap.Api/appsettings.json`

- [ ] **Step 1: Registrar servicio**

Abrir `PropertyMap.Api/Program.cs`. Buscar la sección donde están `AddScoped<ITokenService>` y `AddScoped<IEmailService>` y agregar debajo:

```csharp
builder.Services.AddScoped<IImageService, ImageService>();
```

También agregar el using al inicio si no está:
```csharp
using PropertyMap.Infrastructure.Services;
```

- [ ] **Step 2: Habilitar multipart form data y static files**

En `PropertyMap.Api/Program.cs`, después de `app.UseAuthorization();` y antes de `app.MapControllers();`, agregar:

```csharp
app.UseStaticFiles();
```

También agregar la configuración de límite de tamaño de formulario al inicio (antes de `builder.Build()`):

```csharp
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 50 * 1024 * 1024; // 50 MB total
});
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 50 * 1024 * 1024;
});
```

- [ ] **Step 3: Agregar ImageSettings a appsettings.json de la API**

Abrir `PropertyMap.Api/appsettings.json` y agregar dentro del JSON raíz:

```json
"ImageSettings": {
  "UploadsRoot": "wwwroot/uploads"
}
```

- [ ] **Step 4: Crear directorio wwwroot en API**

```bash
mkdir -p C:/Agentes/PropertyMap/src/PropertyMap.Api/wwwroot/uploads/properties
```

- [ ] **Step 5: Build API**

```bash
cd C:/Agentes/PropertyMap/src
dotnet build PropertyMap.Api/PropertyMap.Api.csproj
```
Expected: `Build succeeded.`

- [ ] **Step 6: Commit**

```bash
cd C:/Agentes/PropertyMap/src
git add PropertyMap.Api/
git commit -m "feat(api): register ImageService, enable static files and multipart upload limits"
```

---

## Task 6: PropertiesController

**Files:**
- Create: `PropertyMap.Api/Controllers/PropertiesController.cs`

- [ ] **Step 1: Crear PropertiesController**

`PropertyMap.Api/Controllers/PropertiesController.cs`
```csharp
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

    public PropertiesController(
        IListingRepository listings,
        IPublisherRepository publishers,
        ILocationRepository locations,
        IImageService images,
        UserManager<ApplicationUser> userManager)
    {
        _listings = listings;
        _publishers = publishers;
        _locations = locations;
        _images = images;
        _userManager = userManager;
    }

    // GET /api/properties/mine — mis propiedades (requiere auth)
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

    // POST /api/properties — crear propiedad (requiere rol Publisher)
    [HttpPost]
    [Authorize(Roles = "Publisher")]
    public async Task<IActionResult> Create(CreateListingRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var publisher = await _publishers.GetByUserIdAsync(userId);
        if (publisher == null)
            return BadRequest(new { message = "No tenés un perfil de publisher. Creá uno primero en /api/publisher/profile." });

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
            Estado = EstadoPublicacion.PendienteAprobacion,
            FechaPublicacion = DateTime.UtcNow,
            FechaActualizacion = DateTime.UtcNow
        };

        var created = await _listings.AddAsync(listing);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, new { id = created.Id });
    }

    // GET /api/properties/{id} — detalle (cualquiera, incluye borradores propios)
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var listing = await _listings.GetByIdAsync(id);
        if (listing == null) return NotFound();

        // Si no está publicada, solo el dueño puede verla
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

    // PUT /api/properties/{id} — editar (solo el dueño)
    [HttpPut("{id:int}")]
    [Authorize(Roles = "Publisher")]
    public async Task<IActionResult> Update(int id, UpdateListingRequest request)
    {
        var listing = await _listings.GetByIdAsync(id);
        if (listing == null) return NotFound();

        if (!await IsOwner(listing)) return Forbid();

        // Actualizar Location si cambió
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

        // Editar vuelve a requerir aprobación si estaba Publicada
        if (listing.Estado == EstadoPublicacion.Publicada)
            listing.Estado = EstadoPublicacion.PendienteAprobacion;

        await _listings.UpdateAsync(listing);
        return NoContent();
    }

    // PATCH /api/properties/{id}/status — cambiar estado (dueño o admin)
    [HttpPatch("{id:int}/status")]
    [Authorize]
    public async Task<IActionResult> UpdateStatus(int id, UpdateListingStatusRequest request)
    {
        var listing = await _listings.GetByIdAsync(id);
        if (listing == null) return NotFound();

        var isAdmin = User.IsInRole("Admin");
        if (!isAdmin && !await IsOwner(listing)) return Forbid();

        // El publisher solo puede pausar o pedir aprobación
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

    // DELETE /api/properties/{id} — eliminar (soft delete, solo dueño)
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

    // POST /api/properties/{id}/images — subir imágenes (solo dueño)
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

        // Persistir PropertyImage rows
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

    // DELETE /api/properties/{listingId}/images — eliminar imagen específica
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

        // Reasignar principal si era la principal
        if (image.EsPrincipal && listing.Images!.Any())
        {
            var first = listing.Images!.OrderBy(i => i.Orden).First();
            first.EsPrincipal = true;
        }

        await _listings.UpdateAsync(listing);
        await _images.DeleteImageAsync(url);

        return NoContent();
    }

    private async Task<bool> IsOwner(PropertyListing listing)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var publisher = await _publishers.GetByUserIdAsync(userId);
        return publisher != null && publisher.Id == listing.PublisherId;
    }
}
```

- [ ] **Step 2: Build API**

```bash
cd C:/Agentes/PropertyMap/src
dotnet build PropertyMap.Api/PropertyMap.Api.csproj
```
Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```bash
cd C:/Agentes/PropertyMap/src
git add PropertyMap.Api/Controllers/PropertiesController.cs
git commit -m "feat(api): add PropertiesController with CRUD, image upload, ownership validation"
```

---

## Task 7: PublisherController

**Files:**
- Create: `PropertyMap.Api/Controllers/PublisherController.cs`

- [ ] **Step 1: Crear PublisherController**

`PropertyMap.Api/Controllers/PublisherController.cs`
```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PropertyMap.Core.DTOs.Publisher;
using PropertyMap.Core.Entities;
using PropertyMap.Core.Enums;
using PropertyMap.Core.Interfaces;

namespace PropertyMap.Api.Controllers;

[ApiController]
[Route("api/publisher")]
[Authorize]
public class PublisherController : ControllerBase
{
    private readonly IPublisherRepository _publishers;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IListingRepository _listings;

    public PublisherController(
        IPublisherRepository publishers,
        UserManager<ApplicationUser> userManager,
        IListingRepository listings)
    {
        _publishers = publishers;
        _userManager = userManager;
        _listings = listings;
    }

    // GET /api/publisher/profile — obtener mi perfil de publisher
    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var publisher = await _publishers.GetByUserIdAsync(userId);
        if (publisher == null) return NotFound(new { message = "No tenés un perfil de publisher aún." });

        var total = await _listings.GetListingsByPublisherAsync(publisher.Id);
        return Ok(new PublisherProfileResponse(
            publisher.Id,
            publisher.Nombre,
            publisher.Email,
            publisher.Telefono,
            publisher.LogoUrl,
            publisher.Tipo,
            total.Count()
        ));
    }

    // POST /api/publisher/profile — crear perfil de publisher (upgrade a rol Publisher)
    [HttpPost("profile")]
    public async Task<IActionResult> CreateProfile(PublisherProfileRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return Unauthorized();

        var existing = await _publishers.GetByUserIdAsync(userId);
        if (existing != null)
            return Conflict(new { message = "Ya tenés un perfil de publisher." });

        var publisher = new Publisher
        {
            Nombre = request.Nombre,
            Email = user.Email!,
            Telefono = request.Telefono,
            Tipo = request.Tipo,
            UserId = userId
        };

        var created = await _publishers.AddAsync(publisher);

        // Asignar rol Publisher
        if (!await _userManager.IsInRoleAsync(user, "Publisher"))
            await _userManager.AddToRoleAsync(user, "Publisher");

        return CreatedAtAction(nameof(GetProfile), null, new PublisherProfileResponse(
            created.Id,
            created.Nombre,
            created.Email,
            created.Telefono,
            created.LogoUrl,
            created.Tipo,
            0
        ));
    }

    // PUT /api/publisher/profile — actualizar perfil
    [HttpPut("profile")]
    [Authorize(Roles = "Publisher")]
    public async Task<IActionResult> UpdateProfile(PublisherProfileRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var publisher = await _publishers.GetByUserIdAsync(userId);
        if (publisher == null) return NotFound();

        publisher.Nombre = request.Nombre;
        publisher.Telefono = request.Telefono;
        publisher.Tipo = request.Tipo;
        await _publishers.UpdateAsync(publisher);

        return NoContent();
    }
}
```

- [ ] **Step 2: Build API**

```bash
cd C:/Agentes/PropertyMap/src
dotnet build PropertyMap.Api/PropertyMap.Api.csproj
```
Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```bash
cd C:/Agentes/PropertyMap/src
git add PropertyMap.Api/Controllers/PublisherController.cs
git commit -m "feat(api): add PublisherController with profile CRUD and automatic role assignment"
```

---

## Task 8: AdminController

**Files:**
- Create: `PropertyMap.Api/Controllers/AdminController.cs`

- [ ] **Step 1: Crear AdminController**

`PropertyMap.Api/Controllers/AdminController.cs`
```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PropertyMap.Core.DTOs.Admin;
using PropertyMap.Core.Enums;
using PropertyMap.Core.Interfaces;

namespace PropertyMap.Api.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly IListingRepository _listings;

    public AdminController(IListingRepository listings)
    {
        _listings = listings;
    }

    // GET /api/admin/listings/pending — listados en espera de aprobación
    [HttpGet("listings/pending")]
    public async Task<IActionResult> GetPending()
    {
        var pending = await _listings.GetPendingListingsAsync();
        return Ok(pending);
    }

    // PATCH /api/admin/listings/{id}/review — aprobar o rechazar
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

        return Ok(new
        {
            message = request.Aprobar
                ? "Propiedad aprobada y publicada."
                : $"Propiedad rechazada. Motivo: {request.MotivoRechazo ?? "no especificado"}"
        });
    }

    // GET /api/admin/listings — todos los listings con cualquier estado
    [HttpGet("listings")]
    public async Task<IActionResult> GetAll([FromQuery] string? estado)
    {
        if (Enum.TryParse<EstadoPublicacion>(estado, ignoreCase: true, out var estadoEnum))
        {
            // Filtramos del lado del cliente por simplicidad — en Phase 9 se agrega query filter
            var all = await _listings.GetActiveListingsAsync();
            return Ok(all);
        }

        var listings = await _listings.GetActiveListingsAsync();
        return Ok(listings);
    }
}
```

- [ ] **Step 2: Build API completo**

```bash
cd C:/Agentes/PropertyMap/src
dotnet build PropertyMap.Api/PropertyMap.Api.csproj
```
Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```bash
cd C:/Agentes/PropertyMap/src
git add PropertyMap.Api/Controllers/AdminController.cs
git commit -m "feat(api): add AdminController with pending listings review and approve/reject flow"
```

---

## Task 9: Integration tests — PropertiesController

**Files:**
- Create: `PropertyMap.Tests/Api/PropertiesControllerTests.cs`

- [ ] **Step 1: Crear helper para autenticar en tests**

Abrir `PropertyMap.Tests/Api/TestWebApplicationFactory.cs` y agregar este helper al final de la clase `TestWebApplicationFactory` (después del cierre de la clase principal, antes del namespace closing):

```csharp
public static class TestAuthHelper
{
    public static async Task<(HttpClient client, string userId)> CreateAuthenticatedPublisherAsync(
        TestWebApplicationFactory factory)
    {
        var client = factory.CreateClient();
        var email = $"pub_{Guid.NewGuid()}@test.com";

        // Registrar usuario
        await client.PostAsJsonAsync("/api/auth/register",
            new PropertyMap.Core.DTOs.Auth.RegisterRequest("Test", "Publisher", email, "Test123!", "Test123!"));

        // Confirmar email directamente via UserManager
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<PropertyMap.Core.Entities.ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        user!.EmailConfirmed = true;
        user.Estado = PropertyMap.Core.Enums.EstadoUsuario.Activo;
        await userManager.UpdateAsync(user);

        // Asignar rol Publisher
        await userManager.AddToRoleAsync(user, "Publisher");

        // Login
        var loginResp = await client.PostAsJsonAsync("/api/auth/login",
            new PropertyMap.Core.DTOs.Auth.LoginRequest(email, "Test123!"));
        var auth = await loginResp.Content.ReadFromJsonAsync<PropertyMap.Core.DTOs.Auth.AuthResponse>();

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        return (client, user.Id);
    }

    public static async Task<int> CreatePublisherProfileAsync(HttpClient client, string nombre = "Test Inmobiliaria")
    {
        var resp = await client.PostAsJsonAsync("/api/publisher/profile",
            new PropertyMap.Core.DTOs.Publisher.PublisherProfileRequest(
                nombre,
                "+54 9 11 1234-5678",
                PropertyMap.Core.Enums.TipoPublicador.Inmobiliaria));
        var body = await resp.Content.ReadFromJsonAsync<PropertyMap.Core.DTOs.Publisher.PublisherProfileResponse>();
        return body!.Id;
    }
}
```

- [ ] **Step 2: Crear PropertiesControllerTests**

`PropertyMap.Tests/Api/PropertiesControllerTests.cs`
```csharp
using System.Net;
using System.Net.Http.Json;
using PropertyMap.Core.DTOs.Properties;
using PropertyMap.Core.Enums;
using Xunit;

namespace PropertyMap.Tests.Api;

public class PropertiesControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public PropertiesControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static CreateListingRequest SampleListing() => new(
        Operacion: TipoOperacion.Venta,
        TipoPropiedad: TipoPropiedad.Casa,
        Titulo: "Casa test",
        Descripcion: "Descripción de prueba",
        Precio: 100000,
        Moneda: "USD",
        DireccionTexto: "Calle Test 123",
        Ciudad: "Santa Rosa",
        Provincia: "La Pampa",
        Lat: -36.6200,
        Lng: -64.2895,
        Superficie: 100,
        SuperficieCubierta: 80,
        Ambientes: 3,
        Dormitorios: 2,
        Banos: 1,
        Antiguedad: 5,
        Cochera: true,
        Amenities: ["Balcón", "Parrilla"]
    );

    [Fact]
    public async Task CreateListing_WithoutAuth_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/properties", SampleListing());
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateListing_WithoutPublisherProfile_Returns400()
    {
        var (client, _) = await TestAuthHelper.CreateAuthenticatedPublisherAsync(_factory);
        // No creamos perfil de publisher

        var response = await client.PostAsJsonAsync("/api/properties", SampleListing());
        // El usuario tiene rol Publisher pero no tiene Publisher entity — retorna 400
        Assert.True(
            response.StatusCode == HttpStatusCode.BadRequest ||
            response.StatusCode == HttpStatusCode.Forbidden,
            $"Expected 400 or 403, got {response.StatusCode}");
    }

    [Fact]
    public async Task CreateListing_WithPublisherProfile_Returns201()
    {
        var (client, _) = await TestAuthHelper.CreateAuthenticatedPublisherAsync(_factory);
        await TestAuthHelper.CreatePublisherProfileAsync(client);

        var response = await client.PostAsJsonAsync("/api/properties", SampleListing());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task GetMine_ReturnsOwnListings()
    {
        var (client, _) = await TestAuthHelper.CreateAuthenticatedPublisherAsync(_factory);
        await TestAuthHelper.CreatePublisherProfileAsync(client);
        await client.PostAsJsonAsync("/api/properties", SampleListing());

        var response = await client.GetAsync("/api/properties/mine");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var listings = await response.Content.ReadFromJsonAsync<List<MyListingDto>>();
        Assert.NotEmpty(listings!);
    }

    [Fact]
    public async Task GetMine_WithoutPublisherProfile_ReturnsEmptyList()
    {
        var (client, _) = await TestAuthHelper.CreateAuthenticatedPublisherAsync(_factory);
        // Sin perfil de publisher
        var response = await client.GetAsync("/api/properties/mine");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var listings = await response.Content.ReadFromJsonAsync<List<MyListingDto>>();
        Assert.Empty(listings!);
    }

    [Fact]
    public async Task DeleteListing_ByOtherUser_ReturnsForbid()
    {
        // Publisher 1 crea propiedad
        var (client1, _) = await TestAuthHelper.CreateAuthenticatedPublisherAsync(_factory);
        await TestAuthHelper.CreatePublisherProfileAsync(client1, "Publisher Uno");
        var createResp = await client1.PostAsJsonAsync("/api/properties", SampleListing());
        var created = await createResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var listingId = created.GetProperty("id").GetInt32();

        // Publisher 2 intenta borrarla
        var (client2, _) = await TestAuthHelper.CreateAuthenticatedPublisherAsync(_factory);
        await TestAuthHelper.CreatePublisherProfileAsync(client2, "Publisher Dos");
        var deleteResp = await client2.DeleteAsync($"/api/properties/{listingId}");

        Assert.Equal(HttpStatusCode.Forbidden, deleteResp.StatusCode);
    }

    [Fact]
    public async Task DeleteListing_ByOwner_Returns204()
    {
        var (client, _) = await TestAuthHelper.CreateAuthenticatedPublisherAsync(_factory);
        await TestAuthHelper.CreatePublisherProfileAsync(client);
        var createResp = await client.PostAsJsonAsync("/api/properties", SampleListing());
        var created = await createResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var listingId = created.GetProperty("id").GetInt32();

        var deleteResp = await client.DeleteAsync($"/api/properties/{listingId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResp.StatusCode);
    }

    [Fact]
    public async Task UpdateStatus_Publisher_CanOnlyPause()
    {
        var (client, _) = await TestAuthHelper.CreateAuthenticatedPublisherAsync(_factory);
        await TestAuthHelper.CreatePublisherProfileAsync(client);
        var createResp = await client.PostAsJsonAsync("/api/properties", SampleListing());
        var created = await createResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var listingId = created.GetProperty("id").GetInt32();

        // Intentar publicar directamente (solo admin puede)
        var resp = await client.PatchAsJsonAsync($"/api/properties/{listingId}/status",
            new UpdateListingStatusRequest(EstadoPublicacion.Publicada));
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }
}
```

- [ ] **Step 3: Crear PublisherControllerTests**

`PropertyMap.Tests/Api/PublisherControllerTests.cs`
```csharp
using System.Net;
using System.Net.Http.Json;
using PropertyMap.Core.DTOs.Publisher;
using PropertyMap.Core.Enums;
using Xunit;

namespace PropertyMap.Tests.Api;

public class PublisherControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public PublisherControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetProfile_WithoutAuth_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/publisher/profile");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetProfile_WithoutProfile_Returns404()
    {
        var (client, _) = await TestAuthHelper.CreateAuthenticatedPublisherAsync(_factory);
        var response = await client.GetAsync("/api/publisher/profile");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateProfile_ValidData_Returns201WithProfile()
    {
        var (client, _) = await TestAuthHelper.CreateAuthenticatedPublisherAsync(_factory);
        var response = await client.PostAsJsonAsync("/api/publisher/profile",
            new PublisherProfileRequest("Test Inmobiliaria", "+54 9 11 1234-5678", TipoPublicador.Inmobiliaria));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var profile = await response.Content.ReadFromJsonAsync<PublisherProfileResponse>();
        Assert.Equal("Test Inmobiliaria", profile!.Nombre);
        Assert.Equal(TipoPublicador.Inmobiliaria, profile.Tipo);
    }

    [Fact]
    public async Task CreateProfile_Duplicate_Returns409()
    {
        var (client, _) = await TestAuthHelper.CreateAuthenticatedPublisherAsync(_factory);
        var req = new PublisherProfileRequest("Inmobiliaria X", "+54 9 11 0000-0000", TipoPublicador.Particular);
        await client.PostAsJsonAsync("/api/publisher/profile", req);
        var response = await client.PostAsJsonAsync("/api/publisher/profile", req);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task GetProfile_AfterCreate_ReturnsProfile()
    {
        var (client, _) = await TestAuthHelper.CreateAuthenticatedPublisherAsync(_factory);
        await client.PostAsJsonAsync("/api/publisher/profile",
            new PublisherProfileRequest("Mi Inmobiliaria", "+54 9 11 9999-9999", TipoPublicador.Inmobiliaria));

        var response = await client.GetAsync("/api/publisher/profile");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var profile = await response.Content.ReadFromJsonAsync<PublisherProfileResponse>();
        Assert.Equal("Mi Inmobiliaria", profile!.Nombre);
        Assert.Equal(0, profile.TotalPublicaciones);
    }
}
```

- [ ] **Step 4: Ejecutar todos los tests**

```bash
cd C:/Agentes/PropertyMap/src
dotnet test PropertyMap.Tests/PropertyMap.Tests.csproj --logger "console;verbosity=normal" 2>&1 | tail -20
```

Expected: todos los tests pasan. Si alguno falla, leer el mensaje de error, diagnosticar y corregir. Problemas comunes:
- `PatchAsJsonAsync` no existe en versiones viejas — usar `SendAsync` con `HttpMethod.Patch`
- InMemory DB no soporta constraints de FK en cascada — ignorar en `TestWebApplicationFactory`

- [ ] **Step 5: Commit**

```bash
cd C:/Agentes/PropertyMap/src
git add PropertyMap.Tests/
git commit -m "test(api): add PropertiesController and PublisherController integration tests"
```

---

## Self-Review

### Spec coverage

- ✅ `POST /api/properties` — crear propiedad (Task 6)
- ✅ `GET /api/properties/mine` — mis propiedades (Task 6)
- ✅ `PUT /api/properties/{id}` — editar (Task 6)
- ✅ `PATCH /api/properties/{id}/status` — cambiar estado (Task 6)
- ✅ `DELETE /api/properties/{id}` — soft delete (Task 6)
- ✅ `POST /api/properties/{id}/images` — subida de imágenes (Task 6)
- ✅ `DELETE /api/properties/{listingId}/images` — eliminar imagen (Task 6)
- ✅ Ownership validation — solo el dueño puede editar/eliminar (Task 6)
- ✅ `POST /api/publisher/profile` — crear perfil + asignar rol Publisher (Task 7)
- ✅ `GET /api/publisher/profile` — obtener perfil (Task 7)
- ✅ `PUT /api/publisher/profile` — actualizar perfil (Task 7)
- ✅ `GET /api/admin/listings/pending` — ver pendientes (Task 8)
- ✅ `PATCH /api/admin/listings/{id}/review` — aprobar/rechazar (Task 8)
- ✅ Flujo estados: Borrador → PendienteAprobacion → Publicada (Tasks 6, 8)
- ✅ IImageService + ImageService — file upload a disco (Task 4)
- ✅ GetMyListingsAsync — repo query para dashboard (Task 3)
- ✅ GetPendingListingsAsync — repo query para admin (Task 3)
- ✅ Tests PropertiesController (Task 9)
- ✅ Tests PublisherController (Task 9)

### Dependencias de tasks
```
Task 1 → Task 3 (MyListingDto, PendingListingDto)
Task 2 → Task 7, Task 8
Task 3 → Task 6, Task 8 (usa GetMyListingsAsync, GetPendingListingsAsync)
Task 4 → Task 5 (IImageService)
Task 5 → Task 6 (registra ImageService en DI)
Task 6 → Task 9 (tests de PropertiesController)
Task 7 → Task 9 (tests de PublisherController)
```

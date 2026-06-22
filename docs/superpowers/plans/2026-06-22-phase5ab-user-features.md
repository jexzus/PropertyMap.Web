# Phase 5A+5B — User Features: Profile, Favorites & View Tracking

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Agregar perfil editable con avatar, favoritos privados (toggle + lista), y view tracking con dedup por día al proyecto PropertyMap.

**Architecture:** Tres features sobre entidades ya existentes (`ApplicationUser`, `PropertyFavorite`, `PropertyView`). Backend: dos controladores nuevos (`UserController`, `FavoritesController`) + `IViewTrackingService` inyectado en el `ListingsController` existente. Frontend: dos servicios nuevos (`UserApiService`, `FavoritesApiService`), dos páginas (`/account/profile`, `/account/favorites`), un componente `FavoriteButton`, y el `Navbar` actualizado.

**Tech Stack:** .NET 9, ASP.NET Core, EF Core 9 (InMemory para tests), Identity, Blazor Server (`@rendermode InteractiveServer`), xUnit, `IHttpClientFactory`.

---

## File Map

### Creados — Backend
```
PropertyMap.Core/DTOs/User/UserProfileResponse.cs
PropertyMap.Core/DTOs/User/UpdateProfileRequest.cs
PropertyMap.Core/DTOs/User/FavoriteStatusResponse.cs
PropertyMap.Core/Interfaces/IFavoriteRepository.cs
PropertyMap.Core/Interfaces/IViewTrackingService.cs
PropertyMap.Infrastructure/Repositories/FavoriteRepository.cs
PropertyMap.Infrastructure/Services/ViewTrackingService.cs
PropertyMap.Api/Controllers/UserController.cs
PropertyMap.Api/Controllers/FavoritesController.cs
PropertyMap.Tests/Api/UserControllerTests.cs
PropertyMap.Tests/Api/FavoritesControllerTests.cs
PropertyMap.Tests/Api/ViewTrackingTests.cs
```

### Modificados — Backend
```
PropertyMap.Core/Interfaces/IImageService.cs           (+ SaveAvatarAsync)
PropertyMap.Infrastructure/Services/ImageService.cs    (+ SaveAvatarAsync)
PropertyMap.Api/Controllers/ListingsController.cs      (+ view tracking en GetById)
PropertyMap.Api/Program.cs                             (+ 3 registros nuevos)
```

### Creados — Frontend
```
PropertyMap.Web/PropertyMap.Web/Services/IUserApiService.cs
PropertyMap.Web/PropertyMap.Web/Services/UserApiService.cs
PropertyMap.Web/PropertyMap.Web/Services/IFavoritesApiService.cs
PropertyMap.Web/PropertyMap.Web/Services/FavoritesApiService.cs
PropertyMap.Web/PropertyMap.Web/Components/Pages/Account/Profile.razor
PropertyMap.Web/PropertyMap.Web/Components/Pages/Account/Favorites.razor
PropertyMap.Web/PropertyMap.Web/Components/Shared/FavoriteButton.razor
```

### Modificados — Frontend
```
PropertyMap.Web/PropertyMap.Web/Program.cs                              (+ 2 registros)
PropertyMap.Web/PropertyMap.Web/Components/Layout/Navbar.razor          (+ links perfil/favoritos)
PropertyMap.Web/PropertyMap.Web/Components/Pages/PropertyDetail.razor   (+ FavoriteButton)
PropertyMap.Web/PropertyMap.Web/wwwroot/css/app.css                     (+ estilos FavoriteButton y Profile)
```

---

## Task 1: DTOs de User

**Files:**
- Create: `PropertyMap.Core/DTOs/User/UserProfileResponse.cs`
- Create: `PropertyMap.Core/DTOs/User/UpdateProfileRequest.cs`
- Create: `PropertyMap.Core/DTOs/User/FavoriteStatusResponse.cs`

- [ ] **Step 1: Crear directorio y los 3 DTOs**

`PropertyMap.Core/DTOs/User/UserProfileResponse.cs`
```csharp
namespace PropertyMap.Core.DTOs.User;

public record UserProfileResponse(
    string Id,
    string Nombre,
    string Apellido,
    string Email,
    string? AvatarUrl);
```

`PropertyMap.Core/DTOs/User/UpdateProfileRequest.cs`
```csharp
namespace PropertyMap.Core.DTOs.User;

public record UpdateProfileRequest(string Nombre, string Apellido);
```

`PropertyMap.Core/DTOs/User/FavoriteStatusResponse.cs`
```csharp
namespace PropertyMap.Core.DTOs.User;

public record FavoriteStatusResponse(bool IsFavorited, int Count);
```

- [ ] **Step 2: Build Core**

```bash
cd C:/Agentes/PropertyMap/src
dotnet build PropertyMap.Core/PropertyMap.Core.csproj
```
Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```bash
cd C:/Agentes/PropertyMap/src
git add PropertyMap.Core/DTOs/User/
git commit -m "feat(core): add User DTOs for profile and favorite status"
```

---

## Task 2: IFavoriteRepository + FavoriteRepository

**Files:**
- Create: `PropertyMap.Core/Interfaces/IFavoriteRepository.cs`
- Create: `PropertyMap.Infrastructure/Repositories/FavoriteRepository.cs`

- [ ] **Step 1: Crear IFavoriteRepository**

`PropertyMap.Core/Interfaces/IFavoriteRepository.cs`
```csharp
using PropertyMap.Core.DTOs.Properties;
using PropertyMap.Core.Entities;

namespace PropertyMap.Core.Interfaces;

public interface IFavoriteRepository
{
    Task AddAsync(PropertyFavorite favorite);
    Task RemoveAsync(int listingId, string userId);
    Task<List<MyListingDto>> GetByUserAsync(string userId);
    Task<bool> IsFavoritedAsync(int listingId, string userId);
    Task<int> GetCountAsync(int listingId);
}
```

- [ ] **Step 2: Crear FavoriteRepository**

`PropertyMap.Infrastructure/Repositories/FavoriteRepository.cs`
```csharp
using Microsoft.EntityFrameworkCore;
using PropertyMap.Core.DTOs.Properties;
using PropertyMap.Core.Entities;
using PropertyMap.Core.Interfaces;
using PropertyMap.Infrastructure.Data;

namespace PropertyMap.Infrastructure.Repositories;

public class FavoriteRepository(AppDbContext ctx) : IFavoriteRepository
{
    public async Task AddAsync(PropertyFavorite favorite)
    {
        var exists = await IsFavoritedAsync(favorite.PropertyListingId, favorite.UserId);
        if (!exists)
        {
            ctx.PropertyFavorites.Add(favorite);
            await ctx.SaveChangesAsync();
        }
    }

    public async Task RemoveAsync(int listingId, string userId)
    {
        var fav = await ctx.PropertyFavorites
            .FirstOrDefaultAsync(f => f.PropertyListingId == listingId && f.UserId == userId);
        if (fav is not null)
        {
            ctx.PropertyFavorites.Remove(fav);
            await ctx.SaveChangesAsync();
        }
    }

    public async Task<List<MyListingDto>> GetByUserAsync(string userId) =>
        await ctx.PropertyFavorites
            .Where(f => f.UserId == userId)
            .OrderByDescending(f => f.FechaAgregado)
            .Select(f => new MyListingDto(
                f.PropertyListing.Id,
                f.PropertyListing.Titulo,
                f.PropertyListing.Location.DireccionTexto,
                f.PropertyListing.Location.Ciudad,
                f.PropertyListing.Precio,
                f.PropertyListing.Moneda,
                f.PropertyListing.TipoPropiedad.ToString(),
                f.PropertyListing.Operacion.ToString(),
                f.PropertyListing.Estado.ToString(),
                f.PropertyListing.Images.Where(i => i.EsPrincipal).Select(i => i.Url).FirstOrDefault(),
                f.PropertyListing.FechaPublicacion,
                f.PropertyListing.FechaActualizacion
            ))
            .ToListAsync();

    public async Task<bool> IsFavoritedAsync(int listingId, string userId) =>
        await ctx.PropertyFavorites
            .AnyAsync(f => f.PropertyListingId == listingId && f.UserId == userId);

    public async Task<int> GetCountAsync(int listingId) =>
        await ctx.PropertyFavorites.CountAsync(f => f.PropertyListingId == listingId);
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
git add PropertyMap.Core/Interfaces/IFavoriteRepository.cs \
        PropertyMap.Infrastructure/Repositories/FavoriteRepository.cs
git commit -m "feat(infra): add IFavoriteRepository and FavoriteRepository"
```

---

## Task 3: IViewTrackingService + ViewTrackingService

**Files:**
- Create: `PropertyMap.Core/Interfaces/IViewTrackingService.cs`
- Create: `PropertyMap.Infrastructure/Services/ViewTrackingService.cs`
- Create: `PropertyMap.Tests/Api/ViewTrackingTests.cs`

- [ ] **Step 1: Crear IViewTrackingService**

`PropertyMap.Core/Interfaces/IViewTrackingService.cs`
```csharp
namespace PropertyMap.Core.Interfaces;

public interface IViewTrackingService
{
    Task TrackViewAsync(int listingId, string? userId, string ipAddress, DateOnly date);
}
```

- [ ] **Step 2: Crear ViewTrackingService**

`PropertyMap.Infrastructure/Services/ViewTrackingService.cs`
```csharp
using Microsoft.EntityFrameworkCore;
using PropertyMap.Core.Entities;
using PropertyMap.Core.Interfaces;
using PropertyMap.Infrastructure.Data;

namespace PropertyMap.Infrastructure.Services;

public class ViewTrackingService(AppDbContext ctx) : IViewTrackingService
{
    public async Task TrackViewAsync(int listingId, string? userId, string ipAddress, DateOnly date)
    {
        var startOfDay = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var endOfDay = startOfDay.AddDays(1);

        bool alreadyViewed;
        if (userId != null)
        {
            alreadyViewed = await ctx.PropertyViews.AnyAsync(v =>
                v.PropertyListingId == listingId &&
                v.UserId == userId &&
                v.FechaVista >= startOfDay &&
                v.FechaVista < endOfDay);
        }
        else
        {
            alreadyViewed = await ctx.PropertyViews.AnyAsync(v =>
                v.PropertyListingId == listingId &&
                v.IpAddress == ipAddress &&
                v.FechaVista >= startOfDay &&
                v.FechaVista < endOfDay);
        }

        if (!alreadyViewed)
        {
            ctx.PropertyViews.Add(new PropertyView
            {
                PropertyListingId = listingId,
                UserId = userId,
                IpAddress = ipAddress,
                FechaVista = DateTime.UtcNow
            });
            await ctx.SaveChangesAsync();
        }
    }
}
```

- [ ] **Step 3: Escribir test que falla**

`PropertyMap.Tests/Api/ViewTrackingTests.cs`
```csharp
using System.Net;
using System.Net.Http.Json;
using PropertyMap.Core.DTOs.Properties;
using PropertyMap.Core.Enums;
using PropertyMap.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace PropertyMap.Tests.Api;

public class ViewTrackingTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public ViewTrackingTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static CreateListingRequest SampleListing() => new(
        Operacion: TipoOperacion.Venta,
        TipoPropiedad: TipoPropiedad.Casa,
        Titulo: "Casa tracking test",
        Descripcion: "Test",
        Precio: 100000,
        Moneda: "USD",
        DireccionTexto: "Calle 123",
        Ciudad: "Santa Rosa",
        Provincia: "La Pampa",
        Lat: -36.62,
        Lng: -64.29,
        Superficie: null,
        SuperficieCubierta: null,
        Ambientes: null,
        Dormitorios: null,
        Banos: null,
        Antiguedad: null,
        Cochera: false,
        Amenities: []
    );

    [Fact]
    public async Task GetListing_SameIpSameDay_CountsOnce()
    {
        // Arrange: create a published listing
        var (pubClient, _) = await TestAuthHelper.CreateAuthenticatedPublisherAsync(_factory);
        await TestAuthHelper.CreatePublisherProfileAsync(pubClient);
        var createResp = await pubClient.PostAsJsonAsync("/api/properties", SampleListing());
        var created = await createResp.Content.ReadFromJsonAsync<CreatedIdResponse>();

        // Publish via admin review
        var adminClient = _factory.CreateClient();
        var adminScope = _factory.Services.CreateScope();
        var adminUserManager = adminScope.ServiceProvider
            .GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<PropertyMap.Core.Entities.ApplicationUser>>();
        var adminUser = new PropertyMap.Core.Entities.ApplicationUser
        {
            UserName = "admin@test.com", Email = "admin@test.com",
            Nombre = "Admin", Apellido = "Test",
            EmailConfirmed = true,
            Estado = PropertyMap.Core.Enums.EstadoUsuario.Activo
        };
        await adminUserManager.CreateAsync(adminUser, "Admin123!");
        await adminUserManager.AddToRoleAsync(adminUser, "Admin");
        var adminLogin = await adminClient.PostAsJsonAsync("/api/auth/login",
            new PropertyMap.Core.DTOs.Auth.LoginRequest("admin@test.com", "Admin123!"));
        var adminAuth = await adminLogin.Content.ReadFromJsonAsync<PropertyMap.Core.DTOs.Auth.AuthResponse>();
        adminClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", adminAuth!.AccessToken);
        await adminClient.PatchAsJsonAsync($"/api/admin/listings/{created!.Id}/review",
            new { Aprobar = true, MotivoRechazo = (string?)null });

        // Act: anonymous client hits GET /api/listings/{id} twice
        var anonClient = _factory.CreateClient();
        await anonClient.GetAsync($"/api/listings/{created.Id}");
        await anonClient.GetAsync($"/api/listings/{created.Id}");

        // Assert: only 1 view recorded
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var viewCount = db.PropertyViews.Count(v => v.PropertyListingId == created.Id);
        Assert.Equal(1, viewCount);
    }

    private record CreatedIdResponse(int Id);
}
```

- [ ] **Step 4: Correr test para verificar que falla**

```bash
cd C:/Agentes/PropertyMap/src
dotnet test PropertyMap.Tests/PropertyMap.Tests.csproj --filter "ViewTrackingTests" -v n 2>&1 | tail -20
```
Expected: FAIL — `IViewTrackingService` no está registrado todavía.

- [ ] **Step 5: Build Infrastructure**

```bash
cd C:/Agentes/PropertyMap/src
dotnet build PropertyMap.Infrastructure/PropertyMap.Infrastructure.csproj
```
Expected: `Build succeeded.`

- [ ] **Step 6: Commit**

```bash
cd C:/Agentes/PropertyMap/src
git add PropertyMap.Core/Interfaces/IViewTrackingService.cs \
        PropertyMap.Infrastructure/Services/ViewTrackingService.cs \
        PropertyMap.Tests/Api/ViewTrackingTests.cs
git commit -m "feat(infra): add IViewTrackingService and ViewTrackingService with daily dedup"
```

---

## Task 4: IImageService.SaveAvatarAsync

**Files:**
- Modify: `PropertyMap.Core/Interfaces/IImageService.cs`
- Modify: `PropertyMap.Infrastructure/Services/ImageService.cs`

- [ ] **Step 1: Leer IImageService actual**

Leer `C:\Agentes\PropertyMap\src\PropertyMap.Core\Interfaces\IImageService.cs` completo antes de editar.

- [ ] **Step 2: Agregar SaveAvatarAsync a IImageService**

Agregar al final de la interfaz existente:
```csharp
Task<string> SaveAvatarAsync(string userId, IFormFile file);
```

El archivo completo debe quedar:
```csharp
using Microsoft.AspNetCore.Http;

namespace PropertyMap.Core.Interfaces;

public interface IImageService
{
    Task<List<string>> SaveImagesAsync(IFormFileCollection files, int listingId);
    Task DeleteImageAsync(string relativeUrl);
    Task DeleteAllImagesForListingAsync(int listingId);
    Task<string> SaveAvatarAsync(string userId, IFormFile file);
}
```

- [ ] **Step 3: Implementar SaveAvatarAsync en ImageService**

Leer `C:\Agentes\PropertyMap\src\PropertyMap.Infrastructure\Services\ImageService.cs` completo.

Agregar al final de la clase (antes del último `}`):
```csharp
public async Task<string> SaveAvatarAsync(string userId, IFormFile file)
{
    var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
    if (!AllowedExtensions.Contains(ext))
        throw new ArgumentException($"Extensión no permitida: {ext}");
    if (file.Length > MaxFileSizeBytes)
        throw new ArgumentException("El archivo supera el límite de 10 MB.");

    var dir = Path.Combine(_uploadsRoot, "avatars", userId);

    if (Directory.Exists(dir))
        foreach (var existing in Directory.GetFiles(dir))
            File.Delete(existing);

    Directory.CreateDirectory(dir);

    var fileName = $"avatar{ext}";
    var fullPath = Path.Combine(dir, fileName);

    await using var stream = File.Create(fullPath);
    await file.CopyToAsync(stream);

    return $"/uploads/avatars/{userId}/{fileName}";
}
```

- [ ] **Step 4: Build**

```bash
cd C:/Agentes/PropertyMap/src
dotnet build PropertyMap.Infrastructure/PropertyMap.Infrastructure.csproj
```
Expected: `Build succeeded.`

- [ ] **Step 5: Commit**

```bash
cd C:/Agentes/PropertyMap/src
git add PropertyMap.Core/Interfaces/IImageService.cs \
        PropertyMap.Infrastructure/Services/ImageService.cs
git commit -m "feat(infra): add SaveAvatarAsync to IImageService and ImageService"
```

---

## Task 5: UserController + tests + Program.cs (API)

**Files:**
- Create: `PropertyMap.Api/Controllers/UserController.cs`
- Create: `PropertyMap.Tests/Api/UserControllerTests.cs`
- Modify: `PropertyMap.Api/Program.cs`

- [ ] **Step 1: Escribir tests que fallan**

`PropertyMap.Tests/Api/UserControllerTests.cs`
```csharp
using System.Net;
using System.Net.Http.Json;
using PropertyMap.Core.DTOs.User;
using Xunit;

namespace PropertyMap.Tests.Api;

public class UserControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public UserControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetProfile_WithoutAuth_Returns401()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/api/user/profile");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task GetProfile_Authenticated_ReturnsProfileData()
    {
        var (client, _) = await TestAuthHelper.CreateAuthenticatedPublisherAsync(_factory);
        var resp = await client.GetAsync("/api/user/profile");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var profile = await resp.Content.ReadFromJsonAsync<UserProfileResponse>();
        Assert.NotNull(profile);
        Assert.Equal("Test", profile.Nombre);
        Assert.Equal("Publisher", profile.Apellido);
    }

    [Fact]
    public async Task UpdateProfile_ChangesNombreApellido()
    {
        var (client, _) = await TestAuthHelper.CreateAuthenticatedPublisherAsync(_factory);
        var resp = await client.PutAsJsonAsync("/api/user/profile",
            new UpdateProfileRequest("Nuevo", "Apellido"));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var getResp = await client.GetAsync("/api/user/profile");
        var profile = await getResp.Content.ReadFromJsonAsync<UserProfileResponse>();
        Assert.Equal("Nuevo", profile!.Nombre);
        Assert.Equal("Apellido", profile.Apellido);
    }

    [Fact]
    public async Task UploadAvatar_ValidImage_ReturnsAvatarUrl()
    {
        var (client, _) = await TestAuthHelper.CreateAuthenticatedPublisherAsync(_factory);

        using var form = new MultipartFormDataContent();
        var imgBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }; // JPEG magic bytes
        var fileContent = new ByteArrayContent(imgBytes);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
        form.Add(fileContent, "file", "test.jpg");

        var resp = await client.PostAsync("/api/user/avatar", form);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<AvatarUrlResponse>();
        Assert.NotNull(body?.AvatarUrl);
        Assert.Contains("/uploads/avatars/", body.AvatarUrl);
    }

    private record AvatarUrlResponse(string AvatarUrl);
}
```

- [ ] **Step 2: Correr tests para verificar que fallan**

```bash
cd C:/Agentes/PropertyMap/src
dotnet test PropertyMap.Tests/PropertyMap.Tests.csproj --filter "UserControllerTests" -v n 2>&1 | tail -10
```
Expected: FAIL — `No se encontró la ruta /api/user/profile`.

- [ ] **Step 3: Crear UserController**

`PropertyMap.Api/Controllers/UserController.cs`
```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PropertyMap.Core.DTOs.User;
using PropertyMap.Core.Entities;
using PropertyMap.Core.Interfaces;

namespace PropertyMap.Api.Controllers;

[ApiController]
[Route("api/user")]
[Authorize]
public class UserController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IImageService _images;

    public UserController(UserManager<ApplicationUser> userManager, IImageService images)
    {
        _userManager = userManager;
        _images = images;
    }

    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return NotFound();

        return Ok(new UserProfileResponse(user.Id, user.Nombre, user.Apellido, user.Email!, user.AvatarUrl));
    }

    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile(UpdateProfileRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Nombre) || string.IsNullOrWhiteSpace(request.Apellido))
            return BadRequest(new { message = "Nombre y apellido son requeridos." });

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return NotFound();

        user.Nombre = request.Nombre.Trim();
        user.Apellido = request.Apellido.Trim();
        await _userManager.UpdateAsync(user);

        return Ok(new UserProfileResponse(user.Id, user.Nombre, user.Apellido, user.Email!, user.AvatarUrl));
    }

    [HttpPost("avatar")]
    public async Task<IActionResult> UploadAvatar(IFormFile file)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "Archivo requerido." });

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return NotFound();

        try
        {
            var avatarUrl = await _images.SaveAvatarAsync(userId, file);
            user.AvatarUrl = avatarUrl;
            await _userManager.UpdateAsync(user);
            return Ok(new { avatarUrl });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
```

- [ ] **Step 4: Registrar en Program.cs (API)**

Leer `C:\Agentes\PropertyMap\src\PropertyMap.Api\Program.cs` completo, luego agregar las líneas siguientes después del bloque de servicios existente (después de `builder.Services.AddScoped<IImageService, ImageService>();`):

```csharp
builder.Services.AddScoped<IFavoriteRepository, FavoriteRepository>();
builder.Services.AddScoped<IViewTrackingService, ViewTrackingService>();
```

Agregar también los usings necesarios al tope del archivo si no están:
```csharp
using PropertyMap.Infrastructure.Repositories;
using PropertyMap.Infrastructure.Services;
```

El archivo `Program.cs` actual ya tiene esos usings. Solo agregar las dos líneas de registro.

- [ ] **Step 5: Build API**

```bash
cd C:/Agentes/PropertyMap/src
dotnet build PropertyMap.Api/PropertyMap.Api.csproj
```
Expected: `Build succeeded.`

- [ ] **Step 6: Correr tests**

```bash
cd C:/Agentes/PropertyMap/src
dotnet test PropertyMap.Tests/PropertyMap.Tests.csproj --filter "UserControllerTests" -v n 2>&1 | tail -15
```
Expected: 4 tests PASS.

- [ ] **Step 7: Commit**

```bash
cd C:/Agentes/PropertyMap/src
git add PropertyMap.Api/Controllers/UserController.cs \
        PropertyMap.Tests/Api/UserControllerTests.cs \
        PropertyMap.Api/Program.cs
git commit -m "feat(api): add UserController with profile GET/PUT and avatar upload"
```

---

## Task 6: FavoritesController + tests + Program.cs (API)

**Files:**
- Create: `PropertyMap.Api/Controllers/FavoritesController.cs`
- Create: `PropertyMap.Tests/Api/FavoritesControllerTests.cs`

(IFavoriteRepository ya fue registrado en Task 5)

- [ ] **Step 1: Escribir tests que fallan**

`PropertyMap.Tests/Api/FavoritesControllerTests.cs`
```csharp
using System.Net;
using System.Net.Http.Json;
using PropertyMap.Core.DTOs.Properties;
using PropertyMap.Core.DTOs.User;
using PropertyMap.Core.Enums;
using Xunit;

namespace PropertyMap.Tests.Api;

public class FavoritesControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public FavoritesControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static CreateListingRequest SampleListing() => new(
        Operacion: TipoOperacion.Venta,
        TipoPropiedad: TipoPropiedad.Departamento,
        Titulo: "Depto favorito test",
        Descripcion: "Test",
        Precio: 80000,
        Moneda: "USD",
        DireccionTexto: "Av. Test 456",
        Ciudad: "Buenos Aires",
        Provincia: "Buenos Aires",
        Lat: -34.60,
        Lng: -58.38,
        Superficie: null,
        SuperficieCubierta: null,
        Ambientes: null,
        Dormitorios: null,
        Banos: null,
        Antiguedad: null,
        Cochera: false,
        Amenities: []
    );

    private async Task<int> CreatePublishedListingAsync()
    {
        var (pubClient, _) = await TestAuthHelper.CreateAuthenticatedPublisherAsync(_factory);
        await TestAuthHelper.CreatePublisherProfileAsync(pubClient);
        var createResp = await pubClient.PostAsJsonAsync("/api/properties", SampleListing());
        var created = await createResp.Content.ReadFromJsonAsync<CreatedIdDto>();

        // approve via admin
        var adminScope = _factory.Services.CreateScope();
        var adminMgr = adminScope.ServiceProvider
            .GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<PropertyMap.Core.Entities.ApplicationUser>>();
        var adminEmail = $"admin_{Guid.NewGuid()}@test.com";
        var adminUser = new PropertyMap.Core.Entities.ApplicationUser
        {
            UserName = adminEmail, Email = adminEmail,
            Nombre = "Admin", Apellido = "Test",
            EmailConfirmed = true,
            Estado = PropertyMap.Core.Enums.EstadoUsuario.Activo
        };
        await adminMgr.CreateAsync(adminUser, "Admin123!");
        await adminMgr.AddToRoleAsync(adminUser, "Admin");
        var adminClient = _factory.CreateClient();
        var loginResp = await adminClient.PostAsJsonAsync("/api/auth/login",
            new PropertyMap.Core.DTOs.Auth.LoginRequest(adminEmail, "Admin123!"));
        var adminAuth = await loginResp.Content.ReadFromJsonAsync<PropertyMap.Core.DTOs.Auth.AuthResponse>();
        adminClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", adminAuth!.AccessToken);
        await adminClient.PatchAsJsonAsync($"/api/admin/listings/{created!.Id}/review",
            new { Aprobar = true, MotivoRechazo = (string?)null });

        return created.Id;
    }

    private record CreatedIdDto(int Id);

    [Fact]
    public async Task AddFavorite_WithoutAuth_Returns401()
    {
        var client = _factory.CreateClient();
        var resp = await client.PostAsync("/api/favorites/1", null);
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task AddAndRemoveFavorite_WorksCorrectly()
    {
        var listingId = await CreatePublishedListingAsync();
        var (client, _) = await TestAuthHelper.CreateAuthenticatedPublisherAsync(_factory);

        // Add
        var addResp = await client.PostAsync($"/api/favorites/{listingId}", null);
        Assert.Equal(HttpStatusCode.OK, addResp.StatusCode);

        // List
        var listResp = await client.GetAsync("/api/favorites");
        var favs = await listResp.Content.ReadFromJsonAsync<List<MyListingDto>>();
        Assert.Contains(favs!, f => f.Id == listingId);

        // Remove
        var removeResp = await client.DeleteAsync($"/api/favorites/{listingId}");
        Assert.Equal(HttpStatusCode.OK, removeResp.StatusCode);

        // List again — empty
        var listResp2 = await client.GetAsync("/api/favorites");
        var favs2 = await listResp2.Content.ReadFromJsonAsync<List<MyListingDto>>();
        Assert.DoesNotContain(favs2!, f => f.Id == listingId);
    }

    [Fact]
    public async Task AddFavorite_Idempotent_DoesNotDuplicate()
    {
        var listingId = await CreatePublishedListingAsync();
        var (client, _) = await TestAuthHelper.CreateAuthenticatedPublisherAsync(_factory);

        await client.PostAsync($"/api/favorites/{listingId}", null);
        await client.PostAsync($"/api/favorites/{listingId}", null);

        var listResp = await client.GetAsync("/api/favorites");
        var favs = await listResp.Content.ReadFromJsonAsync<List<MyListingDto>>();
        Assert.Equal(1, favs!.Count(f => f.Id == listingId));
    }

    [Fact]
    public async Task GetStatus_AnonymousUser_ReturnsFalseWithCount()
    {
        var listingId = await CreatePublishedListingAsync();
        var (authClient, _) = await TestAuthHelper.CreateAuthenticatedPublisherAsync(_factory);
        await authClient.PostAsync($"/api/favorites/{listingId}", null);

        var anonClient = _factory.CreateClient();
        var resp = await anonClient.GetAsync($"/api/favorites/{listingId}/status");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var status = await resp.Content.ReadFromJsonAsync<FavoriteStatusResponse>();
        Assert.False(status!.IsFavorited);
        Assert.Equal(1, status.Count);
    }
}
```

- [ ] **Step 2: Correr tests para verificar que fallan**

```bash
cd C:/Agentes/PropertyMap/src
dotnet test PropertyMap.Tests/PropertyMap.Tests.csproj --filter "FavoritesControllerTests" -v n 2>&1 | tail -10
```
Expected: FAIL — ruta no encontrada.

- [ ] **Step 3: Crear FavoritesController**

`PropertyMap.Api/Controllers/FavoritesController.cs`
```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PropertyMap.Core.DTOs.User;
using PropertyMap.Core.Entities;
using PropertyMap.Core.Interfaces;

namespace PropertyMap.Api.Controllers;

[ApiController]
[Route("api/favorites")]
[Authorize]
public class FavoritesController : ControllerBase
{
    private readonly IFavoriteRepository _favorites;
    private readonly UserManager<ApplicationUser> _userManager;

    public FavoritesController(IFavoriteRepository favorites, UserManager<ApplicationUser> userManager)
    {
        _favorites = favorites;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> GetMyFavorites()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var listings = await _favorites.GetByUserAsync(userId);
        return Ok(listings);
    }

    [HttpPost("{listingId:int}")]
    public async Task<IActionResult> AddFavorite(int listingId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        await _favorites.AddAsync(new PropertyFavorite
        {
            PropertyListingId = listingId,
            UserId = userId
        });
        return Ok();
    }

    [HttpDelete("{listingId:int}")]
    public async Task<IActionResult> RemoveFavorite(int listingId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        await _favorites.RemoveAsync(listingId, userId);
        return Ok();
    }

    [HttpGet("{listingId:int}/status")]
    [AllowAnonymous]
    public async Task<IActionResult> GetStatus(int listingId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var isFavorited = userId != null && await _favorites.IsFavoritedAsync(listingId, userId);
        var count = await _favorites.GetCountAsync(listingId);
        return Ok(new FavoriteStatusResponse(isFavorited, count));
    }
}
```

- [ ] **Step 4: Build API**

```bash
cd C:/Agentes/PropertyMap/src
dotnet build PropertyMap.Api/PropertyMap.Api.csproj
```
Expected: `Build succeeded.`

- [ ] **Step 5: Correr tests**

```bash
cd C:/Agentes/PropertyMap/src
dotnet test PropertyMap.Tests/PropertyMap.Tests.csproj --filter "FavoritesControllerTests" -v n 2>&1 | tail -15
```
Expected: 4 tests PASS.

- [ ] **Step 6: Commit**

```bash
cd C:/Agentes/PropertyMap/src
git add PropertyMap.Api/Controllers/FavoritesController.cs \
        PropertyMap.Tests/Api/FavoritesControllerTests.cs
git commit -m "feat(api): add FavoritesController with add/remove/list/status endpoints"
```

---

## Task 7: ListingsController — view tracking + Program.cs (API)

**Files:**
- Modify: `PropertyMap.Api/Controllers/ListingsController.cs`
- Modify: `PropertyMap.Api/Program.cs`

- [ ] **Step 1: Leer ListingsController actual**

Leer `C:\Agentes\PropertyMap\src\PropertyMap.Api\Controllers\ListingsController.cs` completo.

- [ ] **Step 2: Reemplazar ListingsController**

```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using PropertyMap.Core.Interfaces;

namespace PropertyMap.Api.Controllers;

[ApiController]
[Route("api/listings")]
public class ListingsController : ControllerBase
{
    private readonly IListingRepository _listings;
    private readonly IViewTrackingService _viewTracking;

    public ListingsController(IListingRepository listings, IViewTrackingService viewTracking)
    {
        _listings = listings;
        _viewTracking = viewTracking;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var listings = await _listings.GetActiveListingsAsync();
        return Ok(listings);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var listing = await _listings.GetByIdAsDetailAsync(id);
        if (listing == null) return NotFound();

        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            await _viewTracking.TrackViewAsync(id, userId, ip, DateOnly.FromDateTime(DateTime.UtcNow));
        }
        catch { }

        return Ok(listing);
    }

    [HttpGet("map")]
    public async Task<IActionResult> GetForMap()
    {
        var listings = await _listings.GetActiveListingsForMapAsync();
        return Ok(listings);
    }
}
```

- [ ] **Step 3: Verificar que IViewTrackingService ya está registrado en Program.cs**

Leer `C:\Agentes\PropertyMap\src\PropertyMap.Api\Program.cs` y verificar que existe la línea:
```csharp
builder.Services.AddScoped<IViewTrackingService, ViewTrackingService>();
```
(fue agregada en Task 5). Si no existe, agregarla.

- [ ] **Step 4: Build API**

```bash
cd C:/Agentes/PropertyMap/src
dotnet build PropertyMap.Api/PropertyMap.Api.csproj
```
Expected: `Build succeeded.`

- [ ] **Step 5: Correr ViewTrackingTests**

```bash
cd C:/Agentes/PropertyMap/src
dotnet test PropertyMap.Tests/PropertyMap.Tests.csproj --filter "ViewTrackingTests" -v n 2>&1 | tail -15
```
Expected: 1 test PASS.

- [ ] **Step 6: Correr todos los tests**

```bash
cd C:/Agentes/PropertyMap/src
dotnet test PropertyMap.Tests/PropertyMap.Tests.csproj -v n 2>&1 | tail -10
```
Expected: todos los tests pasan.

- [ ] **Step 7: Commit**

```bash
cd C:/Agentes/PropertyMap/src
git add PropertyMap.Api/Controllers/ListingsController.cs
git commit -m "feat(api): inject IViewTrackingService in ListingsController.GetById"
```

---

## Task 8: Blazor services + Program.cs (Web)

**Files:**
- Create: `PropertyMap.Web/PropertyMap.Web/Services/IUserApiService.cs`
- Create: `PropertyMap.Web/PropertyMap.Web/Services/UserApiService.cs`
- Create: `PropertyMap.Web/PropertyMap.Web/Services/IFavoritesApiService.cs`
- Create: `PropertyMap.Web/PropertyMap.Web/Services/FavoritesApiService.cs`
- Modify: `PropertyMap.Web/PropertyMap.Web/Program.cs`

- [ ] **Step 1: Crear IUserApiService**

`PropertyMap.Web/PropertyMap.Web/Services/IUserApiService.cs`
```csharp
using Microsoft.AspNetCore.Components.Forms;
using PropertyMap.Core.DTOs.User;

namespace PropertyMap.Web.Services;

public interface IUserApiService
{
    Task<UserProfileResponse?> GetProfileAsync();
    Task<(bool Success, string? Error)> UpdateProfileAsync(string nombre, string apellido);
    Task<(bool Success, string? AvatarUrl, string? Error)> UploadAvatarAsync(IBrowserFile file);
}
```

- [ ] **Step 2: Crear UserApiService**

`PropertyMap.Web/PropertyMap.Web/Services/UserApiService.cs`
```csharp
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Forms;
using PropertyMap.Core.DTOs.User;

namespace PropertyMap.Web.Services;

public class UserApiService : IUserApiService
{
    private readonly HttpClient _http;
    private readonly MemoryTokenStore _tokenStore;

    public UserApiService(IHttpClientFactory httpFactory, MemoryTokenStore tokenStore)
    {
        _http = httpFactory.CreateClient("api");
        _tokenStore = tokenStore;
    }

    private void SetAuth()
    {
        _http.DefaultRequestHeaders.Authorization = _tokenStore.AccessToken is null
            ? null
            : new AuthenticationHeaderValue("Bearer", _tokenStore.AccessToken);
    }

    public async Task<UserProfileResponse?> GetProfileAsync()
    {
        try
        {
            SetAuth();
            return await _http.GetFromJsonAsync<UserProfileResponse>("api/user/profile");
        }
        catch { return null; }
    }

    public async Task<(bool Success, string? Error)> UpdateProfileAsync(string nombre, string apellido)
    {
        try
        {
            SetAuth();
            var resp = await _http.PutAsJsonAsync("api/user/profile",
                new UpdateProfileRequest(nombre, apellido));
            if (!resp.IsSuccessStatusCode)
            {
                var err = await resp.Content.ReadFromJsonAsync<ErrorDto>();
                return (false, err?.Message ?? "Error al guardar.");
            }
            return (true, null);
        }
        catch (Exception ex) { return (false, $"No se pudo conectar: {ex.Message}"); }
    }

    public async Task<(bool Success, string? AvatarUrl, string? Error)> UploadAvatarAsync(IBrowserFile file)
    {
        try
        {
            SetAuth();
            using var ms = new MemoryStream();
            await file.OpenReadStream(5 * 1024 * 1024).CopyToAsync(ms);
            var bytes = ms.ToArray();

            using var form = new MultipartFormDataContent();
            var content = new ByteArrayContent(bytes);
            content.Headers.ContentType = new MediaTypeHeaderValue(
                file.ContentType is { Length: > 0 } ct ? ct : "image/jpeg");
            form.Add(content, "file", file.Name);

            var resp = await _http.PostAsync("api/user/avatar", form);
            if (!resp.IsSuccessStatusCode) return (false, null, "Error al subir el avatar.");

            var body = await resp.Content.ReadFromJsonAsync<AvatarUrlDto>();
            return (true, body?.AvatarUrl, null);
        }
        catch (Exception ex) { return (false, null, $"No se pudo subir: {ex.Message}"); }
    }

    private record ErrorDto(string Message);
    private record AvatarUrlDto(string AvatarUrl);
}
```

- [ ] **Step 3: Crear IFavoritesApiService**

`PropertyMap.Web/PropertyMap.Web/Services/IFavoritesApiService.cs`
```csharp
using PropertyMap.Core.DTOs.Properties;

namespace PropertyMap.Web.Services;

public interface IFavoritesApiService
{
    Task<List<MyListingDto>> GetFavoritesAsync();
    Task<bool> ToggleFavoriteAsync(int listingId, bool currentlyFavorited);
    Task<(bool IsFavorited, int Count)> GetStatusAsync(int listingId);
}
```

- [ ] **Step 4: Crear FavoritesApiService**

`PropertyMap.Web/PropertyMap.Web/Services/FavoritesApiService.cs`
```csharp
using System.Net.Http.Headers;
using System.Net.Http.Json;
using PropertyMap.Core.DTOs.Properties;
using PropertyMap.Core.DTOs.User;

namespace PropertyMap.Web.Services;

public class FavoritesApiService : IFavoritesApiService
{
    private readonly HttpClient _http;
    private readonly MemoryTokenStore _tokenStore;

    public FavoritesApiService(IHttpClientFactory httpFactory, MemoryTokenStore tokenStore)
    {
        _http = httpFactory.CreateClient("api");
        _tokenStore = tokenStore;
    }

    private void SetAuth()
    {
        _http.DefaultRequestHeaders.Authorization = _tokenStore.AccessToken is null
            ? null
            : new AuthenticationHeaderValue("Bearer", _tokenStore.AccessToken);
    }

    public async Task<List<MyListingDto>> GetFavoritesAsync()
    {
        try
        {
            SetAuth();
            var result = await _http.GetFromJsonAsync<List<MyListingDto>>("api/favorites");
            return result ?? [];
        }
        catch { return []; }
    }

    public async Task<bool> ToggleFavoriteAsync(int listingId, bool currentlyFavorited)
    {
        try
        {
            SetAuth();
            if (currentlyFavorited)
            {
                var resp = await _http.DeleteAsync($"api/favorites/{listingId}");
                return !resp.IsSuccessStatusCode ? currentlyFavorited : false;
            }
            else
            {
                var resp = await _http.PostAsync($"api/favorites/{listingId}", null);
                return resp.IsSuccessStatusCode;
            }
        }
        catch { return currentlyFavorited; }
    }

    public async Task<(bool IsFavorited, int Count)> GetStatusAsync(int listingId)
    {
        try
        {
            SetAuth();
            var resp = await _http.GetFromJsonAsync<FavoriteStatusResponse>(
                $"api/favorites/{listingId}/status");
            return (resp?.IsFavorited ?? false, resp?.Count ?? 0);
        }
        catch { return (false, 0); }
    }
}
```

- [ ] **Step 5: Registrar en Program.cs (Web)**

Leer `C:\Agentes\PropertyMap\src\PropertyMap.Web\PropertyMap.Web\Program.cs` completo. Agregar después de la línea `builder.Services.AddScoped<IPropertyApiService, PropertyApiService>();`:

```csharp
builder.Services.AddScoped<IUserApiService, UserApiService>();
builder.Services.AddScoped<IFavoritesApiService, FavoritesApiService>();
```

- [ ] **Step 6: Build Web**

```bash
cd C:/Agentes/PropertyMap/src
dotnet build PropertyMap.Web/PropertyMap.Web/PropertyMap.Web.csproj 2>&1 | tail -10
```
Expected: `Build succeeded.`

- [ ] **Step 7: Commit**

```bash
cd C:/Agentes/PropertyMap/src
git add PropertyMap.Web/PropertyMap.Web/Services/IUserApiService.cs \
        PropertyMap.Web/PropertyMap.Web/Services/UserApiService.cs \
        PropertyMap.Web/PropertyMap.Web/Services/IFavoritesApiService.cs \
        PropertyMap.Web/PropertyMap.Web/Services/FavoritesApiService.cs \
        PropertyMap.Web/PropertyMap.Web/Program.cs
git commit -m "feat(web): add IUserApiService, UserApiService, IFavoritesApiService and FavoritesApiService"
```

---

## Task 9: Profile.razor

**Files:**
- Create: `PropertyMap.Web/PropertyMap.Web/Components/Pages/Account/Profile.razor`
- Modify: `PropertyMap.Web/PropertyMap.Web/wwwroot/css/app.css` (append profile styles)

- [ ] **Step 1: Crear Profile.razor**

`PropertyMap.Web/PropertyMap.Web/Components/Pages/Account/Profile.razor`
```razor
@page "/account/profile"
@rendermode InteractiveServer
@inject IUserApiService UserApi
@inject IAuthService AuthService
@inject NavigationManager Nav

<PageTitle>Mi perfil — PropertyMap</PageTitle>

<AuthorizeView>
    <Authorized>
        <div class="auth-page">
            <div class="auth-card" style="max-width:480px">
                <a href="/" class="auth-logo">PropertyMap</a>
                <h1 class="auth-title">Mi perfil</h1>

                @if (saveSuccess)
                {
                    <div class="auth-success">Perfil actualizado correctamente.</div>
                }
                @if (error is not null)
                {
                    <div class="auth-error">@error</div>
                }

                @* Avatar *@
                <div class="profile-avatar-section">
                    <div class="profile-avatar">
                        @if (avatarPreview is not null)
                        {
                            <img src="@avatarPreview" alt="Avatar" class="profile-avatar-img" />
                        }
                        else
                        {
                            <div class="profile-avatar-placeholder">
                                @(nombre.Length > 0 ? nombre[0].ToString().ToUpper() : "?")
                            </div>
                        }
                    </div>
                    <label class="btn-ghost profile-avatar-btn" style="cursor:pointer">
                        Cambiar foto
                        <InputFile OnChange="OnAvatarSelected" accept="image/jpeg,image/png,image/webp"
                                   style="display:none" />
                    </label>
                    @if (avatarUploading)
                    {
                        <span style="font-size:0.8rem;color:var(--color-text-muted)">Subiendo...</span>
                    }
                </div>

                @* Nombre y Apellido *@
                <div class="field-row">
                    <div class="field">
                        <label class="field-label" for="nombre">Nombre</label>
                        <input id="nombre" type="text" class="field-input"
                               @bind="nombre" placeholder="Tu nombre" />
                    </div>
                    <div class="field">
                        <label class="field-label" for="apellido">Apellido</label>
                        <input id="apellido" type="text" class="field-input"
                               @bind="apellido" placeholder="Tu apellido" />
                    </div>
                </div>

                <div class="field">
                    <label class="field-label">Email</label>
                    <input type="email" class="field-input" value="@email" disabled />
                </div>

                <button class="btn-primary auth-btn" @onclick="Save" disabled="@saving">
                    @(saving ? "Guardando..." : "Guardar cambios")
                </button>
            </div>
        </div>
    </Authorized>
    <NotAuthorized>
        <div class="auth-page">
            <div class="auth-card">
                <a href="/" class="auth-logo">PropertyMap</a>
                <p style="text-align:center">Necesitás iniciar sesión para ver tu perfil.</p>
                <a href="/Account/Login?returnUrl=/account/profile" class="btn-primary auth-btn" style="text-align:center">
                    Iniciar sesión
                </a>
            </div>
        </div>
    </NotAuthorized>
</AuthorizeView>

@code {
    private string nombre = "";
    private string apellido = "";
    private string email = "";
    private string? avatarPreview;
    private string? error;
    private bool saving;
    private bool saveSuccess;
    private bool avatarUploading;
    private bool sessionRestored;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && !sessionRestored)
        {
            sessionRestored = true;
            await AuthService.TryRestoreSessionAsync();
            await LoadProfile();
            StateHasChanged();
        }
    }

    private async Task LoadProfile()
    {
        var profile = await UserApi.GetProfileAsync();
        if (profile is null) return;
        nombre = profile.Nombre;
        apellido = profile.Apellido;
        email = profile.Email;
        avatarPreview = profile.AvatarUrl;
    }

    private async Task Save()
    {
        if (string.IsNullOrWhiteSpace(nombre) || string.IsNullOrWhiteSpace(apellido))
        {
            error = "Nombre y apellido son requeridos.";
            return;
        }
        saving = true;
        error = null;
        saveSuccess = false;
        var (ok, err) = await UserApi.UpdateProfileAsync(nombre, apellido);
        saving = false;
        if (ok) saveSuccess = true;
        else error = err;
    }

    private async Task OnAvatarSelected(InputFileChangeEventArgs e)
    {
        var file = e.File;
        if (file.Size > 5 * 1024 * 1024)
        {
            error = "El archivo supera 5 MB.";
            return;
        }
        avatarUploading = true;
        error = null;
        var (ok, url, err) = await UserApi.UploadAvatarAsync(file);
        avatarUploading = false;
        if (ok) avatarPreview = url;
        else error = err;
    }
}
```

- [ ] **Step 2: Agregar estilos de perfil a app.css**

Leer `C:\Agentes\PropertyMap\src\PropertyMap.Web\PropertyMap.Web\wwwroot\css\app.css` y agregar al final:

```css
/* ── Profile page ────────────────────────────────────────────────── */
.profile-avatar-section {
    display: flex;
    align-items: center;
    gap: var(--space-4, 1rem);
}

.profile-avatar {
    width: 72px;
    height: 72px;
    border-radius: 50%;
    overflow: hidden;
    flex-shrink: 0;
    background: var(--color-surface-2, oklch(97% 0.005 250));
    border: 2px solid var(--color-border, oklch(90% 0.01 250));
}

.profile-avatar-img {
    width: 100%;
    height: 100%;
    object-fit: cover;
}

.profile-avatar-placeholder {
    width: 100%;
    height: 100%;
    display: flex;
    align-items: center;
    justify-content: center;
    font-size: 1.75rem;
    font-weight: 700;
    color: var(--color-brand, oklch(55% 0.18 250));
}

.profile-avatar-btn {
    font-size: 0.875rem;
}

.field-row {
    display: grid;
    grid-template-columns: 1fr 1fr;
    gap: var(--space-3, 0.75rem);
}
```

- [ ] **Step 3: Build Web**

```bash
cd C:/Agentes/PropertyMap/src
dotnet build PropertyMap.Web/PropertyMap.Web/PropertyMap.Web.csproj 2>&1 | tail -10
```
Expected: `Build succeeded.`

- [ ] **Step 4: Commit**

```bash
cd C:/Agentes/PropertyMap/src
git add PropertyMap.Web/PropertyMap.Web/Components/Pages/Account/Profile.razor \
        PropertyMap.Web/PropertyMap.Web/wwwroot/css/app.css
git commit -m "feat(web): add Profile page with avatar upload and profile editing"
```

---

## Task 10: Favorites.razor

**Files:**
- Create: `PropertyMap.Web/PropertyMap.Web/Components/Pages/Account/Favorites.razor`

- [ ] **Step 1: Crear Favorites.razor**

`PropertyMap.Web/PropertyMap.Web/Components/Pages/Account/Favorites.razor`
```razor
@page "/account/favorites"
@rendermode InteractiveServer
@inject IFavoritesApiService FavoritesApi
@inject IAuthService AuthService
@inject NavigationManager Nav

<PageTitle>Mis favoritos — PropertyMap</PageTitle>

<AuthorizeView>
    <Authorized>
        <div class="app-shell" style="display:flex;flex-direction:column">
            <nav class="pm-navbar" role="navigation">
                <a href="/" class="pm-navbar__logo">PropertyMap</a>
                <span style="font-weight:600">Mis favoritos</span>
                <div class="pm-navbar__actions">
                    <a href="/account/profile" class="btn-ghost">Mi perfil</a>
                    <a href="/" class="btn-ghost">Volver al mapa</a>
                </div>
            </nav>

            <div style="padding:var(--space-6,1.5rem);max-width:900px;margin:0 auto;width:100%">
                <h1 style="font-size:1.5rem;font-weight:700;margin-bottom:var(--space-4)">
                    Propiedades guardadas
                </h1>

                @if (loading)
                {
                    <p style="color:var(--color-text-muted)">Cargando...</p>
                }
                else if (listings.Count == 0)
                {
                    <div style="text-align:center;padding:var(--space-10) 0;color:var(--color-text-muted)">
                        <p style="font-size:1.125rem">Todavía no guardaste ninguna propiedad.</p>
                        <a href="/" class="btn-primary" style="margin-top:var(--space-4);display:inline-block">
                            Explorar propiedades
                        </a>
                    </div>
                }
                else
                {
                    <div style="display:flex;flex-direction:column;gap:var(--space-3)">
                        @foreach (var l in listings)
                        {
                            var listingId = l.Id;
                            <div class="dashboard-listing-card">
                                @if (l.FotoPrincipalUrl is not null)
                                {
                                    <img src="@l.FotoPrincipalUrl" alt="@l.Titulo" class="dashboard-listing-img" />
                                }
                                else
                                {
                                    <div class="dashboard-listing-img dashboard-listing-img--placeholder" aria-hidden="true">
                                        <svg width="24" height="24" viewBox="0 0 24 24" fill="currentColor">
                                            <path d="M10 20v-6h4v6h5v-8h3L12 3 2 12h3v8z"/>
                                        </svg>
                                    </div>
                                }
                                <div class="dashboard-listing-body">
                                    <a href="/property/@l.Id" class="dashboard-listing-title">@l.Titulo</a>
                                    <div class="dashboard-listing-meta">
                                        @l.Ciudad · @l.TipoPropiedad · @l.Operacion
                                    </div>
                                    <div class="dashboard-listing-price">
                                        @l.Moneda @l.Precio.ToString("N0")
                                    </div>
                                </div>
                                <button class="btn-ghost" style="color:var(--color-text-muted);font-size:0.8rem"
                                        @onclick="() => RemoveFavorite(listingId)">
                                    Quitar
                                </button>
                            </div>
                        }
                    </div>
                }
            </div>
        </div>
    </Authorized>
    <NotAuthorized>
        <div class="auth-page">
            <div class="auth-card">
                <a href="/" class="auth-logo">PropertyMap</a>
                <p style="text-align:center">Necesitás iniciar sesión para ver tus favoritos.</p>
                <a href="/Account/Login?returnUrl=/account/favorites" class="btn-primary auth-btn" style="text-align:center">
                    Iniciar sesión
                </a>
            </div>
        </div>
    </NotAuthorized>
</AuthorizeView>

@code {
    private List<PropertyMap.Core.DTOs.Properties.MyListingDto> listings = [];
    private bool loading = true;
    private bool sessionRestored;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && !sessionRestored)
        {
            sessionRestored = true;
            await AuthService.TryRestoreSessionAsync();
            await LoadFavorites();
            StateHasChanged();
        }
    }

    private async Task LoadFavorites()
    {
        loading = true;
        try { listings = await FavoritesApi.GetFavoritesAsync(); }
        catch { listings = []; }
        finally { loading = false; }
    }

    private async Task RemoveFavorite(int listingId)
    {
        await FavoritesApi.ToggleFavoriteAsync(listingId, currentlyFavorited: true);
        listings.RemoveAll(l => l.Id == listingId);
    }
}
```

- [ ] **Step 2: Build Web**

```bash
cd C:/Agentes/PropertyMap/src
dotnet build PropertyMap.Web/PropertyMap.Web/PropertyMap.Web.csproj 2>&1 | tail -10
```
Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```bash
cd C:/Agentes/PropertyMap/src
git add PropertyMap.Web/PropertyMap.Web/Components/Pages/Account/Favorites.razor
git commit -m "feat(web): add Favorites page with listing cards and remove button"
```

---

## Task 11: FavoriteButton.razor + PropertyDetail + CSS

**Files:**
- Create: `PropertyMap.Web/PropertyMap.Web/Components/Shared/FavoriteButton.razor`
- Modify: `PropertyMap.Web/PropertyMap.Web/Components/Pages/PropertyDetail.razor`
- Modify: `PropertyMap.Web/PropertyMap.Web/wwwroot/css/app.css` (append)

- [ ] **Step 1: Crear directorio Shared si no existe**

```bash
ls "C:/Agentes/PropertyMap/src/PropertyMap.Web/PropertyMap.Web/Components/Shared"
```
Si no existe: `mkdir "C:/Agentes/PropertyMap/src/PropertyMap.Web/PropertyMap.Web/Components/Shared"`

- [ ] **Step 2: Crear FavoriteButton.razor**

`PropertyMap.Web/PropertyMap.Web/Components/Shared/FavoriteButton.razor`
```razor
@inject IFavoritesApiService FavoritesApi
@inject NavigationManager Nav

<button class="favorite-btn @(isFavorited ? "favorite-btn--active" : "")"
        @onclick="Toggle"
        title="@(isFavorited ? "Quitar de favoritos" : "Guardar en favoritos")"
        aria-label="@(isFavorited ? "Quitar de favoritos" : "Guardar en favoritos")">
    <svg width="20" height="20" viewBox="0 0 24 24"
         fill="@(isFavorited ? "currentColor" : "none")"
         stroke="currentColor" stroke-width="2">
        <path d="M20.84 4.61a5.5 5.5 0 0 0-7.78 0L12 5.67l-1.06-1.06a5.5 5.5 0 0 0-7.78 7.78l1.06 1.06L12 21.23l7.78-7.78 1.06-1.06a5.5 5.5 0 0 0 0-7.78z"/>
    </svg>
    @if (count > 0)
    {
        <span class="favorite-btn__count">@count</span>
    }
</button>

@code {
    [Parameter, EditorRequired] public int ListingId { get; set; }

    [CascadingParameter]
    private Task<AuthenticationState>? AuthState { get; set; }

    private bool isFavorited;
    private int count;
    private bool isAuthenticated;

    protected override async Task OnInitializedAsync()
    {
        if (AuthState != null)
        {
            var state = await AuthState;
            isAuthenticated = state.User.Identity?.IsAuthenticated ?? false;
        }
        var (fav, cnt) = await FavoritesApi.GetStatusAsync(ListingId);
        isFavorited = fav;
        count = cnt;
    }

    private async Task Toggle()
    {
        if (!isAuthenticated)
        {
            Nav.NavigateTo($"/Account/Login?returnUrl=/property/{ListingId}");
            return;
        }
        var newState = await FavoritesApi.ToggleFavoriteAsync(ListingId, isFavorited);
        if (newState && !isFavorited) count++;
        else if (!newState && isFavorited && count > 0) count--;
        isFavorited = newState;
    }
}
```

- [ ] **Step 3: Modificar PropertyDetail.razor**

Leer `C:\Agentes\PropertyMap\src\PropertyMap.Web\PropertyMap.Web\Components\Pages\PropertyDetail.razor` completo.

Agregar `@using PropertyMap.Web.Components.Shared` en la sección de directivas al inicio del archivo (debajo de las `@using` existentes).

Agregar `<FavoriteButton ListingId="@Id" />` justo después de la línea `<div class="detail-price">@FormatPrice()</div>` (aprox. línea 85). El bloque del header quedaría:

```razor
<div class="detail-header">
    <div>
        <span class="detail-badge badge-@detail.Operacion.ToLower()">@OperacionLabel</span>
        <h1 class="detail-title">@detail.Titulo</h1>
        <p class="detail-address">
            <svg width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true">
                <path d="M21 10c0 7-9 13-9 13s-9-6-9-13a9 9 0 0 1 18 0z"/><circle cx="12" cy="10" r="3"/>
            </svg>
            @detail.DireccionTexto, @detail.Ciudad, @detail.Provincia
        </p>
    </div>
    <div style="display:flex;align-items:center;gap:var(--space-3)">
        <div class="detail-price">@FormatPrice()</div>
        <FavoriteButton ListingId="@Id" />
    </div>
</div>
```

- [ ] **Step 4: Agregar estilos del FavoriteButton a app.css**

Leer `C:\Agentes\PropertyMap\src\PropertyMap.Web\PropertyMap.Web\wwwroot\css\app.css` y agregar al final:

```css
/* ── FavoriteButton ──────────────────────────────────────────────── */
.favorite-btn {
    display: inline-flex;
    align-items: center;
    gap: 4px;
    background: none;
    border: 1.5px solid var(--color-border, oklch(90% 0.01 250));
    border-radius: var(--radius-md, 8px);
    padding: 6px 10px;
    cursor: pointer;
    color: var(--color-text-muted, oklch(55% 0.01 250));
    transition: color 0.15s, border-color 0.15s, background 0.15s;
    font-size: 0.875rem;
}

.favorite-btn:hover {
    border-color: oklch(65% 0.2 15);
    color: oklch(55% 0.2 15);
}

.favorite-btn--active {
    color: oklch(55% 0.2 15);
    border-color: oklch(65% 0.2 15);
    background: oklch(96% 0.05 15);
}

.favorite-btn__count {
    font-size: 0.8125rem;
    font-weight: 600;
}
```

- [ ] **Step 5: Build Web**

```bash
cd C:/Agentes/PropertyMap/src
dotnet build PropertyMap.Web/PropertyMap.Web/PropertyMap.Web.csproj 2>&1 | tail -10
```
Expected: `Build succeeded.`

- [ ] **Step 6: Commit**

```bash
cd C:/Agentes/PropertyMap/src
git add PropertyMap.Web/PropertyMap.Web/Components/Shared/FavoriteButton.razor \
        PropertyMap.Web/PropertyMap.Web/Components/Pages/PropertyDetail.razor \
        PropertyMap.Web/PropertyMap.Web/wwwroot/css/app.css
git commit -m "feat(web): add FavoriteButton component and integrate into PropertyDetail"
```

---

## Task 12: Navbar — links de perfil y favoritos

**Files:**
- Modify: `PropertyMap.Web/PropertyMap.Web/Components/Layout/Navbar.razor`

- [ ] **Step 1: Leer Navbar actual**

Leer `C:\Agentes\PropertyMap\src\PropertyMap.Web\PropertyMap.Web\Components\Layout\Navbar.razor` completo.

- [ ] **Step 2: Reemplazar Navbar.razor**

El archivo actual tiene un `<AuthorizeView>` anidado para el rol Publisher. Reemplazarlo con:

```razor
@using Microsoft.AspNetCore.Components.Authorization

<nav class="pm-navbar" role="navigation" aria-label="Navegación principal">
    <a href="/" class="pm-navbar__logo" aria-label="PropertyMap — Inicio">
        PropertyMap
    </a>

    <div class="pm-navbar__actions">
        <AuthorizeView>
            <Authorized>
                <a href="/account/favorites" class="btn-ghost">♥ Favoritos</a>
                <a href="/account/profile" class="btn-ghost">Mi perfil</a>
                <AuthorizeView Roles="Publisher" Context="publisherCtx">
                    <Authorized Context="pAuth">
                        <a href="/publisher/dashboard" class="btn-ghost">Mi panel</a>
                        <a href="/publicar" class="btn-primary">Publicar</a>
                    </Authorized>
                    <NotAuthorized Context="pNotAuth">
                        <a href="/publicar" class="btn-ghost">Publicar</a>
                    </NotAuthorized>
                </AuthorizeView>
            </Authorized>
            <NotAuthorized>
                <a href="/Account/Login" class="btn-ghost">Iniciar sesión</a>
                <a href="/publicar" class="btn-primary">Publicar</a>
            </NotAuthorized>
        </AuthorizeView>
    </div>
</nav>
```

- [ ] **Step 3: Build Web**

```bash
cd C:/Agentes/PropertyMap/src
dotnet build PropertyMap.Web/PropertyMap.Web/PropertyMap.Web.csproj 2>&1 | tail -10
```
Expected: `Build succeeded.`

- [ ] **Step 4: Correr todos los tests del backend**

```bash
cd C:/Agentes/PropertyMap/src
dotnet test PropertyMap.Tests/PropertyMap.Tests.csproj -v n 2>&1 | tail -10
```
Expected: todos los tests PASS.

- [ ] **Step 5: Commit**

```bash
cd C:/Agentes/PropertyMap/src
git add PropertyMap.Web/PropertyMap.Web/Components/Layout/Navbar.razor
git commit -m "feat(web): add profile and favorites links to Navbar for authenticated users"
```

---

## Self-Review

### Spec coverage

- ✅ DTOs `UserProfileResponse`, `UpdateProfileRequest`, `FavoriteStatusResponse` — Task 1
- ✅ `IFavoriteRepository` + `FavoriteRepository` — Task 2
- ✅ `IViewTrackingService` + `ViewTrackingService` (dedup por día por usuario/IP) — Task 3
- ✅ `IImageService.SaveAvatarAsync` — elimina anterior, guarda en `uploads/avatars/{userId}/` — Task 4
- ✅ `UserController` GET/PUT profile + POST avatar — Task 5
- ✅ `FavoritesController` GET list + POST/DELETE toggle + GET status (público) — Task 6
- ✅ `ListingsController.GetById` llama tracking con try/catch — Task 7
- ✅ Blazor services `UserApiService` + `FavoritesApiService` — Task 8
- ✅ `/account/profile` — edita nombre/apellido + sube avatar — Task 9
- ✅ `/account/favorites` — lista con botón Quitar — Task 10
- ✅ `FavoriteButton` en PropertyDetail — ícono corazón, toggle, count — Task 11
- ✅ Navbar — links Favoritos y Mi perfil para usuarios autenticados — Task 12
- ✅ Tests: UserController (4), FavoritesController (4), ViewTracking (1) — Tasks 3, 5, 6

### Dependencias entre tasks

```
Task 1 → Task 2 (FavoriteRepository usa MyListingDto del Core)
Task 1 → Task 6 (FavoritesController devuelve FavoriteStatusResponse)
Task 2 → Task 6 (FavoritesController inyecta IFavoriteRepository)
Task 3 → Task 7 (ListingsController inyecta IViewTrackingService)
Task 4 → Task 5 (UserController inyecta IImageService con SaveAvatarAsync)
Task 5 → Task 6 (Program.cs — IFavoriteRepository ya fue registrado en Task 5)
Task 8 → Task 9, 10, 11 (páginas usan IUserApiService e IFavoritesApiService)
Task 11 → PropertyDetail necesita el componente FavoriteButton del Task 11
```

### Notas para el implementador

- `FavoriteButton` usa `[CascadingParameter] Task<AuthenticationState>` — funciona porque `AddCascadingAuthenticationState()` ya está registrado en Program.cs (Web).
- `ViewTrackingService` usa range query sobre `FechaVista` (>= start, < end) para evitar casts SQL que no se traducen bien.
- El test de ViewTracking crea un admin temporal por test — esto funciona porque `TestWebApplicationFactory` usa un DB en memoria único por instancia de factory.
- `ToggleFavoriteAsync` en `FavoritesApiService` devuelve el estado resultante del toggle (true = ahora es favorito, false = dejó de serlo).
- La `GetStatus` del FavoritesController es `[AllowAnonymous]` aunque el controller tenga `[Authorize]` a nivel clase — los atributos a nivel método tienen precedencia.

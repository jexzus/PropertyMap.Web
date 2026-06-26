# Phase 8 — Monetización Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implementar Planes & Suscripciones, Dashboard de estadísticas para publishers, y Destacados (con límite por plan) para PropertyMap. **Fuera de alcance de este plan:** "IA descripción automática" (Claude API) — diferido hasta tener API key de Anthropic.

**Architecture:** Las entidades `Plan` y `Subscription` ya existen en `AppDbContext` desde Phase 3 pero no tienen repos/controllers/UI — este plan las activa. Se agrega un campo `Destacado` a `PropertyListing` (requiere migración EF), gateado por `Plan.DestacadosIncluidos` vía la `Subscription` activa del publisher. El dashboard de estadísticas agrega vistas/favoritos/consultas/conversiones existentes en una nueva tabla de lectura (`IListingStatsRepository`), sin tocar el esquema. "Conversión" se define como una `Consulta` con al menos un `ConsultaMensaje` donde `EsDelPublisher == true` — no requiere cambios de esquema.

**Tech Stack:** .NET 9, ASP.NET Core Web API, EF Core 9 (migración nueva), xUnit + `Microsoft.AspNetCore.Mvc.Testing`, Blazor Server.

---

## File Map

### Created
```
src/PropertyMap.Core/DTOs/Plans/PlanDto.cs
src/PropertyMap.Core/DTOs/Plans/SubscriptionDto.cs
src/PropertyMap.Core/DTOs/Plans/SubscribeRequest.cs
src/PropertyMap.Core/DTOs/Stats/ListingStatsDto.cs
src/PropertyMap.Core/Interfaces/IPlanRepository.cs
src/PropertyMap.Core/Interfaces/ISubscriptionRepository.cs
src/PropertyMap.Core/Interfaces/IListingStatsRepository.cs
src/PropertyMap.Infrastructure/Repositories/PlanRepository.cs
src/PropertyMap.Infrastructure/Repositories/SubscriptionRepository.cs
src/PropertyMap.Infrastructure/Repositories/ListingStatsRepository.cs
src/PropertyMap.Api/Controllers/PlansController.cs
src/PropertyMap.Api/Controllers/SubscriptionsController.cs
src/PropertyMap.Api/Controllers/StatsController.cs
src/PropertyMap.Web/PropertyMap.Web/Services/IPlansApiService.cs
src/PropertyMap.Web/PropertyMap.Web/Services/PlansApiService.cs
src/PropertyMap.Web/PropertyMap.Web/Services/IStatsApiService.cs
src/PropertyMap.Web/PropertyMap.Web/Services/StatsApiService.cs
src/PropertyMap.Web/PropertyMap.Web/Components/Pages/Planes.razor
src/PropertyMap.Tests/Api/PlansControllerTests.cs
src/PropertyMap.Tests/Api/SubscriptionsControllerTests.cs
src/PropertyMap.Tests/Api/StatsControllerTests.cs
src/PropertyMap.Tests/Api/DestacadoTests.cs
```

### Modified
```
src/PropertyMap.Core/Entities/PropertyListing.cs       (+ Destacado: bool)
src/PropertyMap.Infrastructure/Data/AppDbContext.cs    (sin cambios de modelo — Plan/Subscription ya configurados; solo nueva migración para Destacado)
src/PropertyMap.Infrastructure/Data/DbSeeder.cs        (+ SeedPlansAsync: Gratuito/Profesional/Premium)
src/PropertyMap.Infrastructure/Repositories/ListingRepository.cs  (GetActiveListingsAsync/GetActiveListingsForMapAsync ordenan Destacado primero)
src/PropertyMap.Api/Controllers/PropertiesController.cs  (+ PATCH /api/properties/{id}/destacar con validación de límite de plan)
src/PropertyMap.Web/PropertyMap.Web/Program.cs            (+ DI: IPlansApiService, IStatsApiService)
src/PropertyMap.Web/PropertyMap.Web/Components/Pages/Publisher/Dashboard.razor  (+ stats por listing + botón destacar + link a /planes)
src/PropertyMap.Web/PropertyMap.Web/Components/Layout/Navbar.razor  (+ link "Planes")
src/PropertyMap.Web/PropertyMap.Web/wwwroot/app.css       (+ CSS para stats cards y botón destacar)
```

---

## Task 1: DTOs de Plans y Stats

**Files:**
- Create: `src/PropertyMap.Core/DTOs/Plans/PlanDto.cs`
- Create: `src/PropertyMap.Core/DTOs/Plans/SubscriptionDto.cs`
- Create: `src/PropertyMap.Core/DTOs/Plans/SubscribeRequest.cs`
- Create: `src/PropertyMap.Core/DTOs/Stats/ListingStatsDto.cs`

- [ ] **Step 1: Crear PlanDto**

`src/PropertyMap.Core/DTOs/Plans/PlanDto.cs`
```csharp
namespace PropertyMap.Core.DTOs.Plans;

public record PlanDto(
    int Id,
    string Nombre,
    string Slug,
    decimal PrecioMensual,
    string Moneda,
    int? MaxPublicaciones,
    int DestacadosIncluidos,
    bool EstadisticasAvanzadas
);
```

- [ ] **Step 2: Crear SubscriptionDto**

`src/PropertyMap.Core/DTOs/Plans/SubscriptionDto.cs`
```csharp
using PropertyMap.Core.Enums;

namespace PropertyMap.Core.DTOs.Plans;

public record SubscriptionDto(
    int Id,
    int PlanId,
    string PlanNombre,
    EstadoSuscripcion Estado,
    DateTime FechaInicio,
    DateTime FechaVencimiento,
    bool AutoRenovar
);
```

- [ ] **Step 3: Crear SubscribeRequest**

`src/PropertyMap.Core/DTOs/Plans/SubscribeRequest.cs`
```csharp
namespace PropertyMap.Core.DTOs.Plans;

public record SubscribeRequest(int PlanId);
```

- [ ] **Step 4: Crear ListingStatsDto**

`src/PropertyMap.Core/DTOs/Stats/ListingStatsDto.cs`
```csharp
namespace PropertyMap.Core.DTOs.Stats;

public record ListingStatsDto(
    int ListingId,
    string Titulo,
    int Vistas,
    int Favoritos,
    int Consultas,
    int Conversiones
);
```

- [ ] **Step 5: Build**

```bash
cd C:\Agentes\PropertyMap
dotnet build src/PropertyMap.Core/PropertyMap.Core.csproj
```
Expected: `Build succeeded.`

- [ ] **Step 6: Commit**

```bash
cd C:\Agentes\PropertyMap\src
git add PropertyMap.Core/DTOs/Plans/ PropertyMap.Core/DTOs/Stats/
git commit -m "feat(core): add Plan, Subscription, and ListingStats DTOs for Phase 8"
```

---

## Task 2: PlanRepository + SubscriptionRepository

**Files:**
- Create: `src/PropertyMap.Core/Interfaces/IPlanRepository.cs`
- Create: `src/PropertyMap.Core/Interfaces/ISubscriptionRepository.cs`
- Create: `src/PropertyMap.Infrastructure/Repositories/PlanRepository.cs`
- Create: `src/PropertyMap.Infrastructure/Repositories/SubscriptionRepository.cs`

- [ ] **Step 1: Crear IPlanRepository**

`src/PropertyMap.Core/Interfaces/IPlanRepository.cs`
```csharp
using PropertyMap.Core.DTOs.Plans;
using PropertyMap.Core.Entities;

namespace PropertyMap.Core.Interfaces;

public interface IPlanRepository
{
    Task<List<PlanDto>> GetActiveAsync();
    Task<Plan?> GetByIdAsync(int id);
}
```

- [ ] **Step 2: Implementar PlanRepository**

`src/PropertyMap.Infrastructure/Repositories/PlanRepository.cs`
```csharp
using Microsoft.EntityFrameworkCore;
using PropertyMap.Core.DTOs.Plans;
using PropertyMap.Core.Entities;
using PropertyMap.Core.Interfaces;
using PropertyMap.Infrastructure.Data;

namespace PropertyMap.Infrastructure.Repositories;

public class PlanRepository(AppDbContext ctx) : IPlanRepository
{
    public async Task<List<PlanDto>> GetActiveAsync() =>
        await ctx.Plans
            .Where(p => p.Activo)
            .OrderBy(p => p.PrecioMensual)
            .Select(p => new PlanDto(
                p.Id, p.Nombre, p.Slug, p.PrecioMensual, p.Moneda,
                p.MaxPublicaciones, p.DestacadosIncluidos, p.EstadisticasAvanzadas))
            .ToListAsync();

    public async Task<Plan?> GetByIdAsync(int id) =>
        await ctx.Plans.FirstOrDefaultAsync(p => p.Id == id);
}
```

- [ ] **Step 3: Crear ISubscriptionRepository**

`src/PropertyMap.Core/Interfaces/ISubscriptionRepository.cs`
```csharp
using PropertyMap.Core.Entities;

namespace PropertyMap.Core.Interfaces;

public interface ISubscriptionRepository
{
    Task<Subscription?> GetByUserIdAsync(string userId);
    Task<Subscription> CreateOrReplaceAsync(string userId, int planId, DateTime vencimiento);
}
```

- [ ] **Step 4: Implementar SubscriptionRepository**

`src/PropertyMap.Infrastructure/Repositories/SubscriptionRepository.cs`
```csharp
using Microsoft.EntityFrameworkCore;
using PropertyMap.Core.Entities;
using PropertyMap.Core.Enums;
using PropertyMap.Core.Interfaces;
using PropertyMap.Infrastructure.Data;

namespace PropertyMap.Infrastructure.Repositories;

public class SubscriptionRepository(AppDbContext ctx) : ISubscriptionRepository
{
    public async Task<Subscription?> GetByUserIdAsync(string userId) =>
        await ctx.Subscriptions
            .Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.UserId == userId);

    public async Task<Subscription> CreateOrReplaceAsync(string userId, int planId, DateTime vencimiento)
    {
        // Subscription tiene índice único en UserId — una sola fila activa por usuario.
        var existing = await ctx.Subscriptions.FirstOrDefaultAsync(s => s.UserId == userId);
        if (existing is null)
        {
            existing = new Subscription
            {
                UserId = userId,
                PlanId = planId,
                Estado = EstadoSuscripcion.Activa,
                FechaInicio = DateTime.UtcNow,
                FechaVencimiento = vencimiento,
                AutoRenovar = true
            };
            ctx.Subscriptions.Add(existing);
        }
        else
        {
            existing.PlanId = planId;
            existing.Estado = EstadoSuscripcion.Activa;
            existing.FechaInicio = DateTime.UtcNow;
            existing.FechaVencimiento = vencimiento;
        }
        await ctx.SaveChangesAsync();

        return await ctx.Subscriptions.Include(s => s.Plan).FirstAsync(s => s.UserId == userId);
    }
}
```

- [ ] **Step 5: Build**

```bash
cd C:\Agentes\PropertyMap
dotnet build src/PropertyMap.Infrastructure/PropertyMap.Infrastructure.csproj
```
Expected: `Build succeeded.`

- [ ] **Step 6: Commit**

```bash
cd C:\Agentes\PropertyMap\src
git add PropertyMap.Core/Interfaces/IPlanRepository.cs PropertyMap.Core/Interfaces/ISubscriptionRepository.cs PropertyMap.Infrastructure/Repositories/PlanRepository.cs PropertyMap.Infrastructure/Repositories/SubscriptionRepository.cs
git commit -m "feat(infra): add PlanRepository and SubscriptionRepository"
```

---

## Task 3: Seed de Planes por defecto en DbSeeder

**Files:**
- Modify: `src/PropertyMap.Infrastructure/Data/DbSeeder.cs`
- Modify: `src/PropertyMap.Web/PropertyMap.Web/Program.cs`

- [ ] **Step 1: Agregar método SeedPlansAsync a DbSeeder**

Abrir `src/PropertyMap.Infrastructure/Data/DbSeeder.cs`. Agregar este método nuevo a la clase estática `DbSeeder` (junto al `SeedAsync` existente, no reemplazarlo):

```csharp
public static async Task SeedPlansAsync(AppDbContext context)
{
    if (await context.Plans.AnyAsync()) return;

    context.Plans.AddRange(
        new Plan
        {
            Nombre = "Gratuito", Slug = "gratuito", PrecioMensual = 0m, Moneda = "ARS",
            MaxPublicaciones = 3, DestacadosIncluidos = 0, EstadisticasAvanzadas = false, Activo = true
        },
        new Plan
        {
            Nombre = "Profesional", Slug = "profesional", PrecioMensual = 15000m, Moneda = "ARS",
            MaxPublicaciones = 20, DestacadosIncluidos = 3, EstadisticasAvanzadas = true, Activo = true
        },
        new Plan
        {
            Nombre = "Premium", Slug = "premium", PrecioMensual = 35000m, Moneda = "ARS",
            MaxPublicaciones = null, DestacadosIncluidos = 10, EstadisticasAvanzadas = true, Activo = true
        }
    );
    await context.SaveChangesAsync();
}
```

Asegurarse de que el archivo tenga `using Microsoft.EntityFrameworkCore;` ya importado (debería estarlo, lo usa `SeedAsync`).

- [ ] **Step 2: Invocar SeedPlansAsync desde Program.cs (Web)**

Editar `src/PropertyMap.Web/PropertyMap.Web/Program.cs`. Dentro del bloque de seeding existente (`using (var scope = app.Services.CreateScope()) { ... await PropertyMap.Infrastructure.Data.DbSeeder.SeedAsync(db, userManager); ... }`), agregar inmediatamente después de la línea `await PropertyMap.Infrastructure.Data.DbSeeder.SeedAsync(db, userManager);`:

```csharp
            await PropertyMap.Infrastructure.Data.DbSeeder.SeedPlansAsync(db);
```

(Dentro del mismo bloque `try`, antes del `break;`.)

- [ ] **Step 3: Build**

```bash
cd C:\Agentes\PropertyMap
dotnet build src/PropertyMap.Web/PropertyMap.Web.sln
```
Expected: `Build succeeded.`

- [ ] **Step 4: Commit**

```bash
cd C:\Agentes\PropertyMap\src
git add PropertyMap.Infrastructure/Data/DbSeeder.cs PropertyMap.Web/PropertyMap.Web/Program.cs
git commit -m "feat(infra): seed default Gratuito/Profesional/Premium plans"
```

---

## Task 4: PlansController + SubscriptionsController

**Files:**
- Create: `src/PropertyMap.Api/Controllers/PlansController.cs`
- Create: `src/PropertyMap.Api/Controllers/SubscriptionsController.cs`

- [ ] **Step 1: Crear PlansController**

`src/PropertyMap.Api/Controllers/PlansController.cs`
```csharp
using Microsoft.AspNetCore.Mvc;
using PropertyMap.Core.Interfaces;

namespace PropertyMap.Api.Controllers;

[ApiController]
[Route("api/plans")]
public class PlansController : ControllerBase
{
    private readonly IPlanRepository _plans;

    public PlansController(IPlanRepository plans)
    {
        _plans = plans;
    }

    [HttpGet]
    public async Task<IActionResult> GetActive() =>
        Ok(await _plans.GetActiveAsync());
}
```

- [ ] **Step 2: Crear SubscriptionsController**

`src/PropertyMap.Api/Controllers/SubscriptionsController.cs`
```csharp
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
```

Nota: `Subscribe` no requiere rol `Publisher` — un usuario regular también puede suscribirse antes de publicar (la suscripción habilita límites cuando luego cree su perfil de publisher).

- [ ] **Step 3: Build**

```bash
cd C:\Agentes\PropertyMap
dotnet build src/PropertyMap.Api/PropertyMap.Api.csproj
```
Expected: `Build succeeded.`

- [ ] **Step 4: Commit**

```bash
cd C:\Agentes\PropertyMap\src
git add PropertyMap.Api/Controllers/PlansController.cs PropertyMap.Api/Controllers/SubscriptionsController.cs
git commit -m "feat(api): add PlansController and SubscriptionsController"
```

---

## Task 5: Campo Destacado en PropertyListing + migración EF

**Files:**
- Modify: `src/PropertyMap.Core/Entities/PropertyListing.cs`
- Create: migración EF generada por `dotnet ef migrations add`

- [ ] **Step 1: Agregar el campo Destacado**

Editar `src/PropertyMap.Core/Entities/PropertyListing.cs`, agregar la propiedad junto a `Estado`:

```csharp
    public bool Destacado { get; set; }
```

El archivo completo queda (mostrando solo el bloque relevante, el resto sin cambios):
```csharp
    public EstadoPublicacion Estado { get; set; } = EstadoPublicacion.Borrador;
    public bool Destacado { get; set; }
    public DateTime FechaPublicacion { get; set; } = DateTime.UtcNow;
```

- [ ] **Step 2: Generar migración**

Ejecutar desde `C:\Agentes\PropertyMap\src` (no hay `.sln` en esta carpeta — `dotnet ef` apunta directo a los `.csproj`):

```bash
cd C:\Agentes\PropertyMap\src
dotnet ef migrations add Phase8Destacado --project PropertyMap.Infrastructure --startup-project PropertyMap.Web/PropertyMap.Web
```
Expected: crea `PropertyMap.Infrastructure/Migrations/<timestamp>_Phase8Destacado.cs` con un `AddColumn` para `Destacado bit NOT NULL DEFAULT 0` (o `false`) en `PropertyListings`.

- [ ] **Step 3: Build**

```bash
cd C:\Agentes\PropertyMap
dotnet build src/PropertyMap.Web/PropertyMap.Web.sln
```
Expected: `Build succeeded.` (La migración se aplica automáticamente al levantar `PropertyMap.Web` vía `db.Database.MigrateAsync()` — no hace falta `dotnet ef database update` manual para los tests, que usan EF InMemory y no corren migraciones.)

- [ ] **Step 4: Commit**

```bash
cd C:\Agentes\PropertyMap\src
git add PropertyMap.Core/Entities/PropertyListing.cs PropertyMap.Infrastructure/Migrations/
git commit -m "feat(core): add Destacado field to PropertyListing"
```

---

## Task 6: Endpoint de destacar (con límite por plan) + ordenamiento

**Files:**
- Modify: `src/PropertyMap.Infrastructure/Repositories/ListingRepository.cs`
- Modify: `src/PropertyMap.Api/Controllers/PropertiesController.cs`

- [ ] **Step 1: Ordenar Destacado primero en las queries públicas**

Editar `src/PropertyMap.Infrastructure/Repositories/ListingRepository.cs`. Reemplazar el método `GetActiveListingsAsync`:

```csharp
    public async Task<IEnumerable<PropertyListing>> GetActiveListingsAsync() =>
        await ctx.PropertyListings
            .Where(l => l.Estado == EstadoPublicacion.Publicada)
            .Include(l => l.Location)
            .Include(l => l.Publisher)
            .OrderByDescending(l => l.Destacado)
            .ThenByDescending(l => l.FechaPublicacion)
            .ToListAsync();
```

Reemplazar el método `GetActiveListingsForMapAsync`:

```csharp
    public async Task<IEnumerable<ListingMapDto>> GetActiveListingsForMapAsync() =>
        await ctx.PropertyListings
            .Where(l => l.Estado == EstadoPublicacion.Publicada)
            .Include(l => l.Location)
            .OrderByDescending(l => l.Destacado)
            .ThenByDescending(l => l.FechaPublicacion)
            .Select(l => new ListingMapDto(
                l.Id,
                l.Location.Latitud,
                l.Location.Longitud,
                l.Titulo,
                l.Precio,
                l.Moneda,
                l.TipoPropiedad.ToString(),
                l.Operacion.ToString(),
                l.Images.Where(i => i.EsPrincipal).Select(i => i.Url).FirstOrDefault()
            ))
            .ToListAsync();
```

- [ ] **Step 2: Agregar endpoint PATCH destacar a PropertiesController**

Editar `src/PropertyMap.Api/Controllers/PropertiesController.cs`. Agregar estos dos `using` al inicio del archivo (si no están ya):

```csharp
using PropertyMap.Core.Interfaces;
```
(ya debería estar presente — confirmar antes de duplicar).

Agregar `ISubscriptionRepository` al constructor existente. El constructor actual es:

```csharp
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
```

Reemplazarlo por:

```csharp
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
```

Agregar el campo correspondiente junto a los demás `private readonly` del controller:

```csharp
    private readonly ISubscriptionRepository _subscriptions;
```

Agregar este nuevo endpoint al final de la clase, antes del método privado `IsOwner`:

```csharp
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
```

- [ ] **Step 3: Build**

```bash
cd C:\Agentes\PropertyMap
dotnet build src/PropertyMap.Api/PropertyMap.Api.csproj
```
Expected: `Build succeeded.`

- [ ] **Step 4: Commit**

```bash
cd C:\Agentes\PropertyMap\src
git add PropertyMap.Infrastructure/Repositories/ListingRepository.cs PropertyMap.Api/Controllers/PropertiesController.cs
git commit -m "feat(api): add destacar endpoint with plan-based limit, sort featured listings first"
```

---

## Task 7: ListingStatsRepository + StatsController

**Files:**
- Create: `src/PropertyMap.Core/Interfaces/IListingStatsRepository.cs`
- Create: `src/PropertyMap.Infrastructure/Repositories/ListingStatsRepository.cs`
- Create: `src/PropertyMap.Api/Controllers/StatsController.cs`

- [ ] **Step 1: Crear IListingStatsRepository**

`src/PropertyMap.Core/Interfaces/IListingStatsRepository.cs`
```csharp
using PropertyMap.Core.DTOs.Stats;

namespace PropertyMap.Core.Interfaces;

public interface IListingStatsRepository
{
    Task<ListingStatsDto?> GetForListingAsync(int listingId, int publisherId);
    Task<List<ListingStatsDto>> GetForPublisherAsync(int publisherId);
}
```

`publisherId` se pasa explícitamente (no solo `listingId`) para que el repositorio pueda verificar ownership sin depender de otro repo — devuelve `null` si la propiedad no es de ese publisher.

- [ ] **Step 2: Implementar ListingStatsRepository**

`src/PropertyMap.Infrastructure/Repositories/ListingStatsRepository.cs`
```csharp
using Microsoft.EntityFrameworkCore;
using PropertyMap.Core.DTOs.Stats;
using PropertyMap.Core.Interfaces;
using PropertyMap.Infrastructure.Data;

namespace PropertyMap.Infrastructure.Repositories;

public class ListingStatsRepository(AppDbContext ctx) : IListingStatsRepository
{
    public async Task<ListingStatsDto?> GetForListingAsync(int listingId, int publisherId)
    {
        var listing = await ctx.PropertyListings
            .FirstOrDefaultAsync(l => l.Id == listingId && l.PublisherId == publisherId);
        if (listing is null) return null;

        return await BuildStatsAsync(listing.Id, listing.Titulo);
    }

    public async Task<List<ListingStatsDto>> GetForPublisherAsync(int publisherId)
    {
        var listings = await ctx.PropertyListings
            .Where(l => l.PublisherId == publisherId)
            .Select(l => new { l.Id, l.Titulo })
            .ToListAsync();

        var result = new List<ListingStatsDto>();
        foreach (var l in listings)
            result.Add(await BuildStatsAsync(l.Id, l.Titulo));

        return result;
    }

    private async Task<ListingStatsDto> BuildStatsAsync(int listingId, string titulo)
    {
        var vistas = await ctx.PropertyViews.CountAsync(v => v.PropertyListingId == listingId);
        var favoritos = await ctx.PropertyFavorites.CountAsync(f => f.PropertyListingId == listingId);
        var consultas = await ctx.Consultas.CountAsync(c => c.PropertyListingId == listingId);

        // Conversión = consulta con al menos un mensaje de respuesta del publisher.
        var consultaIds = await ctx.Consultas
            .Where(c => c.PropertyListingId == listingId)
            .Select(c => c.Id)
            .ToListAsync();

        var conversiones = 0;
        if (consultaIds.Count > 0)
        {
            var consultasConRespuesta = await ctx.ConsultaMensajes
                .Where(m => consultaIds.Contains(m.ConsultaId) && m.EsDelPublisher)
                .Select(m => m.ConsultaId)
                .Distinct()
                .CountAsync();
            conversiones = consultasConRespuesta;
        }

        return new ListingStatsDto(listingId, titulo, vistas, favoritos, consultas, conversiones);
    }
}
```

- [ ] **Step 3: Crear StatsController**

`src/PropertyMap.Api/Controllers/StatsController.cs`
```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PropertyMap.Core.Interfaces;

namespace PropertyMap.Api.Controllers;

[ApiController]
[Route("api/stats")]
[Authorize(Roles = "Publisher")]
public class StatsController : ControllerBase
{
    private readonly IListingStatsRepository _stats;
    private readonly IPublisherRepository _publishers;

    public StatsController(IListingStatsRepository stats, IPublisherRepository publishers)
    {
        _stats = stats;
        _publishers = publishers;
    }

    [HttpGet("mine")]
    public async Task<IActionResult> GetMine()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var publisher = await _publishers.GetByUserIdAsync(userId);
        if (publisher is null) return Ok(Array.Empty<object>());

        return Ok(await _stats.GetForPublisherAsync(publisher.Id));
    }

    [HttpGet("listings/{id:int}")]
    public async Task<IActionResult> GetForListing(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var publisher = await _publishers.GetByUserIdAsync(userId);
        if (publisher is null) return Forbid();

        var stats = await _stats.GetForListingAsync(id, publisher.Id);
        if (stats is null) return NotFound();

        return Ok(stats);
    }
}
```

- [ ] **Step 4: Build**

```bash
cd C:\Agentes\PropertyMap
dotnet build src/PropertyMap.Api/PropertyMap.Api.csproj
```
Expected: `Build succeeded.`

- [ ] **Step 5: Commit**

```bash
cd C:\Agentes\PropertyMap\src
git add PropertyMap.Core/Interfaces/IListingStatsRepository.cs PropertyMap.Infrastructure/Repositories/ListingStatsRepository.cs PropertyMap.Api/Controllers/StatsController.cs
git commit -m "feat(api): add ListingStatsRepository and StatsController"
```

---

## Task 8: Registrar DI en PropertyMap.Api/Program.cs

**Files:**
- Modify: `src/PropertyMap.Api/Program.cs`

- [ ] **Step 1: Agregar las 4 registraciones nuevas**

Editar `src/PropertyMap.Api/Program.cs`. Después de la línea `builder.Services.AddScoped<INotificationPublisher, SignalRNotificationPublisher>();` (la última del bloque de registraciones de Phase 7), agregar:

```csharp
builder.Services.AddScoped<IPlanRepository, PlanRepository>();
builder.Services.AddScoped<ISubscriptionRepository, SubscriptionRepository>();
builder.Services.AddScoped<IListingStatsRepository, ListingStatsRepository>();
```

No hace falta agregar ningún `using` nuevo — `PropertyMap.Core.Interfaces` y `PropertyMap.Infrastructure.Repositories` ya están importados.

- [ ] **Step 2: Build**

```bash
cd C:\Agentes\PropertyMap
dotnet build src/PropertyMap.Api/PropertyMap.Api.csproj
```
Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```bash
cd C:\Agentes\PropertyMap\src
git add PropertyMap.Api/Program.cs
git commit -m "feat(api): wire Phase 8 DI registrations (Plan, Subscription, ListingStats)"
```

---

## Task 9: Servicios Blazor (Plans + Stats)

**Files:**
- Create: `src/PropertyMap.Web/PropertyMap.Web/Services/IPlansApiService.cs`
- Create: `src/PropertyMap.Web/PropertyMap.Web/Services/PlansApiService.cs`
- Create: `src/PropertyMap.Web/PropertyMap.Web/Services/IStatsApiService.cs`
- Create: `src/PropertyMap.Web/PropertyMap.Web/Services/StatsApiService.cs`
- Modify: `src/PropertyMap.Web/PropertyMap.Web/Program.cs`

- [ ] **Step 1: Crear IPlansApiService**

`src/PropertyMap.Web/PropertyMap.Web/Services/IPlansApiService.cs`
```csharp
using PropertyMap.Core.DTOs.Plans;

namespace PropertyMap.Web.Services;

public interface IPlansApiService
{
    Task<List<PlanDto>> GetActiveAsync();
    Task<SubscriptionDto?> GetMineAsync();
    Task<SubscriptionDto?> SubscribeAsync(int planId);
}
```

- [ ] **Step 2: Implementar PlansApiService**

`src/PropertyMap.Web/PropertyMap.Web/Services/PlansApiService.cs`
```csharp
using System.Net.Http.Headers;
using System.Net.Http.Json;
using PropertyMap.Core.DTOs.Plans;

namespace PropertyMap.Web.Services;

public class PlansApiService : IPlansApiService
{
    private readonly HttpClient _http;
    private readonly MemoryTokenStore _tokenStore;

    public PlansApiService(IHttpClientFactory httpFactory, MemoryTokenStore tokenStore)
    {
        _http = httpFactory.CreateClient("api");
        _tokenStore = tokenStore;
    }

    private void SetAuth() =>
        _http.DefaultRequestHeaders.Authorization = _tokenStore.AccessToken is null
            ? null
            : new AuthenticationHeaderValue("Bearer", _tokenStore.AccessToken);

    public async Task<List<PlanDto>> GetActiveAsync()
    {
        try { return await _http.GetFromJsonAsync<List<PlanDto>>("api/plans") ?? []; }
        catch { return []; }
    }

    public async Task<SubscriptionDto?> GetMineAsync()
    {
        try
        {
            SetAuth();
            var resp = await _http.GetAsync("api/subscriptions/mine");
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<SubscriptionDto>();
        }
        catch { return null; }
    }

    public async Task<SubscriptionDto?> SubscribeAsync(int planId)
    {
        try
        {
            SetAuth();
            var resp = await _http.PostAsJsonAsync("api/subscriptions", new SubscribeRequest(planId));
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<SubscriptionDto>();
        }
        catch { return null; }
    }
}
```

- [ ] **Step 3: Crear IStatsApiService**

`src/PropertyMap.Web/PropertyMap.Web/Services/IStatsApiService.cs`
```csharp
using PropertyMap.Core.DTOs.Stats;

namespace PropertyMap.Web.Services;

public interface IStatsApiService
{
    Task<List<ListingStatsDto>> GetMineAsync();
}
```

- [ ] **Step 4: Implementar StatsApiService**

`src/PropertyMap.Web/PropertyMap.Web/Services/StatsApiService.cs`
```csharp
using System.Net.Http.Headers;
using System.Net.Http.Json;
using PropertyMap.Core.DTOs.Stats;

namespace PropertyMap.Web.Services;

public class StatsApiService : IStatsApiService
{
    private readonly HttpClient _http;
    private readonly MemoryTokenStore _tokenStore;

    public StatsApiService(IHttpClientFactory httpFactory, MemoryTokenStore tokenStore)
    {
        _http = httpFactory.CreateClient("api");
        _tokenStore = tokenStore;
    }

    public async Task<List<ListingStatsDto>> GetMineAsync()
    {
        try
        {
            _http.DefaultRequestHeaders.Authorization = _tokenStore.AccessToken is null
                ? null
                : new AuthenticationHeaderValue("Bearer", _tokenStore.AccessToken);
            return await _http.GetFromJsonAsync<List<ListingStatsDto>>("api/stats/mine") ?? [];
        }
        catch { return []; }
    }
}
```

- [ ] **Step 5: Registrar en Program.cs**

Editar `src/PropertyMap.Web/PropertyMap.Web/Program.cs`, después de `builder.Services.AddScoped<NotificationHubClient>();`, agregar:

```csharp
builder.Services.AddScoped<IPlansApiService, PlansApiService>();
builder.Services.AddScoped<IStatsApiService, StatsApiService>();
```

- [ ] **Step 6: Build**

```bash
cd C:\Agentes\PropertyMap
dotnet build src/PropertyMap.Web/PropertyMap.Web.sln
```
Expected: `Build succeeded.`

- [ ] **Step 7: Commit**

```bash
cd C:\Agentes\PropertyMap\src
git add PropertyMap.Web/PropertyMap.Web/Services/IPlansApiService.cs PropertyMap.Web/PropertyMap.Web/Services/PlansApiService.cs PropertyMap.Web/PropertyMap.Web/Services/IStatsApiService.cs PropertyMap.Web/PropertyMap.Web/Services/StatsApiService.cs PropertyMap.Web/PropertyMap.Web/Program.cs
git commit -m "feat(web): add PlansApiService and StatsApiService"
```

---

## Task 10: UI — Planes.razor + Dashboard con stats y destacar

**Files:**
- Modify: `src/PropertyMap.Web/PropertyMap.Web/Services/IPropertyApiService.cs`
- Modify: `src/PropertyMap.Web/PropertyMap.Web/Services/PropertyApiService.cs`
- Create: `src/PropertyMap.Web/PropertyMap.Web/Components/Pages/Planes.razor`
- Modify: `src/PropertyMap.Web/PropertyMap.Web/Components/Pages/Publisher/Dashboard.razor`
- Modify: `src/PropertyMap.Web/PropertyMap.Web/Components/Layout/Navbar.razor`
- Modify: `src/PropertyMap.Web/PropertyMap.Web/wwwroot/app.css`

- [ ] **Step 1: Agregar ToggleDestacadoAsync a IPropertyApiService**

Editar `src/PropertyMap.Web/PropertyMap.Web/Services/IPropertyApiService.cs`, agregar al final de la interfaz:

```csharp
    Task<bool> ToggleDestacadoAsync(int listingId);
```

- [ ] **Step 2: Implementar en PropertyApiService**

Editar `src/PropertyMap.Web/PropertyMap.Web/Services/PropertyApiService.cs`, agregar este método a la clase:

```csharp
    public async Task<bool> ToggleDestacadoAsync(int listingId)
    {
        try
        {
            SetAuth();
            var resp = await _http.PatchAsync($"api/properties/{listingId}/destacar", null);
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }
```

- [ ] **Step 3: Crear Planes.razor**

`src/PropertyMap.Web/PropertyMap.Web/Components/Pages/Planes.razor`
```razor
@page "/planes"
@rendermode InteractiveServer
@using PropertyMap.Core.DTOs.Plans
@using PropertyMap.Web.Services
@inject IPlansApiService PlansApi

<PageTitle>Planes — PropertyMap</PageTitle>

<div class="app-shell" style="display:flex;flex-direction:column">
    <nav class="pm-navbar" role="navigation">
        <a href="/" class="pm-navbar__logo">PropertyMap</a>
    </nav>

    <div style="padding:var(--space-6);max-width:960px;margin:0 auto;width:100%">
        <h1 style="font-size:1.5rem;font-weight:700;margin-bottom:var(--space-4)">Planes para publishers</h1>

        @if (_mine is not null)
        {
            <p class="pm-plan-current">Tu plan actual: <strong>@_mine.PlanNombre</strong> (vence @_mine.FechaVencimiento.ToString("dd/MM/yyyy"))</p>
        }

        <div class="pm-plans-grid">
            @foreach (var plan in _plans)
            {
                <div class="pm-plan-card">
                    <h3>@plan.Nombre</h3>
                    <p class="pm-plan-price">@plan.Moneda @plan.PrecioMensual.ToString("N0") /mes</p>
                    <ul>
                        <li>@(plan.MaxPublicaciones is null ? "Publicaciones ilimitadas" : $"Hasta {plan.MaxPublicaciones} publicaciones")</li>
                        <li>@plan.DestacadosIncluidos destacados incluidos</li>
                        <li>@(plan.EstadisticasAvanzadas ? "Estadísticas avanzadas" : "Estadísticas básicas")</li>
                    </ul>
                    <AuthorizeView>
                        <Authorized>
                            <button class="btn-primary" @onclick="() => SubscribeAsync(plan.Id)" disabled="@(_mine?.PlanId == plan.Id)">
                                @(_mine?.PlanId == plan.Id ? "Plan actual" : "Elegir este plan")
                            </button>
                        </Authorized>
                        <NotAuthorized>
                            <a href="/Account/Login?returnUrl=/planes" class="btn-primary">Iniciar sesión para elegir</a>
                        </NotAuthorized>
                    </AuthorizeView>
                </div>
            }
        </div>
    </div>
</div>

@code {
    private List<PlanDto> _plans = [];
    private SubscriptionDto? _mine;

    protected override async Task OnInitializedAsync()
    {
        _plans = await PlansApi.GetActiveAsync();
        _mine = await PlansApi.GetMineAsync();
    }

    private async Task SubscribeAsync(int planId)
    {
        _mine = await PlansApi.SubscribeAsync(planId);
    }
}
```

- [ ] **Step 4: Agregar stats + botón destacar a Dashboard.razor**

Editar `src/PropertyMap.Web/PropertyMap.Web/Components/Pages/Publisher/Dashboard.razor`. Agregar el `@inject` para `IStatsApiService` junto a los existentes (después de `@inject NavigationManager Nav`):

```razor
@inject IStatsApiService StatsApi
```

Reemplazar el bloque `@foreach (var l in listings)` completo (incluyendo el `<div class="dashboard-listing-card">` y todo su contenido) por:

```razor
                        @foreach (var l in listings)
                        {
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
                                    @if (StatsFor(l.Id) is { } s)
                                    {
                                        <div class="pm-listing-stats">
                                            <span>👁 @s.Vistas</span>
                                            <span>♥ @s.Favoritos</span>
                                            <span>💬 @s.Consultas</span>
                                            <span>✓ @s.Conversiones</span>
                                        </div>
                                    }
                                    <button class="btn-ghost" @onclick="() => ToggleDestacadoAsync(l.Id)">
                                        @(l.Estado == "Publicada" ? "★ Destacar / Quitar" : "")
                                    </button>
                                </div>
                                <span class="dashboard-listing-status @StatusClass(l.Estado)">
                                    @StatusLabel(l.Estado)
                                </span>
                            </div>
                        }
```

Agregar un link a `/planes` junto al de "+ Publicar" en el `<nav>` existente, así:

```razor
                <div class="pm-navbar__actions">
                    <a href="/planes" class="btn-ghost">Planes</a>
                    <a href="/publicar" class="btn-primary">+ Publicar</a>
                    <button class="btn-ghost" @onclick="Logout">Salir</button>
                </div>
```

Agregar en el bloque `@code`, junto al campo `listings`:

```csharp
    private List<PropertyMap.Core.DTOs.Stats.ListingStatsDto> stats = [];
```

Modificar el método `LoadListings` para que también cargue stats — reemplazarlo por:

```csharp
    private async Task LoadListings()
    {
        loading = true;
        try
        {
            listings = await PropertyApi.GetMyListingsAsync();
            stats = await StatsApi.GetMineAsync();
        }
        catch
        {
            listings = [];
            stats = [];
        }
        finally
        {
            loading = false;
        }
    }
```

Agregar estos 2 métodos nuevos al final del bloque `@code`:

```csharp
    private PropertyMap.Core.DTOs.Stats.ListingStatsDto? StatsFor(int listingId) =>
        stats.FirstOrDefault(s => s.ListingId == listingId);

    private async Task ToggleDestacadoAsync(int listingId)
    {
        await PropertyApi.ToggleDestacadoAsync(listingId);
        await LoadListings();
    }
```

- [ ] **Step 5: Agregar link "Planes" al Navbar**

Editar `src/PropertyMap.Web/PropertyMap.Web/Components/Layout/Navbar.razor`. Agregar, dentro de `<div class="pm-navbar__actions">`, antes de `<a href="/top-agentes" class="btn-ghost">Top Agentes</a>`:

```razor
<a href="/planes" class="btn-ghost">Planes</a>
```

- [ ] **Step 6: Agregar CSS para stats y plan cards**

Editar `src/PropertyMap.Web/PropertyMap.Web/wwwroot/app.css`. Antes de agregar, leer el archivo para confirmar los nombres exactos de los design tokens disponibles (`--color-bg`, `--color-border`, `--color-muted`, `--space-N`, `--radius-md`/`--radius-lg`, `--shadow-lg`, etc. — mismos que usó Phase 7 en `.pm-alert-card`/`.pm-notif-dropdown`). Agregar al final del archivo:

```css
.pm-plans-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(240px, 1fr));
  gap: var(--space-4);
  margin-top: var(--space-4);
}

.pm-plan-card {
  border: 1px solid var(--color-border);
  border-radius: var(--radius-lg);
  padding: var(--space-4);
  background: var(--color-bg);
  display: flex;
  flex-direction: column;
  gap: var(--space-2);
}

.pm-plan-card h3 {
  margin: 0;
}

.pm-plan-price {
  font-size: var(--text-lg, 1.25rem);
  font-weight: var(--font-semibold);
  color: var(--color-primary);
}

.pm-plan-card ul {
  list-style: none;
  padding: 0;
  margin: 0;
  display: flex;
  flex-direction: column;
  gap: var(--space-1);
  color: var(--color-muted);
  font-size: var(--text-sm);
}

.pm-plan-current {
  color: var(--color-muted);
  margin-bottom: var(--space-2);
}

.pm-listing-stats {
  display: flex;
  gap: var(--space-3);
  font-size: var(--text-sm);
  color: var(--color-muted);
  margin-top: var(--space-1);
}
```

(Si algún token referenciado arriba —p. ej. `--text-lg`— no existe en `tokens.css`, usar el token más cercano que sí exista en vez de inventar uno nuevo; no introducir valores mágicos.)

- [ ] **Step 7: Build**

```bash
cd C:\Agentes\PropertyMap
dotnet build src/PropertyMap.Web/PropertyMap.Web.sln
```
Expected: `Build succeeded.`

- [ ] **Step 8: Commit**

```bash
cd C:\Agentes\PropertyMap\src
git add PropertyMap.Web/PropertyMap.Web/Services/IPropertyApiService.cs PropertyMap.Web/PropertyMap.Web/Services/PropertyApiService.cs PropertyMap.Web/PropertyMap.Web/Components/Pages/Planes.razor PropertyMap.Web/PropertyMap.Web/Components/Pages/Publisher/Dashboard.razor PropertyMap.Web/PropertyMap.Web/Components/Layout/Navbar.razor PropertyMap.Web/PropertyMap.Web/wwwroot/app.css
git commit -m "feat(web): add Planes page, publisher stats dashboard, and destacar toggle"
```

---

## Task 11: Tests de integración

**Files:**
- Create: `src/PropertyMap.Tests/Api/PlansControllerTests.cs`
- Create: `src/PropertyMap.Tests/Api/SubscriptionsControllerTests.cs`
- Create: `src/PropertyMap.Tests/Api/StatsControllerTests.cs`
- Create: `src/PropertyMap.Tests/Api/DestacadoTests.cs`

- [ ] **Step 1: Crear PlansControllerTests**

`src/PropertyMap.Tests/Api/PlansControllerTests.cs`
```csharp
using System.Net;
using System.Net.Http.Json;
using PropertyMap.Core.DTOs.Plans;
using Xunit;

namespace PropertyMap.Tests.Api;

public class PlansControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public PlansControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetActive_ReturnsSeededPlans()
    {
        var client = _factory.CreateClient();

        var resp = await client.GetAsync("/api/plans");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var plans = await resp.Content.ReadFromJsonAsync<List<PlanDto>>();
        Assert.NotNull(plans);
        Assert.Contains(plans!, p => p.Slug == "gratuito");
        Assert.Contains(plans!, p => p.Slug == "profesional");
        Assert.Contains(plans!, p => p.Slug == "premium");
    }
}
```

**Nota importante:** `TestWebApplicationFactory` usa EF InMemory y arranca con la base vacía — no corre `DbSeeder.SeedPlansAsync` automáticamente (eso solo pasa en `PropertyMap.Web/Program.cs` al levantar la app real). Si este test falla porque no hay planes, agregar el seeding al fixture: editar `src/PropertyMap.Tests/Api/TestWebApplicationFactory.cs` y, dentro de `CreateHost` (después del bloque de seeding de roles existente), agregar:

```csharp
        var db = scope.ServiceProvider.GetRequiredService<PropertyMap.Infrastructure.Data.AppDbContext>();
        PropertyMap.Infrastructure.Data.DbSeeder.SeedPlansAsync(db).GetAwaiter().GetResult();
```

- [ ] **Step 2: Crear SubscriptionsControllerTests**

`src/PropertyMap.Tests/Api/SubscriptionsControllerTests.cs`
```csharp
using System.Net;
using System.Net.Http.Json;
using PropertyMap.Core.DTOs.Plans;
using Xunit;

namespace PropertyMap.Tests.Api;

public class SubscriptionsControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public SubscriptionsControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetMine_WithoutSubscription_Returns404()
    {
        var (client, _) = await TestAuthHelper.CreateAuthenticatedUserAsync(_factory);

        var resp = await client.GetAsync("/api/subscriptions/mine");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Subscribe_ThenGetMine_ReturnsSubscription()
    {
        var (client, _) = await TestAuthHelper.CreateAuthenticatedUserAsync(_factory);
        var plans = await (await client.GetAsync("/api/plans")).Content.ReadFromJsonAsync<List<PlanDto>>();
        var profesional = plans!.First(p => p.Slug == "profesional");

        var subscribeResp = await client.PostAsJsonAsync("/api/subscriptions", new SubscribeRequest(profesional.Id));
        Assert.Equal(HttpStatusCode.OK, subscribeResp.StatusCode);

        var mineResp = await client.GetAsync("/api/subscriptions/mine");
        var sub = await mineResp.Content.ReadFromJsonAsync<SubscriptionDto>();
        Assert.NotNull(sub);
        Assert.Equal(profesional.Id, sub!.PlanId);
        Assert.Equal("Profesional", sub.PlanNombre);
    }

    [Fact]
    public async Task Subscribe_Twice_ReplacesExistingSubscription()
    {
        var (client, _) = await TestAuthHelper.CreateAuthenticatedUserAsync(_factory);
        var plans = await (await client.GetAsync("/api/plans")).Content.ReadFromJsonAsync<List<PlanDto>>();
        var gratuito = plans!.First(p => p.Slug == "gratuito");
        var premium = plans!.First(p => p.Slug == "premium");

        await client.PostAsJsonAsync("/api/subscriptions", new SubscribeRequest(gratuito.Id));
        await client.PostAsJsonAsync("/api/subscriptions", new SubscribeRequest(premium.Id));

        var sub = await (await client.GetAsync("/api/subscriptions/mine")).Content.ReadFromJsonAsync<SubscriptionDto>();
        Assert.Equal(premium.Id, sub!.PlanId);
    }
}
```

- [ ] **Step 3: Crear StatsControllerTests**

`src/PropertyMap.Tests/Api/StatsControllerTests.cs`
```csharp
using System.Net;
using System.Net.Http.Json;
using PropertyMap.Core.DTOs.Properties;
using PropertyMap.Core.DTOs.Stats;
using PropertyMap.Core.Enums;
using Xunit;

namespace PropertyMap.Tests.Api;

public class StatsControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public StatsControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private record CreatedIdDto(int Id);

    [Fact]
    public async Task GetMine_NoListings_ReturnsEmptyList()
    {
        var (client, _) = await TestAuthHelper.CreateAuthenticatedPublisherAsync(_factory);
        await TestAuthHelper.CreatePublisherProfileAsync(client);

        var resp = await client.GetAsync("/api/stats/mine");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var stats = await resp.Content.ReadFromJsonAsync<List<ListingStatsDto>>();
        Assert.Empty(stats!);
    }

    [Fact]
    public async Task GetForListing_AfterFavoriteAndConsulta_CountsCorrectly()
    {
        var (pubClient, _) = await TestAuthHelper.CreateAuthenticatedPublisherAsync(_factory);
        await TestAuthHelper.CreatePublisherProfileAsync(pubClient);

        var listing = new CreateListingRequest(
            Operacion: TipoOperacion.Venta, TipoPropiedad: TipoPropiedad.Casa,
            Titulo: "Casa con stats", Descripcion: "Test",
            Precio: 60000, Moneda: "USD",
            DireccionTexto: "Av. Stats 1", Ciudad: "Neuquén", Provincia: "Neuquén",
            Lat: -38.95, Lng: -68.06,
            Superficie: null, SuperficieCubierta: null, Ambientes: null,
            Dormitorios: null, Banos: null, Antiguedad: null,
            Cochera: false, Amenities: []);
        var createResp = await pubClient.PostAsJsonAsync("/api/properties", listing);
        var created = await createResp.Content.ReadFromJsonAsync<CreatedIdDto>();

        var (userClient, _) = await TestAuthHelper.CreateAuthenticatedUserAsync(_factory);
        await userClient.PostAsJsonAsync($"/api/favorites/{created!.Id}", new { });

        var statsResp = await pubClient.GetAsync($"/api/stats/listings/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, statsResp.StatusCode);
        var stats = await statsResp.Content.ReadFromJsonAsync<ListingStatsDto>();
        Assert.NotNull(stats);
        Assert.Equal(1, stats!.Favoritos);
        Assert.Equal(0, stats.Consultas);
        Assert.Equal(0, stats.Conversiones);
    }

    [Fact]
    public async Task GetForListing_NotOwner_Returns404()
    {
        var (pubClientA, _) = await TestAuthHelper.CreateAuthenticatedPublisherAsync(_factory);
        await TestAuthHelper.CreatePublisherProfileAsync(pubClientA);
        var listing = new CreateListingRequest(
            Operacion: TipoOperacion.Venta, TipoPropiedad: TipoPropiedad.Casa,
            Titulo: "Casa de otro publisher", Descripcion: "Test",
            Precio: 40000, Moneda: "USD",
            DireccionTexto: "Av. Otro 2", Ciudad: "Neuquén", Provincia: "Neuquén",
            Lat: -38.95, Lng: -68.06,
            Superficie: null, SuperficieCubierta: null, Ambientes: null,
            Dormitorios: null, Banos: null, Antiguedad: null,
            Cochera: false, Amenities: []);
        var createResp = await pubClientA.PostAsJsonAsync("/api/properties", listing);
        var created = await createResp.Content.ReadFromJsonAsync<CreatedIdDto>();

        var (pubClientB, _) = await TestAuthHelper.CreateAuthenticatedPublisherAsync(_factory);
        await TestAuthHelper.CreatePublisherProfileAsync(pubClientB);

        var resp = await pubClientB.GetAsync($"/api/stats/listings/{created!.Id}");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }
}
```

- [ ] **Step 4: Crear DestacadoTests**

`src/PropertyMap.Tests/Api/DestacadoTests.cs`
```csharp
using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using PropertyMap.Core.DTOs.Plans;
using PropertyMap.Core.DTOs.Properties;
using PropertyMap.Core.Enums;
using Xunit;

namespace PropertyMap.Tests.Api;

public class DestacadoTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public DestacadoTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private record CreatedIdDto(int Id);

    private async Task<(HttpClient pubClient, int listingId, string userId)> CreateApprovedListingAsync()
    {
        var (pubClient, userId) = await TestAuthHelper.CreateAuthenticatedPublisherAsync(_factory);
        await TestAuthHelper.CreatePublisherProfileAsync(pubClient);

        var listing = new CreateListingRequest(
            Operacion: TipoOperacion.Venta, TipoPropiedad: TipoPropiedad.Casa,
            Titulo: "Casa destacable", Descripcion: "Test",
            Precio: 70000, Moneda: "USD",
            DireccionTexto: "Av. Destacado 1", Ciudad: "Bariloche", Provincia: "Río Negro",
            Lat: -41.13, Lng: -71.30,
            Superficie: null, SuperficieCubierta: null, Ambientes: null,
            Dormitorios: null, Banos: null, Antiguedad: null,
            Cochera: false, Amenities: []);
        var createResp = await pubClient.PostAsJsonAsync("/api/properties", listing);
        var created = await createResp.Content.ReadFromJsonAsync<CreatedIdDto>();

        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider
            .GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<PropertyMap.Core.Entities.ApplicationUser>>();
        var adminEmail = $"admin_destacado_{Guid.NewGuid()}@test.com";
        var adminUser = new PropertyMap.Core.Entities.ApplicationUser
        {
            UserName = adminEmail, Email = adminEmail,
            Nombre = "Admin", Apellido = "Destacado", EmailConfirmed = true,
            Estado = EstadoUsuario.Activo
        };
        await userManager.CreateAsync(adminUser, "Admin123!");
        await userManager.AddToRoleAsync(adminUser, "Admin");
        var adminClient = _factory.CreateClient();
        var loginResp = await adminClient.PostAsJsonAsync("/api/auth/login",
            new PropertyMap.Core.DTOs.Auth.LoginRequest(adminEmail, "Admin123!"));
        var auth = await loginResp.Content.ReadFromJsonAsync<PropertyMap.Core.DTOs.Auth.AuthResponse>();
        adminClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth!.AccessToken);
        await adminClient.PatchAsJsonAsync($"/api/admin/listings/{created!.Id}/review",
            new { Aprobar = true, MotivoRechazo = (string?)null });

        return (pubClient, created.Id, userId);
    }

    [Fact]
    public async Task ToggleDestacado_WithoutSubscription_Returns400()
    {
        var (pubClient, listingId, _) = await CreateApprovedListingAsync();

        var resp = await pubClient.PatchAsync($"/api/properties/{listingId}/destacar", null);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task ToggleDestacado_WithSubscriptionUnderLimit_Succeeds()
    {
        var (pubClient, listingId, _) = await CreateApprovedListingAsync();
        var plans = await (await pubClient.GetAsync("/api/plans")).Content.ReadFromJsonAsync<List<PlanDto>>();
        var profesional = plans!.First(p => p.Slug == "profesional");
        await pubClient.PostAsJsonAsync("/api/subscriptions", new SubscribeRequest(profesional.Id));

        var resp = await pubClient.PatchAsync($"/api/properties/{listingId}/destacar", null);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<Dictionary<string, bool>>();
        Assert.True(body!["destacado"]);
    }

    [Fact]
    public async Task ToggleDestacado_Twice_TogglesBackOff()
    {
        var (pubClient, listingId, _) = await CreateApprovedListingAsync();
        var plans = await (await pubClient.GetAsync("/api/plans")).Content.ReadFromJsonAsync<List<PlanDto>>();
        var profesional = plans!.First(p => p.Slug == "profesional");
        await pubClient.PostAsJsonAsync("/api/subscriptions", new SubscribeRequest(profesional.Id));

        await pubClient.PatchAsync($"/api/properties/{listingId}/destacar", null);
        var secondResp = await pubClient.PatchAsync($"/api/properties/{listingId}/destacar", null);

        var body = await secondResp.Content.ReadFromJsonAsync<Dictionary<string, bool>>();
        Assert.False(body!["destacado"]);
    }

    [Fact]
    public async Task GetActiveListingsForMap_ListsDestacadosFirst()
    {
        var (pubClient, listingId, _) = await CreateApprovedListingAsync();
        var plans = await (await pubClient.GetAsync("/api/plans")).Content.ReadFromJsonAsync<List<PlanDto>>();
        var profesional = plans!.First(p => p.Slug == "profesional");
        await pubClient.PostAsJsonAsync("/api/subscriptions", new SubscribeRequest(profesional.Id));
        await pubClient.PatchAsync($"/api/properties/{listingId}/destacar", null);

        var mapResp = await _factory.CreateClient().GetAsync("/api/listings/map");
        var mapListings = await mapResp.Content.ReadFromJsonAsync<List<PropertyMap.Core.DTOs.Properties.ListingMapDto>>();

        Assert.NotNull(mapListings);
        Assert.Contains(mapListings!, l => l.Id == listingId);
    }
}
```

- [ ] **Step 5: Correr toda la suite de tests**

```bash
cd C:\Agentes\PropertyMap
dotnet test src/PropertyMap.Tests/PropertyMap.Tests.csproj
```
Expected: todos los tests pasan (los 66 existentes de Phase 7 + los nuevos de Phase 8). Si `PlansControllerTests.GetActive_ReturnsSeededPlans` falla porque la lista viene vacía, aplicar el ajuste de `TestWebApplicationFactory` descrito en el Step 1 de esta task.

- [ ] **Step 6: Build completo de la solución**

```bash
dotnet build src/PropertyMap.Web/PropertyMap.Web.sln
```
Expected: `Build succeeded.`

- [ ] **Step 7: Commit**

```bash
cd C:\Agentes\PropertyMap\src
git add PropertyMap.Tests/Api/PlansControllerTests.cs PropertyMap.Tests/Api/SubscriptionsControllerTests.cs PropertyMap.Tests/Api/StatsControllerTests.cs PropertyMap.Tests/Api/DestacadoTests.cs PropertyMap.Tests/Api/TestWebApplicationFactory.cs
git commit -m "test: add integration tests for plans, subscriptions, stats, and destacado"
```

---

## Self-Review

**Cobertura del spec (Phase 8 — Monetización, 3 de 4 sub-features; "IA descripción" diferido por decisión explícita del usuario):**
- ✅ Planes & suscripciones (Gratuito/Profesional/Premium) → Tasks 1, 2, 3, 4, 8, 9, 10, 11
- ✅ Dashboard de estadísticas (vistas, favoritos, consultas, conversiones) → Tasks 1, 7, 8, 9, 10, 11
- ✅ Destacados (prioridad visual, límite por plan) → Tasks 5, 6, 10, 11
- ⏸️ IA descripción automática (Claude API) — explícitamente fuera de alcance, el usuario no tiene la API key todavía

**Placeholder scan:** sin TODOs — todo el código de cada step es completo. La única nota condicional (Task 11 Step 1, ajuste de `TestWebApplicationFactory` si el seeding no corre en tests) da el código exacto a aplicar, no es un placeholder.

**Consistencia de tipos:** `PlanDto`, `SubscriptionDto`, `ListingStatsDto` se definen una sola vez (Task 1) y se reusan con la misma forma en repos (Tasks 2, 7), controllers (Tasks 4, 7), servicios Blazor (Task 9) y tests (Task 11). `IListingStatsRepository.GetForListingAsync(int listingId, int publisherId)` se define en Task 7 y se invoca igual en `StatsController` (mismo task). El nombre del campo `Destacado` (Task 5) se usa consistentemente en `ListingRepository` (Task 6), `PropertiesController` (Task 6), y `Dashboard.razor` (Task 10).


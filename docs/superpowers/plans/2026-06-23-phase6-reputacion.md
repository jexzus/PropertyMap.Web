# Phase 6 — Reputación Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implementar valoraciones de propiedades AlquilerTemporario y de agentes (con acceso via Consulta), más ranking automático de agentes on-the-fly y página pública `/top-agentes`.

**Architecture:** Las entidades `PropertyRating` y `AgentRating` ya existen en la DB con sus constraints. Se agregan dos repositorios, un controlador, un servicio Blazor y componentes UI. No hay migración nueva. El ranking se calcula on-the-fly en SQL/EF cada vez que se consulta.

**Tech Stack:** .NET 9, ASP.NET Core, EF Core 9 (InMemory en tests, SQL Server en prod), Blazor InteractiveServer, xUnit.

---

## Contexto para subagents

**Estructura del repo:** `C:\Agentes\PropertyMap\src\` es la raíz del repo git.

**Proyectos:**
- `PropertyMap.Core` — entidades, interfaces, DTOs, enums
- `PropertyMap.Infrastructure` — repositorios, AppDbContext, servicios
- `PropertyMap.Api` — controladores, Program.cs
- `PropertyMap.Web/PropertyMap.Web` — Blazor Server
- `PropertyMap.Tests` — tests de integración API (xUnit + WebApplicationFactory InMemory)

**Patrones establecidos (seguir siempre):**
- Repositorios: constructor `(AppDbContext ctx)`, métodos async, dos pasos si InMemory no soporta joins complejos
- Controladores: `[ApiController]`, `[Route("api/...")]`, inyección por constructor, `User.FindFirstValue(ClaimTypes.NameIdentifier)` para userId
- Tests: `IClassFixture<TestWebApplicationFactory>`, helpers `TestAuthHelper.CreateAuthenticatedPublisherAsync/UserAsync`, `SetupPublishedListingAsync` para crear listing aprobado
- Blazor services: `IHttpClientFactory.CreateClient("api")` + `MemoryTokenStore`, método `SetAuth()`, try/catch retorna null/[]
- Blazor pages: `@rendermode InteractiveServer`, `OnAfterRenderAsync(firstRender)` → `TryRestoreSessionAsync()` → cargar datos → `StateHasChanged()`

**Comandos de test:**
```bash
dotnet test PropertyMap.Tests/PropertyMap.Tests.csproj -v n
```

---

## File Map

**Creados:**
```
PropertyMap.Core/DTOs/Ratings/RatingDtos.cs
PropertyMap.Core/Interfaces/IPropertyRatingRepository.cs
PropertyMap.Core/Interfaces/IAgentRatingRepository.cs
PropertyMap.Infrastructure/Repositories/PropertyRatingRepository.cs
PropertyMap.Infrastructure/Repositories/AgentRatingRepository.cs
PropertyMap.Api/Controllers/RatingsController.cs
PropertyMap.Tests/Api/RatingsControllerTests.cs
PropertyMap.Web/PropertyMap.Web/Services/IRatingsApiService.cs
PropertyMap.Web/PropertyMap.Web/Services/RatingsApiService.cs
PropertyMap.Web/PropertyMap.Web/Components/Shared/RatingStars.razor
PropertyMap.Web/PropertyMap.Web/Components/Shared/PropertyRatingForm.razor
PropertyMap.Web/PropertyMap.Web/Components/Shared/AgentRatingForm.razor
PropertyMap.Web/PropertyMap.Web/Components/Pages/TopAgentes.razor
```

**Modificados:**
```
PropertyMap.Core/DTOs/Consultas/ConsultaDtos.cs              (+ OperacionPropiedad, PublisherId en ConsultaDetailDto)
PropertyMap.Infrastructure/Repositories/ConsultaRepository.cs (+ nuevos campos en GetByIdAsync)
PropertyMap.Api/Program.cs                                    (+ 2 AddScoped)
PropertyMap.Web/PropertyMap.Web/Program.cs                   (+ 1 AddScoped)
PropertyMap.Web/PropertyMap.Web/Components/Pages/Account/ConsultaDetalle.razor  (+ formularios rating)
PropertyMap.Web/PropertyMap.Web/Components/Pages/PropertyDetail.razor  (+ stats)
PropertyMap.Web/PropertyMap.Web/Components/Layout/Navbar.razor  (+ Top Agentes)
PropertyMap.Web/PropertyMap.Web/wwwroot/css/app.css           (+ estilos rating)
```

---

## Task 1: DTOs + extensión de ConsultaDetailDto

**Files:**
- Create: `PropertyMap.Core/DTOs/Ratings/RatingDtos.cs`
- Modify: `PropertyMap.Core/DTOs/Consultas/ConsultaDtos.cs`
- Modify: `PropertyMap.Infrastructure/Repositories/ConsultaRepository.cs`

- [ ] **Step 1: Crear directorio y archivo RatingDtos.cs**

```csharp
// PropertyMap.Core/DTOs/Ratings/RatingDtos.cs
using System.ComponentModel.DataAnnotations;

namespace PropertyMap.Core.DTOs.Ratings;

public record RatePropertyRequest(
    int ListingId,
    [Range(1, 5)] int PuntajeUbicacion,
    [Range(1, 5)] int PuntajeEstado,
    [Range(1, 5)] int PuntajePrecioCalidad,
    string? Comentario);

public record RateAgentRequest(
    int PublisherId,
    [Range(1, 5)] int PuntajeAtencion,
    [Range(1, 5)] int PuntajeRapidez,
    [Range(1, 5)] int PuntajeTransparencia,
    [Range(1, 5)] int PuntajeProfesionalismo,
    string? Comentario);

public record PropertyRatingStatsDto(
    double PromedioUbicacion,
    double PromedioEstado,
    double PrecioCal,
    double PromedioGeneral,
    int TotalValoraciones);

public record AgentRatingStatsDto(
    double PromedioAtencion,
    double PromedioRapidez,
    double PromedioTransparencia,
    double PromedioProfesionalismo,
    double PromedioGeneral,
    int TotalValoraciones);

public record AgentRankingItemDto(
    int PublisherId,
    string Nombre,
    string Tipo,
    string? LogoUrl,
    double RankingScore,
    double RatingPromedio,
    double TiempoRespuestaHoras,
    int Operaciones,
    double AntiguedadAnios);
```

- [ ] **Step 2: Extender ConsultaDetailDto con OperacionPropiedad y PublisherId**

En `PropertyMap.Core/DTOs/Consultas/ConsultaDtos.cs`, reemplazar la definición de `ConsultaDetailDto`:

```csharp
// Antes:
public record ConsultaDetailDto(
    int Id,
    int PropertyListingId,
    string PropertyTitulo,
    List<ConsultaMensajeDto> Mensajes);

// Después:
public record ConsultaDetailDto(
    int Id,
    int PropertyListingId,
    string PropertyTitulo,
    string OperacionPropiedad,
    int? PublisherId,
    List<ConsultaMensajeDto> Mensajes);
```

- [ ] **Step 3: Actualizar ConsultaRepository.GetByIdAsync para incluir los nuevos campos**

En `PropertyMap.Infrastructure/Repositories/ConsultaRepository.cs`, el método `GetByIdAsync` construye el `ConsultaDetailDto`. Actualizar la construcción del record (alrededor de la línea 55):

```csharp
return new ConsultaDetailDto(
    consulta.Id,
    consulta.PropertyListingId,
    consulta.PropertyListing.Titulo,
    consulta.PropertyListing.Operacion.ToString(),
    consulta.PropertyListing.Publisher?.Id,
    consulta.Mensajes
        .OrderBy(m => m.FechaEnvio)
        .Select(m => new ConsultaMensajeDto(
            m.Id,
            $"{m.Sender.Nombre} {m.Sender.Apellido}",
            m.EsDelPublisher,
            m.Mensaje,
            m.FechaEnvio))
        .ToList());
```

- [ ] **Step 4: Compilar y verificar que el proyecto buildea sin errores**

```bash
dotnet build PropertyMap.Core/PropertyMap.Core.csproj
dotnet build PropertyMap.Infrastructure/PropertyMap.Infrastructure.csproj
dotnet build PropertyMap.Tests/PropertyMap.Tests.csproj
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 5: Correr todos los tests para asegurar que no hay regresiones**

```bash
dotnet test PropertyMap.Tests/PropertyMap.Tests.csproj -v n
```

Expected: 50 passed.

- [ ] **Step 6: Commit**

```bash
git add PropertyMap.Core/DTOs/Ratings/RatingDtos.cs \
        PropertyMap.Core/DTOs/Consultas/ConsultaDtos.cs \
        PropertyMap.Infrastructure/Repositories/ConsultaRepository.cs
git commit -m "feat: Phase 6 DTOs + extend ConsultaDetailDto with OperacionPropiedad and PublisherId"
```

---

## Task 2: IPropertyRatingRepository + PropertyRatingRepository

**Files:**
- Create: `PropertyMap.Core/Interfaces/IPropertyRatingRepository.cs`
- Create: `PropertyMap.Infrastructure/Repositories/PropertyRatingRepository.cs`
- Modify: `PropertyMap.Api/Program.cs`

- [ ] **Step 1: Crear IPropertyRatingRepository**

```csharp
// PropertyMap.Core/Interfaces/IPropertyRatingRepository.cs
using PropertyMap.Core.DTOs.Ratings;
using PropertyMap.Core.Entities;

namespace PropertyMap.Core.Interfaces;

public interface IPropertyRatingRepository
{
    Task<bool> HasConsultaAsync(int listingId, string userId);
    Task<PropertyRating?> GetByUserAndListingAsync(int listingId, string userId);
    Task AddOrUpdateAsync(PropertyRating rating);
    Task<PropertyRatingStatsDto> GetStatsAsync(int listingId);
}
```

- [ ] **Step 2: Crear PropertyRatingRepository**

```csharp
// PropertyMap.Infrastructure/Repositories/PropertyRatingRepository.cs
using Microsoft.EntityFrameworkCore;
using PropertyMap.Core.DTOs.Ratings;
using PropertyMap.Core.Entities;
using PropertyMap.Core.Interfaces;
using PropertyMap.Infrastructure.Data;

namespace PropertyMap.Infrastructure.Repositories;

public class PropertyRatingRepository : IPropertyRatingRepository
{
    private readonly AppDbContext _ctx;

    public PropertyRatingRepository(AppDbContext ctx)
    {
        _ctx = ctx;
    }

    public async Task<bool> HasConsultaAsync(int listingId, string userId)
    {
        return await _ctx.Consultas
            .AnyAsync(c => c.PropertyListingId == listingId && c.UserId == userId);
    }

    public async Task<PropertyRating?> GetByUserAndListingAsync(int listingId, string userId)
    {
        return await _ctx.PropertyRatings
            .FirstOrDefaultAsync(r => r.PropertyListingId == listingId && r.UserId == userId);
    }

    public async Task AddOrUpdateAsync(PropertyRating rating)
    {
        var existing = await _ctx.PropertyRatings
            .FirstOrDefaultAsync(r => r.UserId == rating.UserId && r.PropertyListingId == rating.PropertyListingId);

        if (existing is null)
        {
            _ctx.PropertyRatings.Add(rating);
        }
        else
        {
            existing.PuntajeUbicacion = rating.PuntajeUbicacion;
            existing.PuntajeEstado = rating.PuntajeEstado;
            existing.PuntajePrecioCalidad = rating.PuntajePrecioCalidad;
            existing.Comentario = rating.Comentario;
            existing.FechaValoracion = rating.FechaValoracion;
        }

        await _ctx.SaveChangesAsync();
    }

    public async Task<PropertyRatingStatsDto> GetStatsAsync(int listingId)
    {
        var ratings = await _ctx.PropertyRatings
            .Where(r => r.PropertyListingId == listingId)
            .ToListAsync();

        if (ratings.Count == 0)
            return new PropertyRatingStatsDto(0, 0, 0, 0, 0);

        var promedioUbicacion = ratings.Average(r => (double)r.PuntajeUbicacion);
        var promedioEstado    = ratings.Average(r => (double)r.PuntajeEstado);
        var promedioPrecio    = ratings.Average(r => (double)r.PuntajePrecioCalidad);
        var promedioGeneral   = (promedioUbicacion + promedioEstado + promedioPrecio) / 3.0;

        return new PropertyRatingStatsDto(
            Math.Round(promedioUbicacion, 2),
            Math.Round(promedioEstado, 2),
            Math.Round(promedioPrecio, 2),
            Math.Round(promedioGeneral, 2),
            ratings.Count);
    }
}
```

- [ ] **Step 3: Registrar en PropertyMap.Api/Program.cs**

Agregar después de la línea `builder.Services.AddScoped<IConsultaRepository, ConsultaRepository>();` (línea ~95):

```csharp
builder.Services.AddScoped<IPropertyRatingRepository, PropertyRatingRepository>();
```

- [ ] **Step 4: Verificar build**

```bash
dotnet build PropertyMap.Api/PropertyMap.Api.csproj
```

Expected: Build succeeded.

- [ ] **Step 5: Commit**

```bash
git add PropertyMap.Core/Interfaces/IPropertyRatingRepository.cs \
        PropertyMap.Infrastructure/Repositories/PropertyRatingRepository.cs \
        PropertyMap.Api/Program.cs
git commit -m "feat: IPropertyRatingRepository + PropertyRatingRepository"
```

---

## Task 3: IAgentRatingRepository + AgentRatingRepository

**Files:**
- Create: `PropertyMap.Core/Interfaces/IAgentRatingRepository.cs`
- Create: `PropertyMap.Infrastructure/Repositories/AgentRatingRepository.cs`
- Modify: `PropertyMap.Api/Program.cs`

- [ ] **Step 1: Crear IAgentRatingRepository**

```csharp
// PropertyMap.Core/Interfaces/IAgentRatingRepository.cs
using PropertyMap.Core.DTOs.Ratings;
using PropertyMap.Core.Entities;

namespace PropertyMap.Core.Interfaces;

public interface IAgentRatingRepository
{
    Task<bool> HasConsultaWithPublisherAsync(int publisherId, string userId);
    Task<AgentRating?> GetByUserAndPublisherAsync(int publisherId, string userId);
    Task AddOrUpdateAsync(AgentRating rating);
    Task<AgentRatingStatsDto> GetStatsAsync(int publisherId);
    Task<List<AgentRankingItemDto>> GetRankingAsync(string? ciudad, int top = 20);
}
```

- [ ] **Step 2: Crear AgentRatingRepository**

```csharp
// PropertyMap.Infrastructure/Repositories/AgentRatingRepository.cs
using Microsoft.EntityFrameworkCore;
using PropertyMap.Core.DTOs.Ratings;
using PropertyMap.Core.Entities;
using PropertyMap.Core.Enums;
using PropertyMap.Core.Interfaces;
using PropertyMap.Infrastructure.Data;

namespace PropertyMap.Infrastructure.Repositories;

public class AgentRatingRepository : IAgentRatingRepository
{
    private readonly AppDbContext _ctx;

    public AgentRatingRepository(AppDbContext ctx)
    {
        _ctx = ctx;
    }

    public async Task<bool> HasConsultaWithPublisherAsync(int publisherId, string userId)
    {
        // Two-step: materialize listing IDs first (InMemory compatibility)
        var publisherListingIds = await _ctx.PropertyListings
            .Where(l => l.PublisherId == publisherId)
            .Select(l => l.Id)
            .ToListAsync();

        if (publisherListingIds.Count == 0) return false;

        return await _ctx.Consultas
            .AnyAsync(c => c.UserId == userId && publisherListingIds.Contains(c.PropertyListingId));
    }

    public async Task<AgentRating?> GetByUserAndPublisherAsync(int publisherId, string userId)
    {
        return await _ctx.AgentRatings
            .FirstOrDefaultAsync(r => r.PublisherId == publisherId && r.UserId == userId);
    }

    public async Task AddOrUpdateAsync(AgentRating rating)
    {
        var existing = await _ctx.AgentRatings
            .FirstOrDefaultAsync(r => r.UserId == rating.UserId && r.PublisherId == rating.PublisherId);

        if (existing is null)
        {
            _ctx.AgentRatings.Add(rating);
        }
        else
        {
            existing.PuntajeAtencion       = rating.PuntajeAtencion;
            existing.PuntajeRapidez        = rating.PuntajeRapidez;
            existing.PuntajeTransparencia  = rating.PuntajeTransparencia;
            existing.PuntajeProfesionalismo = rating.PuntajeProfesionalismo;
            existing.Comentario            = rating.Comentario;
            existing.FechaValoracion       = rating.FechaValoracion;
        }

        await _ctx.SaveChangesAsync();
    }

    public async Task<AgentRatingStatsDto> GetStatsAsync(int publisherId)
    {
        var ratings = await _ctx.AgentRatings
            .Where(r => r.PublisherId == publisherId)
            .ToListAsync();

        if (ratings.Count == 0)
            return new AgentRatingStatsDto(0, 0, 0, 0, 0, 0);

        var promedioAtencion        = ratings.Average(r => (double)r.PuntajeAtencion);
        var promedioRapidez         = ratings.Average(r => (double)r.PuntajeRapidez);
        var promedioTransparencia   = ratings.Average(r => (double)r.PuntajeTransparencia);
        var promedioProfesionalismo = ratings.Average(r => (double)r.PuntajeProfesionalismo);
        var promedioGeneral = (promedioAtencion + promedioRapidez + promedioTransparencia + promedioProfesionalismo) / 4.0;

        return new AgentRatingStatsDto(
            Math.Round(promedioAtencion, 2),
            Math.Round(promedioRapidez, 2),
            Math.Round(promedioTransparencia, 2),
            Math.Round(promedioProfesionalismo, 2),
            Math.Round(promedioGeneral, 2),
            ratings.Count);
    }

    public async Task<List<AgentRankingItemDto>> GetRankingAsync(string? ciudad, int top = 20)
    {
        // Step 1: publishers with at least one rating, optionally filtered by city
        var publisherIdsWithRatings = await _ctx.AgentRatings
            .Select(r => r.PublisherId)
            .Distinct()
            .ToListAsync();

        if (publisherIdsWithRatings.Count == 0) return [];

        IList<int> publisherIds = publisherIdsWithRatings;

        if (!string.IsNullOrWhiteSpace(ciudad))
        {
            var pubIdsInCity = await _ctx.PropertyListings
                .Where(l => l.Ciudad == ciudad && publisherIdsWithRatings.Contains(l.PublisherId))
                .Select(l => l.PublisherId)
                .Distinct()
                .ToListAsync();
            publisherIds = pubIdsInCity;
        }

        if (publisherIds.Count == 0) return [];

        // Step 2: Materialize publishers with ratings
        var publishers = await _ctx.Publishers
            .Where(p => publisherIds.Contains(p.Id))
            .Include(p => p.Ratings)
            .Include(p => p.Listings)
            .Include(p => p.User)
            .ToListAsync();

        // Step 3: Listing IDs per publisher (for response time calc)
        var listingEntries = await _ctx.PropertyListings
            .Where(l => publisherIds.Contains(l.PublisherId))
            .Select(l => new { l.Id, l.PublisherId })
            .ToListAsync();

        var listingToPublisher = listingEntries.ToDictionary(l => l.Id, l => l.PublisherId);
        var allListingIds = listingEntries.Select(l => l.Id).ToList();

        // Step 4: Consulta IDs for those listings
        var consultaEntries = await _ctx.Consultas
            .Where(c => allListingIds.Contains(c.PropertyListingId))
            .Select(c => new { c.Id, c.PropertyListingId })
            .ToListAsync();

        var allConsultaIds = consultaEntries.Select(c => c.Id).ToList();

        // Step 5: Messages in those consultas
        List<(int ConsultaId, bool EsDelPublisher, DateTime FechaEnvio)> messages = [];
        if (allConsultaIds.Count > 0)
        {
            messages = (await _ctx.ConsultaMensajes
                .Where(m => allConsultaIds.Contains(m.ConsultaId))
                .OrderBy(m => m.ConsultaId).ThenBy(m => m.FechaEnvio)
                .Select(m => new { m.ConsultaId, m.EsDelPublisher, m.FechaEnvio })
                .ToListAsync())
                .Select(m => (m.ConsultaId, m.EsDelPublisher, m.FechaEnvio))
                .ToList();
        }

        // Step 6: Calculate average response time per publisher (in hours)
        var responseTimesByPublisher = new Dictionary<int, List<double>>();
        var messagesByConsulta = messages.GroupBy(m => m.ConsultaId)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var (consultaId, msgs) in messagesByConsulta)
        {
            var consultaEntry = consultaEntries.FirstOrDefault(c => c.Id == consultaId);
            if (consultaEntry is null) continue;

            if (!listingToPublisher.TryGetValue(consultaEntry.PropertyListingId, out var pubId)) continue;

            if (!responseTimesByPublisher.ContainsKey(pubId))
                responseTimesByPublisher[pubId] = [];

            for (int i = 0; i < msgs.Count - 1; i++)
            {
                if (!msgs[i].EsDelPublisher && msgs[i + 1].EsDelPublisher)
                {
                    var hours = (msgs[i + 1].FechaEnvio - msgs[i].FechaEnvio).TotalHours;
                    responseTimesByPublisher[pubId].Add(hours);
                }
            }
        }

        // Step 7: Calculate scores
        var now = DateTime.UtcNow;

        var result = publishers.Select(p =>
        {
            var ratings = p.Ratings.ToList();
            var ratingAvg = ratings.Count > 0
                ? ratings.Average(r => (r.PuntajeAtencion + r.PuntajeRapidez + r.PuntajeTransparencia + r.PuntajeProfesionalismo) / 4.0)
                : 1.0;

            var ratingScore = (ratingAvg - 1) / 4.0 * 100;

            var avgHours = responseTimesByPublisher.TryGetValue(p.Id, out var times) && times.Count > 0
                ? times.Average()
                : 72.0;
            var responseScore = Math.Max(0, (72 - avgHours) / 72.0 * 100);

            var operaciones = p.Listings.Count(l => l.Estado == EstadoPublicacion.Vendida || l.Estado == EstadoPublicacion.Alquilada);
            var operacionesScore = Math.Min(100, operaciones / 50.0 * 100);

            var anios = p.User is not null ? (now - p.User.FechaRegistro).TotalDays / 365.25 : 0;
            var antiguedadScore = Math.Min(100, anios / 5.0 * 100);

            var rankingScore = 0.40 * ratingScore + 0.30 * responseScore + 0.20 * operacionesScore + 0.10 * antiguedadScore;

            var tiempoRespHoras = responseTimesByPublisher.ContainsKey(p.Id) && responseTimesByPublisher[p.Id].Count > 0
                ? responseTimesByPublisher[p.Id].Average()
                : 0;

            return new AgentRankingItemDto(
                p.Id,
                p.Nombre,
                p.Tipo.ToString(),
                p.LogoUrl,
                Math.Round(rankingScore, 2),
                Math.Round(ratingAvg, 2),
                Math.Round(tiempoRespHoras, 2),
                operaciones,
                Math.Round(anios, 1));
        })
        .OrderByDescending(x => x.RankingScore)
        .Take(top)
        .ToList();

        return result;
    }
}
```

- [ ] **Step 3: Registrar en PropertyMap.Api/Program.cs**

Agregar junto a la línea de `IPropertyRatingRepository`:

```csharp
builder.Services.AddScoped<IAgentRatingRepository, AgentRatingRepository>();
```

- [ ] **Step 4: Verificar build**

```bash
dotnet build PropertyMap.Api/PropertyMap.Api.csproj
```

Expected: Build succeeded.

- [ ] **Step 5: Commit**

```bash
git add PropertyMap.Core/Interfaces/IAgentRatingRepository.cs \
        PropertyMap.Infrastructure/Repositories/AgentRatingRepository.cs \
        PropertyMap.Api/Program.cs
git commit -m "feat: IAgentRatingRepository + AgentRatingRepository with on-the-fly ranking"
```

---

## Task 4: RatingsController + Tests (TDD)

**Files:**
- Create: `PropertyMap.Api/Controllers/RatingsController.cs` (stub primero)
- Create: `PropertyMap.Tests/Api/RatingsControllerTests.cs`

### Paso A — Stub controller

- [ ] **Step 1: Crear stub de RatingsController**

```csharp
// PropertyMap.Api/Controllers/RatingsController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace PropertyMap.Api.Controllers;

[ApiController]
[Route("api/ratings")]
public class RatingsController : ControllerBase
{
    [HttpPost("property")]
    [Authorize]
    public IActionResult RateProperty() => StatusCode(501);

    [HttpGet("property/{listingId:int}/stats")]
    public IActionResult GetPropertyStats(int listingId) => StatusCode(501);

    [HttpPost("agent")]
    [Authorize]
    public IActionResult RateAgent() => StatusCode(501);

    [HttpGet("agent/{publisherId:int}/stats")]
    public IActionResult GetAgentStats(int publisherId) => StatusCode(501);

    [HttpGet("ranking")]
    public IActionResult GetRanking() => StatusCode(501);
}
```

### Paso B — Tests

- [ ] **Step 2: Crear RatingsControllerTests.cs con 8 tests**

```csharp
// PropertyMap.Tests/Api/RatingsControllerTests.cs
using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using PropertyMap.Core.DTOs.Ratings;
using PropertyMap.Core.DTOs.Properties;
using PropertyMap.Core.DTOs.Consultas;
using PropertyMap.Core.Enums;
using Xunit;

namespace PropertyMap.Tests.Api;

public class RatingsControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public RatingsControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private record CreatedIdDto(int Id);

    private static CreateListingRequest AlquilerTemporarioListing() => new(
        Operacion: TipoOperacion.AlquilerTemporario,
        TipoPropiedad: TipoPropiedad.Departamento,
        Titulo: "Alquiler Temporario Test",
        Descripcion: "Test",
        Precio: 5000,
        Moneda: "ARS",
        DireccionTexto: "Av. Rating 123",
        Ciudad: "Córdoba",
        Provincia: "Córdoba",
        Lat: -31.42,
        Lng: -64.18,
        Superficie: null, SuperficieCubierta: null, Ambientes: null,
        Dormitorios: null, Banos: null, Antiguedad: null,
        Cochera: false, Amenities: []);

    private static CreateListingRequest VentaListing() => new(
        Operacion: TipoOperacion.Venta,
        TipoPropiedad: TipoPropiedad.Departamento,
        Titulo: "Venta Test",
        Descripcion: "Test",
        Precio: 90000,
        Moneda: "USD",
        DireccionTexto: "Av. Venta 456",
        Ciudad: "Buenos Aires",
        Provincia: "Buenos Aires",
        Lat: -34.60, Lng: -58.38,
        Superficie: null, SuperficieCubierta: null, Ambientes: null,
        Dormitorios: null, Banos: null, Antiguedad: null,
        Cochera: false, Amenities: []);

    private async Task<(HttpClient pubClient, int listingId, int publisherId)> SetupApprovedListingAsync(CreateListingRequest listing)
    {
        var (pubClient, _) = await TestAuthHelper.CreateAuthenticatedPublisherAsync(_factory);
        var publisherId = await TestAuthHelper.CreatePublisherProfileAsync(pubClient);
        var createResp = await pubClient.PostAsJsonAsync("/api/properties", listing);
        var created = await createResp.Content.ReadFromJsonAsync<CreatedIdDto>();

        using var adminScope = _factory.Services.CreateScope();
        var adminMgr = adminScope.ServiceProvider
            .GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<PropertyMap.Core.Entities.ApplicationUser>>();
        var adminEmail = $"admin_rating_{Guid.NewGuid()}@test.com";
        var adminUser = new PropertyMap.Core.Entities.ApplicationUser
        {
            UserName = adminEmail, Email = adminEmail,
            Nombre = "Admin", Apellido = "Rating",
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

        return (pubClient, created.Id, publisherId);
    }

    private async Task<(HttpClient userClient, int consultaId)> CreateConsultaAsync(int listingId)
    {
        var (userClient, _) = await TestAuthHelper.CreateAuthenticatedUserAsync(_factory);
        var resp = await userClient.PostAsJsonAsync("/api/consultas",
            new CreateConsultaRequest(listingId, "Hola, consulta de test"));
        var detail = await resp.Content.ReadFromJsonAsync<ConsultaDetailDto>();
        return (userClient, detail!.Id);
    }

    [Fact]
    public async Task PostPropertyRating_WithConsulta_AlquilerTemporario_ReturnsStats()
    {
        var (_, listingId, _) = await SetupApprovedListingAsync(AlquilerTemporarioListing());
        var (userClient, _) = await CreateConsultaAsync(listingId);

        var resp = await userClient.PostAsJsonAsync("/api/ratings/property",
            new RatePropertyRequest(listingId, 5, 4, 3, "Excelente"));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var stats = await resp.Content.ReadFromJsonAsync<PropertyRatingStatsDto>();
        Assert.NotNull(stats);
        Assert.Equal(1, stats!.TotalValoraciones);
        Assert.Equal(5.0, stats.PromedioUbicacion);
        Assert.Equal(4.0, stats.PromedioEstado);
        Assert.Equal(3.0, stats.PrecioCal);
        Assert.Equal(4.0, stats.PromedioGeneral, 1);
    }

    [Fact]
    public async Task PostPropertyRating_NotAlquilerTemporario_Returns400()
    {
        var (_, listingId, _) = await SetupApprovedListingAsync(VentaListing());
        var (userClient, _) = await CreateConsultaAsync(listingId);

        var resp = await userClient.PostAsJsonAsync("/api/ratings/property",
            new RatePropertyRequest(listingId, 5, 5, 5, null));

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task PostPropertyRating_NoConsulta_Returns403()
    {
        var (_, listingId, _) = await SetupApprovedListingAsync(AlquilerTemporarioListing());
        // User with NO consulta for this listing
        var (userClient, _) = await TestAuthHelper.CreateAuthenticatedUserAsync(_factory);

        var resp = await userClient.PostAsJsonAsync("/api/ratings/property",
            new RatePropertyRequest(listingId, 5, 5, 5, null));

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task PostPropertyRating_SecondTime_UpdatesRating()
    {
        var (_, listingId, _) = await SetupApprovedListingAsync(AlquilerTemporarioListing());
        var (userClient, _) = await CreateConsultaAsync(listingId);

        await userClient.PostAsJsonAsync("/api/ratings/property",
            new RatePropertyRequest(listingId, 1, 1, 1, null));
        var resp2 = await userClient.PostAsJsonAsync("/api/ratings/property",
            new RatePropertyRequest(listingId, 5, 5, 5, null));

        Assert.Equal(HttpStatusCode.OK, resp2.StatusCode);
        var stats = await resp2.Content.ReadFromJsonAsync<PropertyRatingStatsDto>();
        Assert.Equal(1, stats!.TotalValoraciones); // still 1 — upsert
        Assert.Equal(5.0, stats.PromedioGeneral, 1);
    }

    [Fact]
    public async Task GetPropertyStats_ReturnsAverages()
    {
        var (_, listingId, _) = await SetupApprovedListingAsync(AlquilerTemporarioListing());
        var (userClient, _) = await CreateConsultaAsync(listingId);
        await userClient.PostAsJsonAsync("/api/ratings/property",
            new RatePropertyRequest(listingId, 4, 3, 5, null));

        var resp = await _factory.CreateClient().GetAsync($"/api/ratings/property/{listingId}/stats");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var stats = await resp.Content.ReadFromJsonAsync<PropertyRatingStatsDto>();
        Assert.NotNull(stats);
        Assert.Equal(1, stats!.TotalValoraciones);
        Assert.InRange(stats.PromedioGeneral, 3.9, 4.1); // (4+3+5)/3 = 4.0
    }

    [Fact]
    public async Task PostAgentRating_WithConsulta_ReturnsStats()
    {
        var (_, listingId, publisherId) = await SetupApprovedListingAsync(AlquilerTemporarioListing());
        var (userClient, _) = await CreateConsultaAsync(listingId);

        var resp = await userClient.PostAsJsonAsync("/api/ratings/agent",
            new RateAgentRequest(publisherId, 5, 5, 4, 3, "Muy bueno"));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var stats = await resp.Content.ReadFromJsonAsync<AgentRatingStatsDto>();
        Assert.NotNull(stats);
        Assert.Equal(1, stats!.TotalValoraciones);
        Assert.Equal(5.0, stats.PromedioAtencion);
        Assert.InRange(stats.PromedioGeneral, 4.2, 4.3); // (5+5+4+3)/4 = 4.25
    }

    [Fact]
    public async Task PostAgentRating_NoConsulta_Returns403()
    {
        var (_, listingId, publisherId) = await SetupApprovedListingAsync(AlquilerTemporarioListing());
        // User with no consulta to this publisher
        var (userClient, _) = await TestAuthHelper.CreateAuthenticatedUserAsync(_factory);

        var resp = await userClient.PostAsJsonAsync("/api/ratings/agent",
            new RateAgentRequest(publisherId, 5, 5, 5, 5, null));

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task GetRanking_AfterAgentRating_ReturnsPublisher()
    {
        var (_, listingId, publisherId) = await SetupApprovedListingAsync(AlquilerTemporarioListing());
        var (userClient, _) = await CreateConsultaAsync(listingId);
        await userClient.PostAsJsonAsync("/api/ratings/agent",
            new RateAgentRequest(publisherId, 5, 5, 5, 5, null));

        var resp = await _factory.CreateClient().GetAsync("/api/ratings/ranking");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var ranking = await resp.Content.ReadFromJsonAsync<List<AgentRankingItemDto>>();
        Assert.NotNull(ranking);
        Assert.Contains(ranking!, r => r.PublisherId == publisherId);
    }
}
```

- [ ] **Step 3: Correr tests para verificar que FALLAN (stub devuelve 501)**

```bash
dotnet test PropertyMap.Tests/PropertyMap.Tests.csproj --filter "RatingsControllerTests" -v n
```

Expected: 8 tests FAIL con error 501 o similar.

### Paso C — Implementación completa del controller

- [ ] **Step 4: Reemplazar el stub con la implementación completa de RatingsController**

```csharp
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

    // POST /api/ratings/property
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
            PropertyListingId  = request.ListingId,
            UserId             = userId,
            PuntajeUbicacion   = request.PuntajeUbicacion,
            PuntajeEstado      = request.PuntajeEstado,
            PuntajePrecioCalidad = request.PuntajePrecioCalidad,
            Comentario         = request.Comentario,
            FechaValoracion    = DateTime.UtcNow
        });

        return Ok(await _propertyRatings.GetStatsAsync(request.ListingId));
    }

    // GET /api/ratings/property/{listingId}/stats
    [HttpGet("property/{listingId:int}/stats")]
    public async Task<IActionResult> GetPropertyStats(int listingId)
    {
        return Ok(await _propertyRatings.GetStatsAsync(listingId));
    }

    // POST /api/ratings/agent
    [HttpPost("agent")]
    [Authorize]
    public async Task<IActionResult> RateAgent([FromBody] RateAgentRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        if (!await _agentRatings.HasConsultaWithPublisherAsync(request.PublisherId, userId))
            return Forbid();

        await _agentRatings.AddOrUpdateAsync(new AgentRating
        {
            PublisherId             = request.PublisherId,
            UserId                  = userId,
            PuntajeAtencion         = request.PuntajeAtencion,
            PuntajeRapidez          = request.PuntajeRapidez,
            PuntajeTransparencia    = request.PuntajeTransparencia,
            PuntajeProfesionalismo  = request.PuntajeProfesionalismo,
            Comentario              = request.Comentario,
            FechaValoracion         = DateTime.UtcNow
        });

        return Ok(await _agentRatings.GetStatsAsync(request.PublisherId));
    }

    // GET /api/ratings/agent/{publisherId}/stats
    [HttpGet("agent/{publisherId:int}/stats")]
    public async Task<IActionResult> GetAgentStats(int publisherId)
    {
        return Ok(await _agentRatings.GetStatsAsync(publisherId));
    }

    // GET /api/ratings/ranking?ciudad=&top=20
    [HttpGet("ranking")]
    public async Task<IActionResult> GetRanking(
        [FromQuery] string? ciudad = null,
        [FromQuery] int top = 20)
    {
        if (top < 1 || top > 100) top = 20;
        return Ok(await _agentRatings.GetRankingAsync(ciudad, top));
    }
}
```

- [ ] **Step 5: Correr tests para verificar que PASAN**

```bash
dotnet test PropertyMap.Tests/PropertyMap.Tests.csproj -v n
```

Expected: 58 passed (50 existentes + 8 nuevos), 0 failures.

- [ ] **Step 6: Commit**

```bash
git add PropertyMap.Api/Controllers/RatingsController.cs \
        PropertyMap.Tests/Api/RatingsControllerTests.cs
git commit -m "feat: RatingsController (5 endpoints) + 8 integration tests (TDD)"
```

---

## Task 5: IRatingsApiService + RatingsApiService + Web Program.cs

**Files:**
- Create: `PropertyMap.Web/PropertyMap.Web/Services/IRatingsApiService.cs`
- Create: `PropertyMap.Web/PropertyMap.Web/Services/RatingsApiService.cs`
- Modify: `PropertyMap.Web/PropertyMap.Web/Program.cs`

- [ ] **Step 1: Crear IRatingsApiService**

```csharp
// PropertyMap.Web/PropertyMap.Web/Services/IRatingsApiService.cs
using PropertyMap.Core.DTOs.Ratings;

namespace PropertyMap.Web.Services;

public interface IRatingsApiService
{
    Task<PropertyRatingStatsDto?> RatePropertyAsync(RatePropertyRequest request);
    Task<PropertyRatingStatsDto?> GetPropertyStatsAsync(int listingId);
    Task<AgentRatingStatsDto?> RateAgentAsync(RateAgentRequest request);
    Task<AgentRatingStatsDto?> GetAgentStatsAsync(int publisherId);
    Task<List<AgentRankingItemDto>> GetRankingAsync(string? ciudad = null, int top = 20);
}
```

- [ ] **Step 2: Crear RatingsApiService**

```csharp
// PropertyMap.Web/PropertyMap.Web/Services/RatingsApiService.cs
using System.Net.Http.Headers;
using System.Net.Http.Json;
using PropertyMap.Core.DTOs.Ratings;

namespace PropertyMap.Web.Services;

public class RatingsApiService : IRatingsApiService
{
    private readonly HttpClient _http;
    private readonly MemoryTokenStore _tokenStore;

    public RatingsApiService(IHttpClientFactory httpFactory, MemoryTokenStore tokenStore)
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

    public async Task<PropertyRatingStatsDto?> RatePropertyAsync(RatePropertyRequest request)
    {
        try
        {
            SetAuth();
            var resp = await _http.PostAsJsonAsync("api/ratings/property", request);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<PropertyRatingStatsDto>();
        }
        catch { return null; }
    }

    public async Task<PropertyRatingStatsDto?> GetPropertyStatsAsync(int listingId)
    {
        try
        {
            return await _http.GetFromJsonAsync<PropertyRatingStatsDto>($"api/ratings/property/{listingId}/stats");
        }
        catch { return null; }
    }

    public async Task<AgentRatingStatsDto?> RateAgentAsync(RateAgentRequest request)
    {
        try
        {
            SetAuth();
            var resp = await _http.PostAsJsonAsync("api/ratings/agent", request);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<AgentRatingStatsDto>();
        }
        catch { return null; }
    }

    public async Task<AgentRatingStatsDto?> GetAgentStatsAsync(int publisherId)
    {
        try
        {
            return await _http.GetFromJsonAsync<AgentRatingStatsDto>($"api/ratings/agent/{publisherId}/stats");
        }
        catch { return null; }
    }

    public async Task<List<AgentRankingItemDto>> GetRankingAsync(string? ciudad = null, int top = 20)
    {
        try
        {
            var query = ciudad is not null ? $"?ciudad={Uri.EscapeDataString(ciudad)}&top={top}" : $"?top={top}";
            return await _http.GetFromJsonAsync<List<AgentRankingItemDto>>($"api/ratings/ranking{query}") ?? [];
        }
        catch { return []; }
    }
}
```

- [ ] **Step 3: Registrar en PropertyMap.Web/PropertyMap.Web/Program.cs**

Agregar después de la línea `builder.Services.AddScoped<IConsultasApiService, ConsultasApiService>();`:

```csharp
builder.Services.AddScoped<IRatingsApiService, RatingsApiService>();
```

- [ ] **Step 4: Verificar build**

```bash
dotnet build "PropertyMap.Web/PropertyMap.Web/PropertyMap.Web.csproj"
```

Expected: Build succeeded.

- [ ] **Step 5: Commit**

```bash
git add "PropertyMap.Web/PropertyMap.Web/Services/IRatingsApiService.cs" \
        "PropertyMap.Web/PropertyMap.Web/Services/RatingsApiService.cs" \
        "PropertyMap.Web/PropertyMap.Web/Program.cs"
git commit -m "feat: IRatingsApiService + RatingsApiService + Web Program.cs registration"
```

---

## Task 6: RatingStars.razor + CSS

**Files:**
- Create: `PropertyMap.Web/PropertyMap.Web/Components/Shared/RatingStars.razor`
- Modify: `PropertyMap.Web/PropertyMap.Web/wwwroot/css/app.css`

- [ ] **Step 1: Crear RatingStars.razor**

```razor
@* PropertyMap.Web/PropertyMap.Web/Components/Shared/RatingStars.razor *@
<div class="rating-stars @(ReadOnly ? "rating-stars--readonly" : "rating-stars--interactive")">
    @for (int i = 1; i <= 5; i++)
    {
        var star = i;
        <button type="button"
                class="rating-star-btn @(GetStarClass(star))"
                disabled="@ReadOnly"
                @onclick="() => HandleClick(star)"
                @onmouseover="() => HandleHover(star)"
                @onmouseout="HandleMouseOut"
                aria-label="@star estrellas">
            <svg width="20" height="20" viewBox="0 0 24 24" fill="currentColor" xmlns="http://www.w3.org/2000/svg">
                <path d="M12 2l3.09 6.26L22 9.27l-5 4.87 1.18 6.88L12 17.77l-6.18 3.25L7 14.14 2 9.27l6.91-1.01L12 2z"/>
            </svg>
        </button>
    }
</div>

@code {
    [Parameter] public double Value { get; set; }
    [Parameter] public bool ReadOnly { get; set; }
    [Parameter] public EventCallback<int> OnChange { get; set; }

    private int _hovered;

    private string GetStarClass(int star)
    {
        var active = _hovered > 0 ? _hovered : Value;
        return star <= active ? "rating-star-btn--filled" : "rating-star-btn--empty";
    }

    private async Task HandleClick(int star)
    {
        if (!ReadOnly) await OnChange.InvokeAsync(star);
    }

    private void HandleHover(int star) { if (!ReadOnly) _hovered = star; }
    private void HandleMouseOut() { _hovered = 0; }
}
```

- [ ] **Step 2: Agregar estilos de rating en app.css**

Agregar al final de `PropertyMap.Web/PropertyMap.Web/wwwroot/css/app.css`:

```css
/* ── Rating Stars ── */
.rating-stars {
    display: inline-flex;
    gap: 2px;
}

.rating-star-btn {
    background: none;
    border: none;
    padding: 2px;
    cursor: pointer;
    transition: transform 0.1s;
    line-height: 0;
}

.rating-star-btn:hover:not(:disabled) {
    transform: scale(1.2);
}

.rating-star-btn--filled {
    color: #f59e0b;
}

.rating-star-btn--empty {
    color: #d1d5db;
}

.rating-stars--readonly .rating-star-btn {
    cursor: default;
    pointer-events: none;
}

/* ── Rating Form ── */
.rating-form {
    background: var(--color-surface, #fff);
    border: 1px solid var(--color-border, #e5e7eb);
    border-radius: var(--radius-lg, 12px);
    padding: var(--space-5, 1.25rem);
    display: flex;
    flex-direction: column;
    gap: var(--space-4, 1rem);
}

.rating-form__title {
    font-size: 1rem;
    font-weight: 600;
    margin: 0;
}

.rating-form__criterion {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: var(--space-3, 0.75rem);
}

.rating-form__criterion label {
    font-size: 0.875rem;
    color: var(--color-text-muted, #6b7280);
    min-width: 120px;
}

.rating-form__textarea {
    width: 100%;
    border: 1px solid var(--color-border, #e5e7eb);
    border-radius: var(--radius-md, 8px);
    padding: var(--space-3, 0.75rem);
    font-size: 0.875rem;
    resize: vertical;
    font-family: inherit;
    box-sizing: border-box;
}

.rating-form__success {
    color: #059669;
    font-size: 0.875rem;
    margin: 0;
}

.rating-form__stats {
    display: flex;
    align-items: center;
    gap: var(--space-2, 0.5rem);
    font-size: 0.875rem;
    color: var(--color-text-muted, #6b7280);
}

/* ── Top Agentes ── */
.ranking-table {
    width: 100%;
    border-collapse: collapse;
    font-size: 0.9rem;
}

.ranking-table th,
.ranking-table td {
    padding: var(--space-3, 0.75rem) var(--space-4, 1rem);
    text-align: left;
    border-bottom: 1px solid var(--color-border, #e5e7eb);
}

.ranking-table th {
    font-weight: 600;
    color: var(--color-text-muted, #6b7280);
    font-size: 0.8rem;
    text-transform: uppercase;
    letter-spacing: 0.05em;
}

.ranking-table tr:hover td {
    background: var(--color-surface-hover, #f9fafb);
}

.ranking-score {
    font-weight: 700;
    color: var(--color-primary, #2563eb);
}

.ranking-pos {
    font-weight: 700;
    color: var(--color-text-muted, #6b7280);
    width: 40px;
}

.ranking-filter {
    display: flex;
    align-items: center;
    gap: var(--space-3, 0.75rem);
    margin-bottom: var(--space-5, 1.25rem);
}

.ranking-filter input {
    border: 1px solid var(--color-border, #e5e7eb);
    border-radius: var(--radius-md, 8px);
    padding: var(--space-2, 0.5rem) var(--space-3, 0.75rem);
    font-size: 0.875rem;
    width: 220px;
}
```

- [ ] **Step 3: Verificar build**

```bash
dotnet build "PropertyMap.Web/PropertyMap.Web/PropertyMap.Web.csproj"
```

Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add "PropertyMap.Web/PropertyMap.Web/Components/Shared/RatingStars.razor" \
        "PropertyMap.Web/PropertyMap.Web/wwwroot/css/app.css"
git commit -m "feat: RatingStars.razor shared component + rating/ranking CSS styles"
```

---

## Task 7: PropertyRatingForm.razor + AgentRatingForm.razor

**Files:**
- Create: `PropertyMap.Web/PropertyMap.Web/Components/Shared/PropertyRatingForm.razor`
- Create: `PropertyMap.Web/PropertyMap.Web/Components/Shared/AgentRatingForm.razor`

- [ ] **Step 1: Crear PropertyRatingForm.razor**

```razor
@* PropertyMap.Web/PropertyMap.Web/Components/Shared/PropertyRatingForm.razor *@
@using PropertyMap.Core.DTOs.Ratings
@inject IRatingsApiService RatingsApi

<div class="rating-form">
    <h3 class="rating-form__title">Valorar esta propiedad</h3>

    <div class="rating-form__criterion">
        <label>Ubicación</label>
        <RatingStars Value="_puntajeUbicacion" OnChange="v => _puntajeUbicacion = v" />
    </div>
    <div class="rating-form__criterion">
        <label>Estado</label>
        <RatingStars Value="_puntajeEstado" OnChange="v => _puntajeEstado = v" />
    </div>
    <div class="rating-form__criterion">
        <label>Precio/Calidad</label>
        <RatingStars Value="_puntajePrecioCalidad" OnChange="v => _puntajePrecioCalidad = v" />
    </div>

    <textarea class="rating-form__textarea" rows="2"
              @bind="_comentario"
              placeholder="Comentario opcional..."></textarea>

    <button class="btn-primary"
            @onclick="HandleSubmit"
            disabled="@(_sending || _puntajeUbicacion == 0 || _puntajeEstado == 0 || _puntajePrecioCalidad == 0)">
        @(_sending ? "Enviando..." : (_stats is not null ? "Actualizar valoración" : "Valorar"))
    </button>

    @if (_success)
    {
        <p class="rating-form__success">¡Gracias por tu valoración!</p>
    }

    @if (_stats is not null && _stats.TotalValoraciones > 0)
    {
        <div class="rating-form__stats">
            <RatingStars Value="_stats.PromedioGeneral" ReadOnly="true" />
            <span>@_stats.PromedioGeneral.ToString("F1") promedio (@_stats.TotalValoraciones valoraciones)</span>
        </div>
    }
</div>

@code {
    [Parameter] public int ListingId { get; set; }

    private int _puntajeUbicacion;
    private int _puntajeEstado;
    private int _puntajePrecioCalidad;
    private string? _comentario;
    private bool _sending;
    private bool _success;
    private PropertyRatingStatsDto? _stats;

    protected override async Task OnParametersSetAsync()
    {
        _stats = await RatingsApi.GetPropertyStatsAsync(ListingId);
    }

    private async Task HandleSubmit()
    {
        _sending = true;
        _success = false;
        var result = await RatingsApi.RatePropertyAsync(new RatePropertyRequest(
            ListingId, _puntajeUbicacion, _puntajeEstado, _puntajePrecioCalidad, _comentario));
        if (result is not null)
        {
            _stats = result;
            _success = true;
        }
        _sending = false;
    }
}
```

- [ ] **Step 2: Crear AgentRatingForm.razor**

```razor
@* PropertyMap.Web/PropertyMap.Web/Components/Shared/AgentRatingForm.razor *@
@using PropertyMap.Core.DTOs.Ratings
@inject IRatingsApiService RatingsApi

<div class="rating-form">
    <h3 class="rating-form__title">Valorar al agente</h3>

    <div class="rating-form__criterion">
        <label>Atención</label>
        <RatingStars Value="_puntajeAtencion" OnChange="v => _puntajeAtencion = v" />
    </div>
    <div class="rating-form__criterion">
        <label>Rapidez</label>
        <RatingStars Value="_puntajeRapidez" OnChange="v => _puntajeRapidez = v" />
    </div>
    <div class="rating-form__criterion">
        <label>Transparencia</label>
        <RatingStars Value="_puntajeTransparencia" OnChange="v => _puntajeTransparencia = v" />
    </div>
    <div class="rating-form__criterion">
        <label>Profesionalismo</label>
        <RatingStars Value="_puntajeProfesionalismo" OnChange="v => _puntajeProfesionalismo = v" />
    </div>

    <textarea class="rating-form__textarea" rows="2"
              @bind="_comentario"
              placeholder="Comentario opcional..."></textarea>

    <button class="btn-primary"
            @onclick="HandleSubmit"
            disabled="@(_sending || _puntajeAtencion == 0)">
        @(_sending ? "Enviando..." : (_stats is not null ? "Actualizar valoración" : "Valorar agente"))
    </button>

    @if (_success)
    {
        <p class="rating-form__success">¡Gracias por tu valoración!</p>
    }

    @if (_stats is not null && _stats.TotalValoraciones > 0)
    {
        <div class="rating-form__stats">
            <RatingStars Value="_stats.PromedioGeneral" ReadOnly="true" />
            <span>@_stats.PromedioGeneral.ToString("F1") promedio (@_stats.TotalValoraciones valoraciones)</span>
        </div>
    }
</div>

@code {
    [Parameter] public int PublisherId { get; set; }

    private int _puntajeAtencion;
    private int _puntajeRapidez;
    private int _puntajeTransparencia;
    private int _puntajeProfesionalismo;
    private string? _comentario;
    private bool _sending;
    private bool _success;
    private AgentRatingStatsDto? _stats;

    protected override async Task OnParametersSetAsync()
    {
        if (PublisherId > 0)
            _stats = await RatingsApi.GetAgentStatsAsync(PublisherId);
    }

    private async Task HandleSubmit()
    {
        if (_puntajeAtencion == 0 || _puntajeRapidez == 0 || _puntajeTransparencia == 0 || _puntajeProfesionalismo == 0) return;
        _sending = true;
        _success = false;
        var result = await RatingsApi.RateAgentAsync(new RateAgentRequest(
            PublisherId, _puntajeAtencion, _puntajeRapidez, _puntajeTransparencia, _puntajeProfesionalismo, _comentario));
        if (result is not null)
        {
            _stats = result;
            _success = true;
        }
        _sending = false;
    }
}
```

- [ ] **Step 3: Verificar build**

```bash
dotnet build "PropertyMap.Web/PropertyMap.Web/PropertyMap.Web.csproj"
```

Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add "PropertyMap.Web/PropertyMap.Web/Components/Shared/PropertyRatingForm.razor" \
        "PropertyMap.Web/PropertyMap.Web/Components/Shared/AgentRatingForm.razor"
git commit -m "feat: PropertyRatingForm.razor + AgentRatingForm.razor shared components"
```

---

## Task 8: Account/ConsultaDetalle.razor + TopAgentes.razor

**Files:**
- Modify: `PropertyMap.Web/PropertyMap.Web/Components/Pages/Account/ConsultaDetalle.razor`
- Create: `PropertyMap.Web/PropertyMap.Web/Components/Pages/TopAgentes.razor`

- [ ] **Step 1: Modificar Account/ConsultaDetalle.razor para agregar los formularios de rating**

Agregar `@inject IRatingsApiService RatingsApi` después de las otras directivas `@inject` al principio del archivo.

Dentro del bloque `else` (donde se muestra el hilo, alrededor de la línea 34), después de `<ConsultaThread ... />`, agregar:

```razor
@if (detail is not null)
{
    @if (detail.OperacionPropiedad == "AlquilerTemporario")
    {
        <PropertyRatingForm ListingId="detail.PropertyListingId" />
    }
    @if (detail.PublisherId.HasValue)
    {
        <AgentRatingForm PublisherId="detail.PublisherId.Value" />
    }
}
```

El archivo completo modificado queda:

```razor
@page "/account/consultas/{Id:int}"
@page "/account/consultas/nueva"
@rendermode InteractiveServer
@inject IConsultasApiService ConsultasApi
@inject IRatingsApiService RatingsApi
@inject IAuthService AuthService
@inject NavigationManager Nav

<PageTitle>Consulta — PropertyMap</PageTitle>

<AuthorizeView>
    <Authorized>
        <div class="app-shell" style="display:flex;flex-direction:column">
            <nav class="pm-navbar" role="navigation">
                <a href="/" class="pm-navbar__logo">PropertyMap</a>
                <span style="font-weight:600">@(detail?.PropertyTitulo ?? "Consulta")</span>
                <div class="pm-navbar__actions">
                    <a href="/account/consultas" class="btn-ghost">← Mis consultas</a>
                </div>
            </nav>

            <div style="padding:var(--space-6,1.5rem);max-width:700px;margin:0 auto;width:100%;display:flex;flex-direction:column;gap:var(--space-4)">

                @if (loading)
                {
                    <p style="color:var(--color-text-muted)">Cargando...</p>
                }
                else if (error is not null)
                {
                    <div style="color:#be123c">@error</div>
                }
                else
                {
                    @if (ListingId > 0 && detail is null)
                    {
                        <p style="color:var(--color-text-muted)">
                            Escribí tu primer mensaje para iniciar la consulta sobre esta propiedad.
                        </p>
                    }

                    <ConsultaThread
                        Mensajes="@(detail?.Mensajes ?? [])"
                        EsPublisher="false"
                        OnSend="HandleSend" />

                    @if (detail is not null)
                    {
                        @if (detail.OperacionPropiedad == "AlquilerTemporario")
                        {
                            <PropertyRatingForm ListingId="detail.PropertyListingId" />
                        }
                        @if (detail.PublisherId.HasValue)
                        {
                            <AgentRatingForm PublisherId="detail.PublisherId.Value" />
                        }
                    }
                }
            </div>
        </div>
    </Authorized>
    <NotAuthorized>
        <div class="auth-page">
            <div class="auth-card">
                <a href="/" class="auth-logo">PropertyMap</a>
                <p style="text-align:center">Necesitás iniciar sesión.</p>
                <a href="/Account/Login" class="btn-primary auth-btn" style="text-align:center">
                    Iniciar sesión
                </a>
            </div>
        </div>
    </NotAuthorized>
</AuthorizeView>

@code {
    [Parameter] public int Id { get; set; }
    [SupplyParameterFromQuery] public int ListingId { get; set; }

    private PropertyMap.Core.DTOs.Consultas.ConsultaDetailDto? detail;
    private bool loading = true;
    private string? error;
    private bool sessionRestored;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && !sessionRestored)
        {
            sessionRestored = true;
            await AuthService.TryRestoreSessionAsync();
            await LoadDetail();
            StateHasChanged();
        }
    }

    private async Task LoadDetail()
    {
        loading = true;
        error = null;
        try
        {
            if (Id > 0)
            {
                detail = await ConsultasApi.GetDetailAsync(Id);
                if (detail is null) error = "No tenés acceso a esta consulta.";
            }
            else if (ListingId > 0)
            {
                detail = null;
            }
            else
            {
                Nav.NavigateTo("/account/consultas");
            }
        }
        catch { error = "Error al cargar la consulta."; }
        finally { loading = false; }
    }

    private async Task HandleSend(string mensaje)
    {
        var listingIdToUse = detail?.PropertyListingId ?? ListingId;
        if (listingIdToUse <= 0) return;

        var result = await ConsultasApi.CreateOrContinueAsync(listingIdToUse, mensaje);
        if (result is not null)
        {
            detail = result;
            if (Id == 0)
                Nav.NavigateTo($"/account/consultas/{result.Id}", replace: true);
        }
    }
}
```

- [ ] **Step 2: Crear TopAgentes.razor**

```razor
@* PropertyMap.Web/PropertyMap.Web/Components/Pages/TopAgentes.razor *@
@page "/top-agentes"
@rendermode InteractiveServer
@using PropertyMap.Core.DTOs.Ratings
@inject IRatingsApiService RatingsApi

<PageTitle>Top Agentes — PropertyMap</PageTitle>

<div class="app-shell" style="display:flex;flex-direction:column">
    <nav class="pm-navbar" role="navigation">
        <a href="/" class="pm-navbar__logo">PropertyMap</a>
        <div class="pm-navbar__actions">
            <a href="/" class="btn-ghost">Volver al mapa</a>
        </div>
    </nav>

    <div style="padding:var(--space-6,1.5rem);max-width:900px;margin:0 auto;width:100%">
        <h1 style="font-size:1.5rem;font-weight:700;margin-bottom:var(--space-5)">Top Agentes e Inmobiliarias</h1>

        <div class="ranking-filter">
            <label style="font-size:0.875rem;color:var(--color-text-muted)">Filtrar por ciudad:</label>
            <input type="text"
                   @bind="ciudadFiltro"
                   @bind:event="oninput"
                   @oninput="OnCiudadChanged"
                   placeholder="Ej: Buenos Aires"
                   class="ranking-filter-input" />
        </div>

        @if (loading)
        {
            <p style="color:var(--color-text-muted)">Cargando ranking...</p>
        }
        else if (ranking.Count == 0)
        {
            <p style="color:var(--color-text-muted)">
                @(string.IsNullOrWhiteSpace(ciudadFiltro)
                    ? "Todavía no hay agentes con valoraciones."
                    : $"No hay agentes con valoraciones en \"{ciudadFiltro}\".")
            </p>
        }
        else
        {
            <table class="ranking-table">
                <thead>
                    <tr>
                        <th class="ranking-pos">#</th>
                        <th>Nombre</th>
                        <th>Tipo</th>
                        <th>Score</th>
                        <th>Rating</th>
                        <th>Resp. promedio</th>
                        <th>Operaciones</th>
                        <th>Antigüedad</th>
                    </tr>
                </thead>
                <tbody>
                    @foreach (var (item, idx) in ranking.Select((r, i) => (r, i + 1)))
                    {
                        <tr>
                            <td class="ranking-pos">@idx</td>
                            <td style="font-weight:600">@item.Nombre</td>
                            <td>@item.Tipo</td>
                            <td class="ranking-score">@item.RankingScore.ToString("F1")</td>
                            <td>
                                <RatingStars Value="item.RatingPromedio" ReadOnly="true" />
                                <span style="font-size:0.8rem;color:var(--color-text-muted)">@item.RatingPromedio.ToString("F1")</span>
                            </td>
                            <td>@(item.TiempoRespuestaHoras == 0 ? "—" : $"{item.TiempoRespuestaHoras:F1}h")</td>
                            <td>@item.Operaciones</td>
                            <td>@item.AntiguedadAnios.ToString("F1") años</td>
                        </tr>
                    }
                </tbody>
            </table>
        }
    </div>
</div>

@code {
    private List<AgentRankingItemDto> ranking = [];
    private bool loading = true;
    private string ciudadFiltro = "";
    private bool initialized;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && !initialized)
        {
            initialized = true;
            await LoadRanking();
            StateHasChanged();
        }
    }

    private async Task LoadRanking()
    {
        loading = true;
        ranking = await RatingsApi.GetRankingAsync(
            string.IsNullOrWhiteSpace(ciudadFiltro) ? null : ciudadFiltro.Trim());
        loading = false;
    }

    private async Task OnCiudadChanged(ChangeEventArgs e)
    {
        ciudadFiltro = e.Value?.ToString() ?? "";
        await LoadRanking();
        StateHasChanged();
    }
}
```

- [ ] **Step 3: Verificar build**

```bash
dotnet build "PropertyMap.Web/PropertyMap.Web/PropertyMap.Web.csproj"
```

Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add "PropertyMap.Web/PropertyMap.Web/Components/Pages/Account/ConsultaDetalle.razor" \
        "PropertyMap.Web/PropertyMap.Web/Components/Pages/TopAgentes.razor"
git commit -m "feat: add rating forms to ConsultaDetalle + TopAgentes public ranking page"
```

---

## Task 9: PropertyDetail.razor + Navbar.razor

**Files:**
- Modify: `PropertyMap.Web/PropertyMap.Web/Components/Pages/PropertyDetail.razor`
- Modify: `PropertyMap.Web/PropertyMap.Web/Components/Layout/Navbar.razor`

- [ ] **Step 1: Modificar PropertyDetail.razor para mostrar stats de rating**

Agregar `@inject IRatingsApiService RatingsApi` y `@using PropertyMap.Core.DTOs.Ratings` después de las otras directivas en la parte superior.

En `@code`, agregar campo:
```csharp
private PropertyRatingStatsDto? ratingStats;
```

En `OnInitializedAsync`, agregar después de `detail = await ListingApi.GetByIdAsync(Id);`:
```csharp
if (detail is not null)
    ratingStats = await RatingsApi.GetPropertyStatsAsync(Id);
```

En el bloque de HTML del `detail-header`, después de `<div class="detail-price">@FormatPrice()</div>`, agregar el bloque de rating:
```razor
@if (ratingStats is not null && ratingStats.TotalValoraciones > 0)
{
    <div style="display:flex;align-items:center;gap:var(--space-2)">
        <RatingStars Value="ratingStats.PromedioGeneral" ReadOnly="true" />
        <span style="font-size:0.8rem;color:var(--color-text-muted)">
            @ratingStats.PromedioGeneral.ToString("F1") (@ratingStats.TotalValoraciones)
        </span>
    </div>
}
```

El `@code` block actualizado (solo las partes que cambian):

```csharp
@code {
    [Parameter] public int Id { get; set; }

    private ListingDetailDto? detail;
    private PropertyRatingStatsDto? ratingStats;
    private bool loading = true;
    private int galleryIdx;

    protected override async Task OnInitializedAsync()
    {
        detail = await ListingApi.GetByIdAsync(Id);
        if (detail is not null)
            ratingStats = await RatingsApi.GetPropertyStatsAsync(Id);
        loading = false;
    }

    // ... rest of existing methods unchanged
}
```

- [ ] **Step 2: Modificar Navbar.razor para agregar link Top Agentes**

En `PropertyMap.Web/PropertyMap.Web/Components/Layout/Navbar.razor`, agregar el link **fuera** del bloque `<AuthorizeView>` (visible para todos, sin auth). El link va justo antes de `<div class="pm-navbar__actions">` o dentro del div de acciones, junto a otros links.

Agregar dentro de `pm-navbar__actions`, antes del bloque `<AuthorizeView>`:

```razor
<a href="/top-agentes" class="btn-ghost">Top Agentes</a>
```

El archivo completo modificado:

```razor
@using Microsoft.AspNetCore.Components.Authorization

<nav class="pm-navbar" role="navigation" aria-label="Navegación principal">
    <a href="/" class="pm-navbar__logo" aria-label="PropertyMap — Inicio">
        PropertyMap
    </a>

    <div class="pm-navbar__actions">
        <a href="/top-agentes" class="btn-ghost">Top Agentes</a>
        <AuthorizeView>
            <Authorized>
                <a href="/account/favorites" class="btn-ghost">♥ Favoritos</a>
                <a href="/account/consultas" class="btn-ghost">Consultas</a>
                <a href="/account/profile" class="btn-ghost">Mi perfil</a>
                <AuthorizeView Roles="Publisher" Context="publisherCtx">
                    <Authorized Context="pAuth">
                        <a href="/publisher/consultas" class="btn-ghost">Consultas recibidas</a>
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

- [ ] **Step 3: Verificar build completo**

```bash
dotnet build "PropertyMap.Web/PropertyMap.Web/PropertyMap.Web.csproj"
dotnet test PropertyMap.Tests/PropertyMap.Tests.csproj -v n
```

Expected: Build succeeded, 58 passed.

- [ ] **Step 4: Commit**

```bash
git add "PropertyMap.Web/PropertyMap.Web/Components/Pages/PropertyDetail.razor" \
        "PropertyMap.Web/PropertyMap.Web/Components/Layout/Navbar.razor"
git commit -m "feat: show property rating stats on PropertyDetail + Top Agentes link in Navbar"
```

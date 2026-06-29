# Phase 9.4 — Audit Logs Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Registrar en la tabla `AuditLog` (existente desde Phase 3, sin usar hasta ahora) cuándo un admin aprueba/rechaza una propiedad o resuelve/rechaza un reporte, y exponer una página admin simple para consultar esas entradas.

**Architecture:** Nuevo `IAuditLogRepository`/`AuditLogRepository` (mismo patrón que `IReportRepository`/`ReportRepository`), 2 hooks de escritura dentro de `AdminController.Review`/`ReviewReport`, un endpoint nuevo `GET /api/admin/audit-logs`, y una página Blazor `Admin/AuditLogs.razor` (mismo patrón que `Admin/Reportes.razor`).

**Tech Stack:** EF Core 9 (sin migración nueva — la tabla `AuditLog` ya existe en el schema desde Phase 3), ASP.NET Core Web API, Blazor Server.

**Spec de referencia:** `docs/superpowers/specs/2026-06-29-phase9-auditlogs-design.md`

---

### Task 1: AuditLogDto + IAuditLogRepository/AuditLogRepository

**Files:**
- Create: `PropertyMap.Core/DTOs/Admin/AuditLogDto.cs`
- Create: `PropertyMap.Core/Interfaces/IAuditLogRepository.cs`
- Create: `PropertyMap.Infrastructure/Repositories/AuditLogRepository.cs`

- [ ] **Step 1: Crear el DTO**

```csharp
namespace PropertyMap.Core.DTOs.Admin;

public record AuditLogDto(
    int Id,
    string? UserId,
    string Accion,
    string Entidad,
    string EntidadId,
    string? Detalles,
    DateTime FechaAccion,
    string? IpAddress
);
```

- [ ] **Step 2: Crear la interfaz**

```csharp
using PropertyMap.Core.DTOs.Admin;
using PropertyMap.Core.Entities;

namespace PropertyMap.Core.Interfaces;

public interface IAuditLogRepository
{
    Task AddAsync(AuditLog log);
    Task<List<AuditLogDto>> GetRecentAsync(int take = 50);
}
```

- [ ] **Step 3: Implementar el repositorio**

```csharp
using Microsoft.EntityFrameworkCore;
using PropertyMap.Core.DTOs.Admin;
using PropertyMap.Core.Entities;
using PropertyMap.Core.Interfaces;
using PropertyMap.Infrastructure.Data;

namespace PropertyMap.Infrastructure.Repositories;

public class AuditLogRepository(AppDbContext ctx) : IAuditLogRepository
{
    public async Task AddAsync(AuditLog log)
    {
        ctx.AuditLogs.Add(log);
        await ctx.SaveChangesAsync();
    }

    public async Task<List<AuditLogDto>> GetRecentAsync(int take = 50) =>
        await ctx.AuditLogs
            .OrderByDescending(a => a.FechaAccion)
            .Take(take)
            .Select(a => new AuditLogDto(
                a.Id, a.UserId, a.Accion, a.Entidad, a.EntidadId,
                a.Detalles, a.FechaAccion, a.IpAddress))
            .ToListAsync();
}
```

- [ ] **Step 4: Verificar que compila**

Run: `cd C:\Agentes\PropertyMap && dotnet build src/PropertyMap.Web/PropertyMap.Web.sln`
Expected: `Compilación correcta. 0 Errores`

- [ ] **Step 5: Commit**

```bash
cd C:\Agentes\PropertyMap\src
git add PropertyMap.Core/DTOs/Admin/AuditLogDto.cs PropertyMap.Core/Interfaces/IAuditLogRepository.cs PropertyMap.Infrastructure/Repositories/AuditLogRepository.cs
git commit -m "feat(audit): add AuditLogDto and IAuditLogRepository/AuditLogRepository"
```

---

### Task 2: Wiring en AdminController + DI

**Files:**
- Modify: `PropertyMap.Api/Controllers/AdminController.cs`
- Modify: `PropertyMap.Api/Program.cs`

- [ ] **Step 1: Reescribir AdminController.cs completo**

El archivo actual es:

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PropertyMap.Core.DTOs.Admin;
using PropertyMap.Core.DTOs.Reports;
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

    public AdminController(
        IListingRepository listings,
        IReportRepository reports,
        IAlertMatchingService alertMatching)
    {
        _listings = listings;
        _reports = reports;
        _alertMatching = alertMatching;
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

        return NoContent();
    }
}
```

Reemplazalo completo por:

```csharp
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

        await _auditLog.AddAsync(new AuditLog
        {
            UserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
            Accion = request.Aprobar ? "AprobarListing" : "RechazarListing",
            Entidad = "PropertyListing",
            EntidadId = id.ToString(),
            Detalles = request.Aprobar ? null : request.MotivoRechazo,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        });

        if (request.Aprobar)
            await _alertMatching.NotifyMatchingAlertsAsync(listing);

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

        await _auditLog.AddAsync(new AuditLog
        {
            UserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
            Accion = request.NuevoEstado == EstadoReporte.Resuelto ? "ResolverReporte" : "RechazarReporte",
            Entidad = "Report",
            EntidadId = id.ToString(),
            Detalles = null,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        });

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

        return NoContent();
    }

    [HttpGet("audit-logs")]
    public async Task<IActionResult> GetAuditLogs() =>
        Ok(await _auditLog.GetRecentAsync());
}
```

- [ ] **Step 2: Registrar el repositorio en Program.cs**

En `PropertyMap.Api/Program.cs`, agregar después de la línea `builder.Services.AddScoped<IListingStatsRepository, ListingStatsRepository>();`:

```csharp
builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();
```

- [ ] **Step 3: Verificar que compila**

Run: `cd C:\Agentes\PropertyMap && dotnet build src/PropertyMap.Web/PropertyMap.Web.sln`
Expected: `Compilación correcta. 0 Errores`

- [ ] **Step 4: Correr la suite completa (no debería romperse nada — solo se agrega lógica nueva, sin cambiar comportamiento existente)**

Run: `cd C:\Agentes\PropertyMap && dotnet test src/PropertyMap.Tests/PropertyMap.Tests.csproj`
Expected: `Correctas! - Con error: 0, Superado: 105, Total: 105`

- [ ] **Step 5: Commit**

```bash
cd C:\Agentes\PropertyMap\src
git add PropertyMap.Api/Controllers/AdminController.cs PropertyMap.Api/Program.cs
git commit -m "feat(audit): log admin moderation actions (listing/report review)"
```

---

### Task 3: Tests de integración

**Files:**
- Create: `PropertyMap.Tests/Api/AuditLogTests.cs`

- [ ] **Step 1: Crear el archivo con las 4 pruebas**

```csharp
using System.Net;
using System.Net.Http.Json;
using PropertyMap.Core.DTOs.Admin;
using PropertyMap.Core.DTOs.Properties;
using PropertyMap.Core.DTOs.Reports;
using PropertyMap.Core.Enums;
using Xunit;

namespace PropertyMap.Tests.Api;

public class AuditLogTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public AuditLogTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private record CreatedIdDto(int Id);

    private static CreateListingRequest BuildListingRequest(string titulo) => new(
        Operacion: TipoOperacion.Venta, TipoPropiedad: TipoPropiedad.Casa,
        Titulo: titulo, Descripcion: "Test",
        Precio: 90000, Moneda: "USD",
        DireccionTexto: "Av. Auditoria 1", Ciudad: "Cordoba", Provincia: "Cordoba",
        Lat: -31.42, Lng: -64.18,
        Superficie: null, SuperficieCubierta: null, Ambientes: null,
        Dormitorios: null, Banos: null, Antiguedad: null,
        Cochera: false, Amenities: []);

    private async Task<(HttpClient pubClient, int listingId)> CreatePendingListingAsync(string titulo)
    {
        var (pubClient, _) = await TestAuthHelper.CreateAuthenticatedPublisherAsync(_factory);
        await TestAuthHelper.CreatePublisherProfileAsync(pubClient);
        var createResp = await pubClient.PostAsJsonAsync("/api/properties", BuildListingRequest(titulo));
        var created = await createResp.Content.ReadFromJsonAsync<CreatedIdDto>();
        return (pubClient, created!.Id);
    }

    [Fact]
    public async Task Review_Aprobar_LogsAuditEntry()
    {
        var (adminClient, _) = await TestAuthHelper.CreateAuthenticatedAdminAsync(_factory);
        var (_, listingId) = await CreatePendingListingAsync("Casa auditoria aprobar");

        await adminClient.PatchAsJsonAsync($"/api/admin/listings/{listingId}/review",
            new ReviewListingRequest(true, null));

        var logs = await adminClient.GetFromJsonAsync<List<AuditLogDto>>("/api/admin/audit-logs");

        Assert.Contains(logs!, l =>
            l.Accion == "AprobarListing" && l.Entidad == "PropertyListing" && l.EntidadId == listingId.ToString());
    }

    [Fact]
    public async Task Review_Rechazar_LogsAuditEntryWithMotivo()
    {
        var (adminClient, _) = await TestAuthHelper.CreateAuthenticatedAdminAsync(_factory);
        var (_, listingId) = await CreatePendingListingAsync("Casa auditoria rechazar");

        await adminClient.PatchAsJsonAsync($"/api/admin/listings/{listingId}/review",
            new ReviewListingRequest(false, "Fotos borrosas"));

        var logs = await adminClient.GetFromJsonAsync<List<AuditLogDto>>("/api/admin/audit-logs");

        Assert.Contains(logs!, l =>
            l.Accion == "RechazarListing" && l.EntidadId == listingId.ToString() && l.Detalles == "Fotos borrosas");
    }

    [Fact]
    public async Task ReviewReport_Resuelto_LogsAuditEntry()
    {
        var (adminClient, _) = await TestAuthHelper.CreateAuthenticatedAdminAsync(_factory);
        var (_, listingId) = await CreatePendingListingAsync("Casa auditoria reporte");
        await adminClient.PatchAsJsonAsync($"/api/admin/listings/{listingId}/review",
            new ReviewListingRequest(true, null));

        var (userClient, _) = await TestAuthHelper.CreateAuthenticatedUserAsync(_factory);
        await userClient.PostAsJsonAsync("/api/reports",
            new CreateReportRequest(listingId, MotivoReporte.Spam, null));

        var pending = await adminClient.GetFromJsonAsync<List<ReportDto>>("/api/admin/reports");
        var reportId = pending!.First(r => r.PropertyListingId == listingId).Id;

        await adminClient.PatchAsJsonAsync($"/api/admin/reports/{reportId}/review",
            new ReviewReportRequest(EstadoReporte.Resuelto));

        var logs = await adminClient.GetFromJsonAsync<List<AuditLogDto>>("/api/admin/audit-logs");

        Assert.Contains(logs!, l =>
            l.Accion == "ResolverReporte" && l.Entidad == "Report" && l.EntidadId == reportId.ToString());
    }

    [Fact]
    public async Task GetAuditLogs_RequiresAdminRole()
    {
        var (pubClient, _) = await TestAuthHelper.CreateAuthenticatedPublisherAsync(_factory);

        var resp = await pubClient.GetAsync("/api/admin/audit-logs");

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }
}
```

- [ ] **Step 2: Correr los tests nuevos**

Run: `cd C:\Agentes\PropertyMap && dotnet test src/PropertyMap.Tests/PropertyMap.Tests.csproj --filter "FullyQualifiedName~AuditLogTests"`
Expected: `Correctas! - Con error: 0, Superado: 4, Total: 4`

- [ ] **Step 3: Correr la suite completa**

Run: `cd C:\Agentes\PropertyMap && dotnet test src/PropertyMap.Tests/PropertyMap.Tests.csproj`
Expected: `Correctas! - Con error: 0, Superado: 109, Total: 109`

- [ ] **Step 4: Commit**

```bash
cd C:\Agentes\PropertyMap\src
git add PropertyMap.Tests/Api/AuditLogTests.cs
git commit -m "test: add integration tests for audit logging"
```

---

### Task 4: Servicio Blazor + página admin

**Files:**
- Create: `PropertyMap.Web/PropertyMap.Web/Services/IAuditLogApiService.cs`
- Create: `PropertyMap.Web/PropertyMap.Web/Services/AuditLogApiService.cs`
- Modify: `PropertyMap.Web/PropertyMap.Web/Program.cs`
- Create: `PropertyMap.Web/PropertyMap.Web/Components/Pages/Admin/AuditLogs.razor`
- Modify: `PropertyMap.Web/PropertyMap.Web/Components/Layout/Navbar.razor`

- [ ] **Step 1: Crear la interfaz del servicio**

```csharp
using PropertyMap.Core.DTOs.Admin;

namespace PropertyMap.Web.Services;

public interface IAuditLogApiService
{
    Task<List<AuditLogDto>> GetRecentAsync();
}
```

- [ ] **Step 2: Implementar el servicio**

```csharp
using System.Net.Http.Headers;
using System.Net.Http.Json;
using PropertyMap.Core.DTOs.Admin;

namespace PropertyMap.Web.Services;

public class AuditLogApiService : IAuditLogApiService
{
    private readonly HttpClient _http;
    private readonly MemoryTokenStore _tokenStore;

    public AuditLogApiService(IHttpClientFactory httpFactory, MemoryTokenStore tokenStore)
    {
        _http = httpFactory.CreateClient("api");
        _tokenStore = tokenStore;
    }

    public async Task<List<AuditLogDto>> GetRecentAsync()
    {
        _http.DefaultRequestHeaders.Authorization = _tokenStore.AccessToken is null
            ? null
            : new AuthenticationHeaderValue("Bearer", _tokenStore.AccessToken);
        return await _http.GetFromJsonAsync<List<AuditLogDto>>("api/admin/audit-logs") ?? [];
    }
}
```

- [ ] **Step 3: Registrar el servicio en Program.cs (Web)**

En `PropertyMap.Web/PropertyMap.Web/Program.cs`, agregar después de la línea `builder.Services.AddScoped<IStatsApiService, StatsApiService>();`:

```csharp
builder.Services.AddScoped<IAuditLogApiService, AuditLogApiService>();
```

- [ ] **Step 4: Crear la página admin**

```razor
@page "/admin/auditoria"
@rendermode InteractiveServer
@attribute [Authorize(Roles = "Admin")]
@using Microsoft.AspNetCore.Authorization
@using PropertyMap.Core.DTOs.Admin
@inject PropertyMap.Web.Services.IAuditLogApiService AuditLogApi

<h1>Auditoría</h1>

@if (_logs.Count == 0)
{
    <p>No hay registros de auditoría todavía.</p>
}
else
{
    <table class="pm-audit-table">
        <thead>
            <tr>
                <th>Fecha</th>
                <th>Usuario</th>
                <th>Acción</th>
                <th>Entidad</th>
                <th>Detalles</th>
            </tr>
        </thead>
        <tbody>
            @foreach (var log in _logs)
            {
                <tr>
                    <td>@log.FechaAccion.ToLocalTime().ToString("g")</td>
                    <td>@(log.UserId ?? "—")</td>
                    <td>@log.Accion</td>
                    <td>@log.Entidad #@log.EntidadId</td>
                    <td>@(log.Detalles ?? "—")</td>
                </tr>
            }
        </tbody>
    </table>
}

@code {
    private List<AuditLogDto> _logs = [];

    protected override async Task OnInitializedAsync()
    {
        _logs = await AuditLogApi.GetRecentAsync();
    }
}
```

- [ ] **Step 5: Agregar el link en Navbar.razor**

En `PropertyMap.Web/PropertyMap.Web/Components/Layout/Navbar.razor`, dentro del bloque `<AuthorizeView Roles="Admin" Context="adminCtx"><Authorized Context="aAuth">`, agregar la línea después de `<a href="/admin/reportes" class="btn-ghost">Reportes</a>`:

```razor
                        <a href="/admin/auditoria" class="btn-ghost">Auditoría</a>
```

- [ ] **Step 6: Verificar que compila**

Run: `cd C:\Agentes\PropertyMap && dotnet build src/PropertyMap.Web/PropertyMap.Web.sln`
Expected: `Compilación correcta. 0 Errores`

- [ ] **Step 7: Commit**

```bash
cd C:\Agentes\PropertyMap\src
git add PropertyMap.Web/PropertyMap.Web/Services/IAuditLogApiService.cs PropertyMap.Web/PropertyMap.Web/Services/AuditLogApiService.cs PropertyMap.Web/PropertyMap.Web/Program.cs PropertyMap.Web/PropertyMap.Web/Components/Pages/Admin/AuditLogs.razor PropertyMap.Web/PropertyMap.Web/Components/Layout/Navbar.razor
git commit -m "feat(audit): add admin page to view recent audit log entries"
```

---

### Task 5: Verificación final

**Files:** ninguno (solo verificación)

- [ ] **Step 1: Correr toda la suite de tests**

Run: `cd C:\Agentes\PropertyMap && dotnet test src/PropertyMap.Tests/PropertyMap.Tests.csproj`
Expected: `Correctas! - Con error: 0, Superado: 109` (105 previos + 4 de AuditLogTests)

- [ ] **Step 2: Correr el build completo de la solución**

Run: `cd C:\Agentes\PropertyMap && dotnet build src/PropertyMap.Web/PropertyMap.Web.sln`
Expected: `Compilación correcta. 0 Errores`

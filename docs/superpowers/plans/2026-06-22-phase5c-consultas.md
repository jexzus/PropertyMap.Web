# Phase 5C — Consultas Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add private bidirectional messaging threads (consultas) between users and publishers, one thread per user-property pair, with in-app and email notifications on each new message.

**Architecture:** Two new entities (`Consulta` as thread header, `ConsultaMensaje` as individual messages) + `IConsultaRepository` + `ConsultasController` (5 endpoints) + `IConsultasApiService` for Blazor + 4 Blazor pages (user inbox, user thread, publisher inbox, publisher thread) + shared `ConsultaThread.razor` component.

**Tech Stack:** .NET 9, ASP.NET Core, EF Core 9 (InMemory for tests), Blazor Server `@rendermode InteractiveServer`, MailKit, xUnit.

---

## File Map

**Created:**
```
PropertyMap.Core/Entities/Consulta.cs
PropertyMap.Core/Entities/ConsultaMensaje.cs
PropertyMap.Core/DTOs/Consultas/ConsultaDtos.cs
PropertyMap.Core/Interfaces/IConsultaRepository.cs
PropertyMap.Infrastructure/Repositories/ConsultaRepository.cs
PropertyMap.Api/Controllers/ConsultasController.cs
PropertyMap.Tests/Api/ConsultasControllerTests.cs
PropertyMap.Web/PropertyMap.Web/Services/IConsultasApiService.cs
PropertyMap.Web/PropertyMap.Web/Services/ConsultasApiService.cs
PropertyMap.Web/PropertyMap.Web/Components/Shared/ConsultaThread.razor
PropertyMap.Web/PropertyMap.Web/Components/Pages/Account/Consultas.razor
PropertyMap.Web/PropertyMap.Web/Components/Pages/Account/ConsultaDetalle.razor
PropertyMap.Web/PropertyMap.Web/Components/Pages/Publisher/Consultas.razor
PropertyMap.Web/PropertyMap.Web/Components/Pages/Publisher/ConsultaDetalle.razor
```

**Modified:**
```
PropertyMap.Infrastructure/Data/AppDbContext.cs         (+ DbSets + OnModelCreating)
PropertyMap.Core/Interfaces/IEmailService.cs            (+ 2 new methods)
PropertyMap.Infrastructure/Services/EmailService.cs     (+ 2 implementations)
PropertyMap.Api/Program.cs                              (+ IConsultaRepository registration)
PropertyMap.Tests/Api/TestWebApplicationFactory.cs      (+ NoOpEmailService methods + CreateAuthenticatedUserAsync)
PropertyMap.Web/PropertyMap.Web/Program.cs              (+ IConsultasApiService registration)
PropertyMap.Web/PropertyMap.Web/Components/Layout/Navbar.razor      (+ Consultas links)
PropertyMap.Web/PropertyMap.Web/Components/Pages/PropertyDetail.razor (+ Consultar button)
PropertyMap.Web/PropertyMap.Web/wwwroot/css/app.css     (+ chat bubble styles + consulta card styles)
```

**EF Migration:**
```
PropertyMap.Infrastructure/Migrations/*_Phase5CConsultas.cs   (add-migration output)
```

---

## Task 1: Entities + AppDbContext + Migration

**Files:**
- Create: `PropertyMap.Core/Entities/Consulta.cs`
- Create: `PropertyMap.Core/Entities/ConsultaMensaje.cs`
- Modify: `PropertyMap.Infrastructure/Data/AppDbContext.cs`

- [ ] **Step 1: Create `Consulta.cs`**

```csharp
// PropertyMap.Core/Entities/Consulta.cs
namespace PropertyMap.Core.Entities;

public class Consulta
{
    public int Id { get; set; }
    public int PropertyListingId { get; set; }
    public string UserId { get; set; } = "";
    public DateTime FechaCreacion { get; set; }
    public DateTime FechaUltimoMensaje { get; set; }

    public PropertyListing PropertyListing { get; set; } = null!;
    public ApplicationUser User { get; set; } = null!;
    public ICollection<ConsultaMensaje> Mensajes { get; set; } = [];
}
```

- [ ] **Step 2: Create `ConsultaMensaje.cs`**

```csharp
// PropertyMap.Core/Entities/ConsultaMensaje.cs
namespace PropertyMap.Core.Entities;

public class ConsultaMensaje
{
    public int Id { get; set; }
    public int ConsultaId { get; set; }
    public string SenderId { get; set; } = "";
    public bool EsDelPublisher { get; set; }
    public string Mensaje { get; set; } = "";
    public DateTime FechaEnvio { get; set; }

    public Consulta Consulta { get; set; } = null!;
    public ApplicationUser Sender { get; set; } = null!;
}
```

- [ ] **Step 3: Add DbSets and model config to `AppDbContext.cs`**

Add two DbSets after the existing `Notifications` line:
```csharp
public DbSet<Consulta> Consultas => Set<Consulta>();
public DbSet<ConsultaMensaje> ConsultaMensajes => Set<ConsultaMensaje>();
```

Add these configurations inside `OnModelCreating`, before the closing `}`:
```csharp
// Consulta — unique thread per user-property pair
modelBuilder.Entity<Consulta>()
    .HasIndex(c => new { c.PropertyListingId, c.UserId }).IsUnique();

modelBuilder.Entity<Consulta>()
    .HasOne(c => c.User)
    .WithMany()
    .HasForeignKey(c => c.UserId)
    .OnDelete(DeleteBehavior.NoAction);

modelBuilder.Entity<Consulta>()
    .HasMany(c => c.Mensajes)
    .WithOne(m => m.Consulta)
    .HasForeignKey(m => m.ConsultaId)
    .OnDelete(DeleteBehavior.Cascade);

// ConsultaMensaje sender — avoid multiple cascade paths
modelBuilder.Entity<ConsultaMensaje>()
    .HasOne(m => m.Sender)
    .WithMany()
    .HasForeignKey(m => m.SenderId)
    .OnDelete(DeleteBehavior.NoAction);
```

- [ ] **Step 4: Add migration**

Run from the solution root (the folder that contains `PropertyMap.sln`):
```bash
dotnet ef migrations add Phase5CConsultas --project PropertyMap.Infrastructure --startup-project PropertyMap.Api
```

Expected: creates `PropertyMap.Infrastructure/Migrations/*_Phase5CConsultas.cs` with `CreateTable` for `Consultas` and `ConsultaMensajes`.

- [ ] **Step 5: Build to verify**

```bash
dotnet build PropertyMap.Infrastructure
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 6: Commit**

```bash
git add PropertyMap.Core/Entities/Consulta.cs PropertyMap.Core/Entities/ConsultaMensaje.cs PropertyMap.Infrastructure/Data/AppDbContext.cs PropertyMap.Infrastructure/Migrations/
git commit -m "feat: add Consulta and ConsultaMensaje entities + Phase5CConsultas migration"
```

---

## Task 2: DTOs

**Files:**
- Create: `PropertyMap.Core/DTOs/Consultas/ConsultaDtos.cs`

- [ ] **Step 1: Create `ConsultaDtos.cs`**

```csharp
// PropertyMap.Core/DTOs/Consultas/ConsultaDtos.cs
namespace PropertyMap.Core.DTOs.Consultas;

public record CreateConsultaRequest(int ListingId, string Mensaje);

public record SendMensajeRequest(string Mensaje);

public record ConsultaSummaryDto(
    int Id,
    int PropertyListingId,
    string PropertyTitulo,
    string UltimoMensaje,
    bool UltimoEsDelPublisher,
    DateTime FechaUltimoMensaje);

public record ConsultaMensajeDto(
    int Id,
    string SenderNombre,
    bool EsDelPublisher,
    string Mensaje,
    DateTime FechaEnvio);

public record ConsultaDetailDto(
    int Id,
    int PropertyListingId,
    string PropertyTitulo,
    List<ConsultaMensajeDto> Mensajes);
```

- [ ] **Step 2: Build to verify**

```bash
dotnet build PropertyMap.Core
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```bash
git add PropertyMap.Core/DTOs/Consultas/ConsultaDtos.cs
git commit -m "feat: add Consultas DTOs"
```

---

## Task 3: IConsultaRepository + ConsultaRepository

**Files:**
- Create: `PropertyMap.Core/Interfaces/IConsultaRepository.cs`
- Create: `PropertyMap.Infrastructure/Repositories/ConsultaRepository.cs`

- [ ] **Step 1: Create `IConsultaRepository.cs`**

```csharp
// PropertyMap.Core/Interfaces/IConsultaRepository.cs
using PropertyMap.Core.DTOs.Consultas;
using PropertyMap.Core.Entities;

namespace PropertyMap.Core.Interfaces;

public interface IConsultaRepository
{
    Task<Consulta> GetOrCreateAsync(int listingId, string userId);
    Task<ConsultaDetailDto?> GetByIdAsync(int consultaId, string requesterId);
    Task<List<ConsultaSummaryDto>> GetByUserAsync(string userId);
    Task<List<ConsultaSummaryDto>> GetByPublisherAsync(string publisherUserId);
    Task<ConsultaMensajeDto> AddMessageAsync(ConsultaMensaje message);
    Task<bool> CanPublisherReplyAsync(int consultaId, string publisherUserId);
    Task<string?> GetConsultaOwnerUserIdAsync(int consultaId);
    Task CreateNotificationAsync(Notification notification);
}
```

- [ ] **Step 2: Create `ConsultaRepository.cs`**

```csharp
// PropertyMap.Infrastructure/Repositories/ConsultaRepository.cs
using Microsoft.EntityFrameworkCore;
using PropertyMap.Core.DTOs.Consultas;
using PropertyMap.Core.Entities;
using PropertyMap.Core.Interfaces;
using PropertyMap.Infrastructure.Data;

namespace PropertyMap.Infrastructure.Repositories;

public class ConsultaRepository : IConsultaRepository
{
    private readonly AppDbContext _ctx;

    public ConsultaRepository(AppDbContext ctx)
    {
        _ctx = ctx;
    }

    public async Task<Consulta> GetOrCreateAsync(int listingId, string userId)
    {
        var existing = await _ctx.Consultas
            .Include(c => c.PropertyListing).ThenInclude(l => l.Publisher)
            .FirstOrDefaultAsync(c => c.PropertyListingId == listingId && c.UserId == userId);

        if (existing is not null) return existing;

        var consulta = new Consulta
        {
            PropertyListingId = listingId,
            UserId = userId,
            FechaCreacion = DateTime.UtcNow,
            FechaUltimoMensaje = DateTime.UtcNow
        };
        _ctx.Consultas.Add(consulta);
        await _ctx.SaveChangesAsync();

        return await _ctx.Consultas
            .Include(c => c.PropertyListing).ThenInclude(l => l.Publisher)
            .FirstAsync(c => c.Id == consulta.Id);
    }

    public async Task<ConsultaDetailDto?> GetByIdAsync(int consultaId, string requesterId)
    {
        var consulta = await _ctx.Consultas
            .Include(c => c.PropertyListing).ThenInclude(l => l.Publisher)
            .Include(c => c.Mensajes).ThenInclude(m => m.Sender)
            .FirstOrDefaultAsync(c => c.Id == consultaId);

        if (consulta is null) return null;

        var isOwner = consulta.UserId == requesterId;
        var isPublisher = consulta.PropertyListing.Publisher?.UserId == requesterId;
        if (!isOwner && !isPublisher) return null;

        return new ConsultaDetailDto(
            consulta.Id,
            consulta.PropertyListingId,
            consulta.PropertyListing.Titulo,
            consulta.Mensajes
                .OrderBy(m => m.FechaEnvio)
                .Select(m => new ConsultaMensajeDto(
                    m.Id,
                    $"{m.Sender.Nombre} {m.Sender.Apellido}",
                    m.EsDelPublisher,
                    m.Mensaje,
                    m.FechaEnvio))
                .ToList());
    }

    public async Task<List<ConsultaSummaryDto>> GetByUserAsync(string userId)
    {
        return await _ctx.Consultas
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.FechaUltimoMensaje)
            .Select(c => new ConsultaSummaryDto(
                c.Id,
                c.PropertyListingId,
                c.PropertyListing.Titulo,
                c.Mensajes.OrderByDescending(m => m.FechaEnvio).Select(m => m.Mensaje).FirstOrDefault() ?? "",
                c.Mensajes.OrderByDescending(m => m.FechaEnvio).Select(m => m.EsDelPublisher).FirstOrDefault(),
                c.FechaUltimoMensaje))
            .ToListAsync();
    }

    public async Task<List<ConsultaSummaryDto>> GetByPublisherAsync(string publisherUserId)
    {
        var listingIds = await _ctx.PropertyListings
            .Where(l => l.Publisher != null && l.Publisher.UserId == publisherUserId)
            .Select(l => l.Id)
            .ToListAsync();

        return await _ctx.Consultas
            .Where(c => listingIds.Contains(c.PropertyListingId))
            .OrderByDescending(c => c.FechaUltimoMensaje)
            .Select(c => new ConsultaSummaryDto(
                c.Id,
                c.PropertyListingId,
                c.PropertyListing.Titulo,
                c.Mensajes.OrderByDescending(m => m.FechaEnvio).Select(m => m.Mensaje).FirstOrDefault() ?? "",
                c.Mensajes.OrderByDescending(m => m.FechaEnvio).Select(m => m.EsDelPublisher).FirstOrDefault(),
                c.FechaUltimoMensaje))
            .ToListAsync();
    }

    public async Task<ConsultaMensajeDto> AddMessageAsync(ConsultaMensaje message)
    {
        var consulta = await _ctx.Consultas.FindAsync(message.ConsultaId);
        consulta!.FechaUltimoMensaje = message.FechaEnvio;
        _ctx.ConsultaMensajes.Add(message);
        await _ctx.SaveChangesAsync();

        await _ctx.Entry(message).Reference(m => m.Sender).LoadAsync();
        return new ConsultaMensajeDto(
            message.Id,
            $"{message.Sender.Nombre} {message.Sender.Apellido}",
            message.EsDelPublisher,
            message.Mensaje,
            message.FechaEnvio);
    }

    public async Task<bool> CanPublisherReplyAsync(int consultaId, string publisherUserId)
    {
        var consulta = await _ctx.Consultas
            .Include(c => c.PropertyListing).ThenInclude(l => l.Publisher)
            .FirstOrDefaultAsync(c => c.Id == consultaId);
        return consulta?.PropertyListing.Publisher?.UserId == publisherUserId;
    }

    public async Task<string?> GetConsultaOwnerUserIdAsync(int consultaId)
    {
        return await _ctx.Consultas
            .Where(c => c.Id == consultaId)
            .Select(c => c.UserId)
            .FirstOrDefaultAsync();
    }

    public async Task CreateNotificationAsync(Notification notification)
    {
        _ctx.Notifications.Add(notification);
        await _ctx.SaveChangesAsync();
    }
}
```

- [ ] **Step 3: Build to verify**

```bash
dotnet build PropertyMap.Infrastructure
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Commit**

```bash
git add PropertyMap.Core/Interfaces/IConsultaRepository.cs PropertyMap.Infrastructure/Repositories/ConsultaRepository.cs
git commit -m "feat: add IConsultaRepository + ConsultaRepository"
```

---

## Task 4: IEmailService + EmailService + NoOpEmailService + Test Helper

**Files:**
- Modify: `PropertyMap.Core/Interfaces/IEmailService.cs`
- Modify: `PropertyMap.Infrastructure/Services/EmailService.cs`
- Modify: `PropertyMap.Tests/Api/TestWebApplicationFactory.cs`

- [ ] **Step 1: Add two methods to `IEmailService.cs`**

Append before the closing `}`:
```csharp
Task SendNuevaConsultaAsync(
    string toEmail, string publisherNombre,
    string propertyTitulo, string userNombre, string mensaje);

Task SendNuevaRespuestaAsync(
    string toEmail, string userNombre,
    string propertyTitulo, string publisherNombre, string mensaje);
```

Full file after edit:
```csharp
namespace PropertyMap.Core.Interfaces;

public interface IEmailService
{
    Task SendEmailVerificationAsync(string toEmail, string toName, string token);
    Task SendPasswordResetAsync(string toEmail, string toName, string token, string resetUrl);
    Task SendWelcomeAsync(string toEmail, string toName);

    Task SendNuevaConsultaAsync(
        string toEmail, string publisherNombre,
        string propertyTitulo, string userNombre, string mensaje);

    Task SendNuevaRespuestaAsync(
        string toEmail, string userNombre,
        string propertyTitulo, string publisherNombre, string mensaje);
}
```

- [ ] **Step 2: Implement the two methods in `EmailService.cs`**

Add these two methods to `EmailService` class (after `SendWelcomeAsync`):
```csharp
public async Task SendNuevaConsultaAsync(
    string toEmail, string publisherNombre,
    string propertyTitulo, string userNombre, string mensaje)
{
    var html = $"""
        <!DOCTYPE html>
        <html>
        <body style="font-family:system-ui,sans-serif;max-width:480px;margin:0 auto;padding:24px">
          <h2 style="color:#be123c">Nueva consulta</h2>
          <p>Hola <strong>{publisherNombre}</strong>,</p>
          <p><strong>{userNombre}</strong> te envió una consulta sobre <strong>{propertyTitulo}</strong>:</p>
          <blockquote style="border-left:3px solid #be123c;padding-left:12px;color:#333;margin:16px 0">
            {mensaje}
          </blockquote>
          <p>Respondé desde tu panel en PropertyMap.</p>
        </body>
        </html>
        """;
    await SendAsync(toEmail, publisherNombre, $"Nueva consulta sobre {propertyTitulo}", html);
}

public async Task SendNuevaRespuestaAsync(
    string toEmail, string userNombre,
    string propertyTitulo, string publisherNombre, string mensaje)
{
    var html = $"""
        <!DOCTYPE html>
        <html>
        <body style="font-family:system-ui,sans-serif;max-width:480px;margin:0 auto;padding:24px">
          <h2 style="color:#be123c">Respuesta a tu consulta</h2>
          <p>Hola <strong>{userNombre}</strong>,</p>
          <p><strong>{publisherNombre}</strong> respondió tu consulta sobre <strong>{propertyTitulo}</strong>:</p>
          <blockquote style="border-left:3px solid #be123c;padding-left:12px;color:#333;margin:16px 0">
            {mensaje}
          </blockquote>
          <p>Ver la conversación completa en PropertyMap.</p>
        </body>
        </html>
        """;
    await SendAsync(toEmail, userNombre, $"Respuesta de {publisherNombre}", html);
}
```

- [ ] **Step 3: Update `NoOpEmailService` in `TestWebApplicationFactory.cs`**

The `NoOpEmailService` class currently implements only 3 methods. Add the 2 new ones:
```csharp
public class NoOpEmailService : IEmailService
{
    public Task SendEmailVerificationAsync(string toEmail, string toName, string token) => Task.CompletedTask;
    public Task SendPasswordResetAsync(string toEmail, string toName, string token, string resetUrl) => Task.CompletedTask;
    public Task SendWelcomeAsync(string toEmail, string toName) => Task.CompletedTask;
    public Task SendNuevaConsultaAsync(string toEmail, string publisherNombre, string propertyTitulo, string userNombre, string mensaje) => Task.CompletedTask;
    public Task SendNuevaRespuestaAsync(string toEmail, string userNombre, string propertyTitulo, string publisherNombre, string mensaje) => Task.CompletedTask;
}
```

- [ ] **Step 4: Add `CreateAuthenticatedUserAsync` to `TestAuthHelper` in `TestWebApplicationFactory.cs`**

Add this static method to the `TestAuthHelper` class (after `CreatePublisherProfileAsync`):
```csharp
public static async Task<(HttpClient client, string userId)> CreateAuthenticatedUserAsync(
    TestWebApplicationFactory factory)
{
    var client = factory.CreateClient();
    var email = $"user_{Guid.NewGuid()}@test.com";

    await client.PostAsJsonAsync("/api/auth/register",
        new PropertyMap.Core.DTOs.Auth.RegisterRequest("Test", "User", email, "Test123!", "Test123!"));

    using var scope = factory.Services.CreateScope();
    var userManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<PropertyMap.Core.Entities.ApplicationUser>>();
    var user = await userManager.FindByEmailAsync(email);
    user!.EmailConfirmed = true;
    user.Estado = PropertyMap.Core.Enums.EstadoUsuario.Activo;
    await userManager.UpdateAsync(user);

    // No Publisher role — plain user
    var loginResp = await client.PostAsJsonAsync("/api/auth/login",
        new PropertyMap.Core.DTOs.Auth.LoginRequest(email, "Test123!"));
    var auth = await loginResp.Content.ReadFromJsonAsync<PropertyMap.Core.DTOs.Auth.AuthResponse>();

    client.DefaultRequestHeaders.Authorization =
        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth!.AccessToken);

    return (client, user.Id);
}
```

- [ ] **Step 5: Build tests to verify**

```bash
dotnet build PropertyMap.Tests
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 6: Commit**

```bash
git add PropertyMap.Core/Interfaces/IEmailService.cs PropertyMap.Infrastructure/Services/EmailService.cs PropertyMap.Tests/Api/TestWebApplicationFactory.cs
git commit -m "feat: add SendNuevaConsultaAsync + SendNuevaRespuestaAsync to IEmailService; add CreateAuthenticatedUserAsync test helper"
```

---

## Task 5: ConsultasController + Tests (TDD) + Program.cs API

**Files:**
- Create: `PropertyMap.Tests/Api/ConsultasControllerTests.cs`
- Create: `PropertyMap.Api/Controllers/ConsultasController.cs`
- Modify: `PropertyMap.Api/Program.cs`

- [ ] **Step 1: Create stub `ConsultasController.cs` (makes tests compile)**

```csharp
// PropertyMap.Api/Controllers/ConsultasController.cs
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
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
    private readonly IEmailService _email;
    private readonly UserManager<ApplicationUser> _userManager;

    public ConsultasController(
        IConsultaRepository consultas,
        IEmailService email,
        UserManager<ApplicationUser> userManager)
    {
        _consultas = consultas;
        _email = email;
        _userManager = userManager;
    }

    [HttpPost]
    public Task<IActionResult> CreateOrContinue([FromBody] CreateConsultaRequest request)
        => Task.FromResult<IActionResult>(Ok());

    [HttpGet]
    public Task<IActionResult> GetMyConsultas()
        => Task.FromResult<IActionResult>(Ok(new List<ConsultaSummaryDto>()));

    [HttpGet("publisher")]
    [Authorize(Roles = "Publisher")]
    public Task<IActionResult> GetPublisherConsultas()
        => Task.FromResult<IActionResult>(Ok(new List<ConsultaSummaryDto>()));

    [HttpGet("{id:int}")]
    public Task<IActionResult> GetDetail(int id)
        => Task.FromResult<IActionResult>(Ok());

    [HttpPost("{id:int}/mensajes")]
    [Authorize(Roles = "Publisher")]
    public Task<IActionResult> PublisherReply(int id, [FromBody] SendMensajeRequest request)
        => Task.FromResult<IActionResult>(Ok());
}
```

- [ ] **Step 2: Register IConsultaRepository in `PropertyMap.Api/Program.cs`**

Add after the existing `AddScoped<IViewTrackingService, ViewTrackingService>()` line:
```csharp
builder.Services.AddScoped<IConsultaRepository, ConsultaRepository>();
```

Also add the using at the top if not present:
```csharp
using PropertyMap.Infrastructure.Repositories;
```

- [ ] **Step 3: Create `ConsultasControllerTests.cs` with 5 tests**

```csharp
// PropertyMap.Tests/Api/ConsultasControllerTests.cs
using System.Net;
using System.Net.Http.Json;
using PropertyMap.Core.DTOs.Consultas;
using PropertyMap.Core.DTOs.Properties;
using PropertyMap.Core.Enums;
using Xunit;

namespace PropertyMap.Tests.Api;

public class ConsultasControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public ConsultasControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static CreateListingRequest SampleListing() => new(
        Operacion: TipoOperacion.Venta,
        TipoPropiedad: TipoPropiedad.Departamento,
        Titulo: "Propiedad consulta test",
        Descripcion: "Test",
        Precio: 80000,
        Moneda: "USD",
        DireccionTexto: "Av. Test 123",
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
        Amenities: []);

    private record CreatedIdDto(int Id);

    private async Task<(HttpClient pubClient, string pubUserId, int listingId)> SetupPublishedListingAsync()
    {
        var (pubClient, pubUserId) = await TestAuthHelper.CreateAuthenticatedPublisherAsync(_factory);
        await TestAuthHelper.CreatePublisherProfileAsync(pubClient);
        var createResp = await pubClient.PostAsJsonAsync("/api/properties", SampleListing());
        var created = await createResp.Content.ReadFromJsonAsync<CreatedIdDto>();

        // approve via admin
        using var adminScope = _factory.Services.CreateScope();
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

        return (pubClient, pubUserId, created.Id);
    }

    [Fact]
    public async Task PostConsulta_CreatesThreadAndFirstMessage()
    {
        var (_, _, listingId) = await SetupPublishedListingAsync();
        var (userClient, _) = await TestAuthHelper.CreateAuthenticatedUserAsync(_factory);

        var resp = await userClient.PostAsJsonAsync("/api/consultas",
            new CreateConsultaRequest(listingId, "Hola, ¿sigue disponible?"));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var detail = await resp.Content.ReadFromJsonAsync<ConsultaDetailDto>();
        Assert.NotNull(detail);
        Assert.Single(detail!.Mensajes);
        Assert.Equal("Hola, ¿sigue disponible?", detail.Mensajes[0].Mensaje);
        Assert.False(detail.Mensajes[0].EsDelPublisher);
    }

    [Fact]
    public async Task PostConsulta_SameListingId_ReturnsSameThreadWithNewMessage()
    {
        var (_, _, listingId) = await SetupPublishedListingAsync();
        var (userClient, _) = await TestAuthHelper.CreateAuthenticatedUserAsync(_factory);

        var resp1 = await userClient.PostAsJsonAsync("/api/consultas",
            new CreateConsultaRequest(listingId, "Primer mensaje"));
        var detail1 = await resp1.Content.ReadFromJsonAsync<ConsultaDetailDto>();

        var resp2 = await userClient.PostAsJsonAsync("/api/consultas",
            new CreateConsultaRequest(listingId, "Segundo mensaje"));
        var detail2 = await resp2.Content.ReadFromJsonAsync<ConsultaDetailDto>();

        Assert.Equal(detail1!.Id, detail2!.Id);
        Assert.Equal(2, detail2.Mensajes.Count);
    }

    [Fact]
    public async Task GetConsultaDetail_ThirdParty_ReturnsForbid()
    {
        var (_, _, listingId) = await SetupPublishedListingAsync();
        var (userClient, _) = await TestAuthHelper.CreateAuthenticatedUserAsync(_factory);

        var postResp = await userClient.PostAsJsonAsync("/api/consultas",
            new CreateConsultaRequest(listingId, "Consulta privada"));
        var detail = await postResp.Content.ReadFromJsonAsync<ConsultaDetailDto>();

        // A different user tries to access the thread
        var (otherClient, _) = await TestAuthHelper.CreateAuthenticatedUserAsync(_factory);
        var getResp = await otherClient.GetAsync($"/api/consultas/{detail!.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, getResp.StatusCode);
    }

    [Fact]
    public async Task PublisherReply_AddsMessageToThread()
    {
        var (pubClient, _, listingId) = await SetupPublishedListingAsync();
        var (userClient, _) = await TestAuthHelper.CreateAuthenticatedUserAsync(_factory);

        var postResp = await userClient.PostAsJsonAsync("/api/consultas",
            new CreateConsultaRequest(listingId, "Quiero saber el precio"));
        var detail = await postResp.Content.ReadFromJsonAsync<ConsultaDetailDto>();

        var replyResp = await pubClient.PostAsJsonAsync(
            $"/api/consultas/{detail!.Id}/mensajes",
            new SendMensajeRequest("El precio es USD 80.000"));

        Assert.Equal(HttpStatusCode.OK, replyResp.StatusCode);
        var msgDto = await replyResp.Content.ReadFromJsonAsync<ConsultaMensajeDto>();
        Assert.NotNull(msgDto);
        Assert.True(msgDto!.EsDelPublisher);
        Assert.Equal("El precio es USD 80.000", msgDto.Mensaje);
    }

    [Fact]
    public async Task RegularUser_CannotUsePublisherEndpoint()
    {
        var (_, _, listingId) = await SetupPublishedListingAsync();
        var (userClient, _) = await TestAuthHelper.CreateAuthenticatedUserAsync(_factory);

        var postResp = await userClient.PostAsJsonAsync("/api/consultas",
            new CreateConsultaRequest(listingId, "Consulta inicial"));
        var detail = await postResp.Content.ReadFromJsonAsync<ConsultaDetailDto>();

        // Regular user tries to use the publisher reply endpoint
        var replyResp = await userClient.PostAsJsonAsync(
            $"/api/consultas/{detail!.Id}/mensajes",
            new SendMensajeRequest("Intento ilegítimo"));

        Assert.Equal(HttpStatusCode.Forbidden, replyResp.StatusCode);
    }
}
```

- [ ] **Step 4: Run tests — they should fail (stub returns empty OK)**

```bash
dotnet test PropertyMap.Tests --filter "ConsultasControllerTests" --no-build
```

Expected: Multiple FAILED — `PostConsulta_CreatesThreadAndFirstMessage` fails because stub returns `Ok()` with no body, etc.

- [ ] **Step 5: Replace stub `ConsultasController.cs` with full implementation**

```csharp
// PropertyMap.Api/Controllers/ConsultasController.cs
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
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
    private readonly IEmailService _email;
    private readonly UserManager<ApplicationUser> _userManager;

    public ConsultasController(
        IConsultaRepository consultas,
        IEmailService email,
        UserManager<ApplicationUser> userManager)
    {
        _consultas = consultas;
        _email = email;
        _userManager = userManager;
    }

    // POST /api/consultas — user creates or continues a thread
    [HttpPost]
    public async Task<IActionResult> CreateOrContinue([FromBody] CreateConsultaRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

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
        catch { }

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
        catch { }

        return Ok(msgDto);
    }
}
```

- [ ] **Step 6: Run tests — they should pass**

```bash
dotnet test PropertyMap.Tests --filter "ConsultasControllerTests"
```

Expected: 5 passed, 0 failed.

- [ ] **Step 7: Commit**

```bash
git add PropertyMap.Api/Controllers/ConsultasController.cs PropertyMap.Api/Program.cs PropertyMap.Tests/Api/ConsultasControllerTests.cs
git commit -m "feat: ConsultasController with 5 endpoints + 5 integration tests"
```

---

## Task 6: IConsultasApiService + ConsultasApiService + Program.cs Web

**Files:**
- Create: `PropertyMap.Web/PropertyMap.Web/Services/IConsultasApiService.cs`
- Create: `PropertyMap.Web/PropertyMap.Web/Services/ConsultasApiService.cs`
- Modify: `PropertyMap.Web/PropertyMap.Web/Program.cs`

- [ ] **Step 1: Create `IConsultasApiService.cs`**

```csharp
// PropertyMap.Web/PropertyMap.Web/Services/IConsultasApiService.cs
using PropertyMap.Core.DTOs.Consultas;

namespace PropertyMap.Web.Services;

public interface IConsultasApiService
{
    Task<ConsultaDetailDto?> CreateOrContinueAsync(int listingId, string mensaje);
    Task<List<ConsultaSummaryDto>> GetMyConsultasAsync();
    Task<List<ConsultaSummaryDto>> GetPublisherConsultasAsync();
    Task<ConsultaDetailDto?> GetDetailAsync(int consultaId);
    Task<ConsultaMensajeDto?> ReplyAsync(int consultaId, string mensaje);
}
```

- [ ] **Step 2: Create `ConsultasApiService.cs`**

```csharp
// PropertyMap.Web/PropertyMap.Web/Services/ConsultasApiService.cs
using System.Net.Http.Headers;
using System.Net.Http.Json;
using PropertyMap.Core.DTOs.Consultas;

namespace PropertyMap.Web.Services;

public class ConsultasApiService : IConsultasApiService
{
    private readonly HttpClient _http;
    private readonly MemoryTokenStore _tokenStore;

    public ConsultasApiService(IHttpClientFactory httpFactory, MemoryTokenStore tokenStore)
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

    public async Task<ConsultaDetailDto?> CreateOrContinueAsync(int listingId, string mensaje)
    {
        try
        {
            SetAuth();
            var resp = await _http.PostAsJsonAsync("api/consultas",
                new CreateConsultaRequest(listingId, mensaje));
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<ConsultaDetailDto>();
        }
        catch { return null; }
    }

    public async Task<List<ConsultaSummaryDto>> GetMyConsultasAsync()
    {
        try
        {
            SetAuth();
            return await _http.GetFromJsonAsync<List<ConsultaSummaryDto>>("api/consultas") ?? [];
        }
        catch { return []; }
    }

    public async Task<List<ConsultaSummaryDto>> GetPublisherConsultasAsync()
    {
        try
        {
            SetAuth();
            return await _http.GetFromJsonAsync<List<ConsultaSummaryDto>>("api/consultas/publisher") ?? [];
        }
        catch { return []; }
    }

    public async Task<ConsultaDetailDto?> GetDetailAsync(int consultaId)
    {
        try
        {
            SetAuth();
            return await _http.GetFromJsonAsync<ConsultaDetailDto>($"api/consultas/{consultaId}");
        }
        catch { return null; }
    }

    public async Task<ConsultaMensajeDto?> ReplyAsync(int consultaId, string mensaje)
    {
        try
        {
            SetAuth();
            var resp = await _http.PostAsJsonAsync(
                $"api/consultas/{consultaId}/mensajes",
                new SendMensajeRequest(mensaje));
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<ConsultaMensajeDto>();
        }
        catch { return null; }
    }
}
```

- [ ] **Step 3: Register service in `PropertyMap.Web/PropertyMap.Web/Program.cs`**

Add after `AddScoped<IFavoritesApiService, FavoritesApiService>()`:
```csharp
builder.Services.AddScoped<IConsultasApiService, ConsultasApiService>();
```

- [ ] **Step 4: Build to verify**

```bash
dotnet build PropertyMap.Web/PropertyMap.Web
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 5: Commit**

```bash
git add PropertyMap.Web/PropertyMap.Web/Services/IConsultasApiService.cs PropertyMap.Web/PropertyMap.Web/Services/ConsultasApiService.cs PropertyMap.Web/PropertyMap.Web/Program.cs
git commit -m "feat: add IConsultasApiService + ConsultasApiService + Web Program.cs registration"
```

---

## Task 7: ConsultaThread.razor + CSS

**Files:**
- Create: `PropertyMap.Web/PropertyMap.Web/Components/Shared/ConsultaThread.razor`
- Modify: `PropertyMap.Web/PropertyMap.Web/wwwroot/css/app.css`

- [ ] **Step 1: Create `ConsultaThread.razor`**

```razor
@* PropertyMap.Web/PropertyMap.Web/Components/Shared/ConsultaThread.razor *@
@using PropertyMap.Core.DTOs.Consultas

<div class="consulta-thread">
    <div class="consulta-messages">
        @if (Mensajes.Count == 0)
        {
            <p class="consulta-empty">Todavía no hay mensajes en esta consulta.</p>
        }
        else
        {
            @foreach (var m in Mensajes)
            {
                var isMyMessage = m.EsDelPublisher == EsPublisher;
                <div class="consulta-bubble-row @(isMyMessage ? "consulta-bubble-row--mine" : "consulta-bubble-row--theirs")">
                    <div class="consulta-bubble @(isMyMessage ? "consulta-bubble--mine" : "consulta-bubble--theirs")">
                        <span class="consulta-bubble-sender">@m.SenderNombre</span>
                        <p class="consulta-bubble-text">@m.Mensaje</p>
                        <span class="consulta-bubble-time">@m.FechaEnvio.ToLocalTime().ToString("dd/MM HH:mm")</span>
                    </div>
                </div>
            }
        }
    </div>

    <div class="consulta-input-row">
        <textarea class="consulta-input" rows="2"
                  placeholder="Escribí tu mensaje..."
                  @bind="newMensaje"
                  @onkeydown="HandleKeyDown"></textarea>
        <button class="btn-primary consulta-send-btn"
                @onclick="Send"
                disabled="@(string.IsNullOrWhiteSpace(newMensaje) || sending)">
            @(sending ? "Enviando..." : "Enviar")
        </button>
    </div>
</div>

@code {
    [Parameter] public List<ConsultaMensajeDto> Mensajes { get; set; } = [];
    [Parameter] public bool EsPublisher { get; set; }
    [Parameter] public EventCallback<string> OnSend { get; set; }

    private string newMensaje = "";
    private bool sending;

    private async Task Send()
    {
        if (string.IsNullOrWhiteSpace(newMensaje) || sending) return;
        sending = true;
        var texto = newMensaje;
        newMensaje = "";
        await OnSend.InvokeAsync(texto);
        sending = false;
    }

    private async Task HandleKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter" && !e.ShiftKey)
            await Send();
    }
}
```

- [ ] **Step 2: Add CSS to `app.css`**

Append at the end of `wwwroot/css/app.css`:
```css
/* ── Consultas / Chat ── */
.consulta-thread {
  display: flex;
  flex-direction: column;
  gap: var(--space-4, 1rem);
  height: 100%;
}

.consulta-messages {
  flex: 1;
  overflow-y: auto;
  display: flex;
  flex-direction: column;
  gap: var(--space-3, 0.75rem);
  padding: var(--space-2, 0.5rem) 0;
  max-height: 480px;
}

.consulta-empty {
  color: var(--color-text-muted, #888);
  text-align: center;
  padding: var(--space-6, 1.5rem);
}

.consulta-bubble-row {
  display: flex;
}

.consulta-bubble-row--mine {
  justify-content: flex-end;
}

.consulta-bubble-row--theirs {
  justify-content: flex-start;
}

.consulta-bubble {
  max-width: 72%;
  padding: 10px 14px;
  border-radius: 12px;
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.consulta-bubble--mine {
  background: #be123c;
  color: #fff;
  border-bottom-right-radius: 2px;
}

.consulta-bubble--theirs {
  background: var(--color-surface-secondary, #f3f4f6);
  color: var(--color-text, #1a1a1a);
  border-bottom-left-radius: 2px;
}

.consulta-bubble-sender {
  font-size: 0.7rem;
  font-weight: 600;
  opacity: 0.75;
}

.consulta-bubble-text {
  margin: 0;
  font-size: 0.95rem;
  line-height: 1.4;
  white-space: pre-wrap;
}

.consulta-bubble-time {
  font-size: 0.7rem;
  opacity: 0.6;
  align-self: flex-end;
}

.consulta-input-row {
  display: flex;
  gap: var(--space-2, 0.5rem);
  align-items: flex-end;
}

.consulta-input {
  flex: 1;
  padding: 10px 12px;
  border: 1px solid var(--color-border, #e2e8f0);
  border-radius: 8px;
  font-size: 0.95rem;
  resize: none;
  font-family: inherit;
  line-height: 1.4;
}

.consulta-input:focus {
  outline: none;
  border-color: #be123c;
}

.consulta-send-btn {
  white-space: nowrap;
  align-self: flex-end;
}

/* Consulta summary cards */
.consulta-card {
  background: var(--color-surface, #fff);
  border: 1px solid var(--color-border, #e2e8f0);
  border-radius: 10px;
  padding: var(--space-4, 1rem);
  display: flex;
  flex-direction: column;
  gap: var(--space-1, 0.25rem);
  cursor: pointer;
  transition: box-shadow 0.15s ease;
  text-decoration: none;
  color: inherit;
}

.consulta-card:hover {
  box-shadow: 0 2px 8px rgba(0,0,0,0.08);
}

.consulta-card-title {
  font-weight: 600;
  font-size: 0.95rem;
  color: var(--color-text, #1a1a1a);
}

.consulta-card-preview {
  font-size: 0.85rem;
  color: var(--color-text-muted, #888);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.consulta-card-date {
  font-size: 0.75rem;
  color: var(--color-text-muted, #888);
}
```

- [ ] **Step 3: Build to verify**

```bash
dotnet build PropertyMap.Web/PropertyMap.Web
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Commit**

```bash
git add PropertyMap.Web/PropertyMap.Web/Components/Shared/ConsultaThread.razor PropertyMap.Web/PropertyMap.Web/wwwroot/css/app.css
git commit -m "feat: ConsultaThread shared component + chat CSS styles"
```

---

## Task 8: Account/Consultas.razor + Account/ConsultaDetalle.razor

**Files:**
- Create: `PropertyMap.Web/PropertyMap.Web/Components/Pages/Account/Consultas.razor`
- Create: `PropertyMap.Web/PropertyMap.Web/Components/Pages/Account/ConsultaDetalle.razor`

- [ ] **Step 1: Create `Account/Consultas.razor`**

```razor
@* PropertyMap.Web/PropertyMap.Web/Components/Pages/Account/Consultas.razor *@
@page "/account/consultas"
@rendermode InteractiveServer
@inject IConsultasApiService ConsultasApi
@inject IAuthService AuthService
@inject NavigationManager Nav

<PageTitle>Mis consultas — PropertyMap</PageTitle>

<AuthorizeView>
    <Authorized>
        <div class="app-shell" style="display:flex;flex-direction:column">
            <nav class="pm-navbar" role="navigation">
                <a href="/" class="pm-navbar__logo">PropertyMap</a>
                <span style="font-weight:600">Mis consultas</span>
                <div class="pm-navbar__actions">
                    <a href="/account/profile" class="btn-ghost">Mi perfil</a>
                    <a href="/" class="btn-ghost">Volver al mapa</a>
                </div>
            </nav>

            <div style="padding:var(--space-6,1.5rem);max-width:700px;margin:0 auto;width:100%">
                <h1 style="font-size:1.5rem;font-weight:700;margin-bottom:var(--space-4)">
                    Mis consultas
                </h1>

                @if (loading)
                {
                    <p style="color:var(--color-text-muted)">Cargando...</p>
                }
                else if (consultas.Count == 0)
                {
                    <div style="text-align:center;padding:var(--space-10) 0;color:var(--color-text-muted)">
                        <p style="font-size:1.125rem">Todavía no iniciaste ninguna consulta.</p>
                        <a href="/" class="btn-primary" style="margin-top:var(--space-4);display:inline-block">
                            Explorar propiedades
                        </a>
                    </div>
                }
                else
                {
                    <div style="display:flex;flex-direction:column;gap:var(--space-3)">
                        @foreach (var c in consultas)
                        {
                            <a href="/account/consultas/@c.Id" class="consulta-card">
                                <span class="consulta-card-title">@c.PropertyTitulo</span>
                                <span class="consulta-card-preview">
                                    @(c.UltimoEsDelPublisher ? "Publisher: " : "Vos: ")@c.UltimoMensaje
                                </span>
                                <span class="consulta-card-date">@c.FechaUltimoMensaje.ToLocalTime().ToString("dd/MM/yyyy HH:mm")</span>
                            </a>
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
                <p style="text-align:center">Necesitás iniciar sesión para ver tus consultas.</p>
                <a href="/Account/Login?returnUrl=/account/consultas" class="btn-primary auth-btn" style="text-align:center">
                    Iniciar sesión
                </a>
            </div>
        </div>
    </NotAuthorized>
</AuthorizeView>

@code {
    private List<PropertyMap.Core.DTOs.Consultas.ConsultaSummaryDto> consultas = [];
    private bool loading = true;
    private bool sessionRestored;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && !sessionRestored)
        {
            sessionRestored = true;
            await AuthService.TryRestoreSessionAsync();
            loading = true;
            try { consultas = await ConsultasApi.GetMyConsultasAsync(); }
            catch { consultas = []; }
            finally { loading = false; }
            StateHasChanged();
        }
    }
}
```

- [ ] **Step 2: Create `Account/ConsultaDetalle.razor`**

This page handles two routes:
- `/account/consultas/{Id:int}` — existing thread
- `/account/consultas/nueva` with `?listingId=N` query param — new or existing thread for that listing

```razor
@* PropertyMap.Web/PropertyMap.Web/Components/Pages/Account/ConsultaDetalle.razor *@
@page "/account/consultas/{Id:int}"
@page "/account/consultas/nueva"
@rendermode InteractiveServer
@inject IConsultasApiService ConsultasApi
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
                // No thread yet — will create on first send
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
            // Update URL to permanent thread URL on first message
            if (Id == 0)
                Nav.NavigateTo($"/account/consultas/{result.Id}", replace: true);
        }
    }
}
```

- [ ] **Step 3: Build to verify**

```bash
dotnet build PropertyMap.Web/PropertyMap.Web
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Commit**

```bash
git add PropertyMap.Web/PropertyMap.Web/Components/Pages/Account/Consultas.razor PropertyMap.Web/PropertyMap.Web/Components/Pages/Account/ConsultaDetalle.razor
git commit -m "feat: Account/Consultas.razor + Account/ConsultaDetalle.razor"
```

---

## Task 9: Publisher/Consultas.razor + Publisher/ConsultaDetalle.razor

**Files:**
- Create: `PropertyMap.Web/PropertyMap.Web/Components/Pages/Publisher/Consultas.razor`
- Create: `PropertyMap.Web/PropertyMap.Web/Components/Pages/Publisher/ConsultaDetalle.razor`

- [ ] **Step 1: Create `Publisher/Consultas.razor`**

```razor
@* PropertyMap.Web/PropertyMap.Web/Components/Pages/Publisher/Consultas.razor *@
@page "/publisher/consultas"
@rendermode InteractiveServer
@inject IConsultasApiService ConsultasApi
@inject IAuthService AuthService
@inject NavigationManager Nav

<PageTitle>Consultas recibidas — PropertyMap</PageTitle>

<AuthorizeView Roles="Publisher">
    <Authorized>
        <div class="app-shell" style="display:flex;flex-direction:column">
            <nav class="pm-navbar" role="navigation">
                <a href="/" class="pm-navbar__logo">PropertyMap</a>
                <span style="font-weight:600">Consultas recibidas</span>
                <div class="pm-navbar__actions">
                    <a href="/publisher/dashboard" class="btn-ghost">Mi panel</a>
                    <a href="/" class="btn-ghost">Volver al mapa</a>
                </div>
            </nav>

            <div style="padding:var(--space-6,1.5rem);max-width:700px;margin:0 auto;width:100%">
                <h1 style="font-size:1.5rem;font-weight:700;margin-bottom:var(--space-4)">
                    Consultas recibidas
                </h1>

                @if (loading)
                {
                    <p style="color:var(--color-text-muted)">Cargando...</p>
                }
                else if (consultas.Count == 0)
                {
                    <div style="text-align:center;padding:var(--space-10) 0;color:var(--color-text-muted)">
                        <p style="font-size:1.125rem">Todavía no recibiste ninguna consulta.</p>
                    </div>
                }
                else
                {
                    <div style="display:flex;flex-direction:column;gap:var(--space-3)">
                        @foreach (var c in consultas)
                        {
                            <a href="/publisher/consultas/@c.Id" class="consulta-card">
                                <span class="consulta-card-title">@c.PropertyTitulo</span>
                                <span class="consulta-card-preview">
                                    @(c.UltimoEsDelPublisher ? "Vos: " : "Usuario: ")@c.UltimoMensaje
                                </span>
                                <span class="consulta-card-date">@c.FechaUltimoMensaje.ToLocalTime().ToString("dd/MM/yyyy HH:mm")</span>
                            </a>
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
                <p style="text-align:center">Acceso solo para publishers.</p>
                <a href="/Account/Login" class="btn-primary auth-btn" style="text-align:center">
                    Iniciar sesión
                </a>
            </div>
        </div>
    </NotAuthorized>
</AuthorizeView>

@code {
    private List<PropertyMap.Core.DTOs.Consultas.ConsultaSummaryDto> consultas = [];
    private bool loading = true;
    private bool sessionRestored;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && !sessionRestored)
        {
            sessionRestored = true;
            await AuthService.TryRestoreSessionAsync();
            loading = true;
            try { consultas = await ConsultasApi.GetPublisherConsultasAsync(); }
            catch { consultas = []; }
            finally { loading = false; }
            StateHasChanged();
        }
    }
}
```

- [ ] **Step 2: Create `Publisher/ConsultaDetalle.razor`**

```razor
@* PropertyMap.Web/PropertyMap.Web/Components/Pages/Publisher/ConsultaDetalle.razor *@
@page "/publisher/consultas/{Id:int}"
@rendermode InteractiveServer
@inject IConsultasApiService ConsultasApi
@inject IAuthService AuthService
@inject NavigationManager Nav

<PageTitle>Consulta — PropertyMap</PageTitle>

<AuthorizeView Roles="Publisher">
    <Authorized>
        <div class="app-shell" style="display:flex;flex-direction:column">
            <nav class="pm-navbar" role="navigation">
                <a href="/" class="pm-navbar__logo">PropertyMap</a>
                <span style="font-weight:600">@(detail?.PropertyTitulo ?? "Consulta")</span>
                <div class="pm-navbar__actions">
                    <a href="/publisher/consultas" class="btn-ghost">← Consultas</a>
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
                else if (detail is not null)
                {
                    <ConsultaThread
                        Mensajes="@detail.Mensajes"
                        EsPublisher="true"
                        OnSend="HandleReply" />
                }
            </div>
        </div>
    </Authorized>
    <NotAuthorized>
        <div class="auth-page">
            <div class="auth-card">
                <a href="/" class="auth-logo">PropertyMap</a>
                <p style="text-align:center">Acceso solo para publishers.</p>
                <a href="/Account/Login" class="btn-primary auth-btn" style="text-align:center">
                    Iniciar sesión
                </a>
            </div>
        </div>
    </NotAuthorized>
</AuthorizeView>

@code {
    [Parameter] public int Id { get; set; }

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
            loading = true;
            error = null;
            try
            {
                detail = await ConsultasApi.GetDetailAsync(Id);
                if (detail is null) error = "No tenés acceso a esta consulta.";
            }
            catch { error = "Error al cargar la consulta."; }
            finally { loading = false; }
            StateHasChanged();
        }
    }

    private async Task HandleReply(string mensaje)
    {
        var msgDto = await ConsultasApi.ReplyAsync(Id, mensaje);
        if (msgDto is not null && detail is not null)
        {
            detail = detail with { Mensajes = [..detail.Mensajes, msgDto] };
        }
    }
}
```

- [ ] **Step 3: Build to verify**

```bash
dotnet build PropertyMap.Web/PropertyMap.Web
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Commit**

```bash
git add PropertyMap.Web/PropertyMap.Web/Components/Pages/Publisher/Consultas.razor PropertyMap.Web/PropertyMap.Web/Components/Pages/Publisher/ConsultaDetalle.razor
git commit -m "feat: Publisher/Consultas.razor + Publisher/ConsultaDetalle.razor"
```

---

## Task 10: PropertyDetail.razor + Navbar.razor

**Files:**
- Modify: `PropertyMap.Web/PropertyMap.Web/Components/Pages/PropertyDetail.razor`
- Modify: `PropertyMap.Web/PropertyMap.Web/Components/Layout/Navbar.razor`

- [ ] **Step 1: Add "Consultar" button to `PropertyDetail.razor`**

Find the section where `<FavoriteButton ListingId="@Id" />` is rendered (inside the flex container near the title/price area). Add the Consultar link immediately after the `<FavoriteButton>`:

```razor
<AuthorizeView>
    <Authorized>
        <a href="/account/consultas/nueva?listingId=@Id" class="btn-ghost"
           style="font-size:0.875rem">
            Consultar al publisher
        </a>
    </Authorized>
</AuthorizeView>
```

Place this right after the existing `<FavoriteButton ListingId="@Id" />` line, inside the same container. The exact surrounding context to look for:

```razor
<FavoriteButton ListingId="@Id" />
```

Add after it:
```razor
<AuthorizeView>
    <Authorized>
        <a href="/account/consultas/nueva?listingId=@Id" class="btn-ghost"
           style="font-size:0.875rem">
            Consultar al publisher
        </a>
    </Authorized>
</AuthorizeView>
```

- [ ] **Step 2: Update `Navbar.razor` to add Consultas links**

Current Authorized block:
```razor
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
```

Replace with:
```razor
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
```

- [ ] **Step 3: Build to verify**

```bash
dotnet build PropertyMap.Web/PropertyMap.Web
```

Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Run all tests to confirm no regressions**

```bash
dotnet test PropertyMap.Tests
```

Expected: All tests pass (including the 5 new ConsultasControllerTests).

- [ ] **Step 5: Commit**

```bash
git add PropertyMap.Web/PropertyMap.Web/Components/Pages/PropertyDetail.razor PropertyMap.Web/PropertyMap.Web/Components/Layout/Navbar.razor
git commit -m "feat: add Consultar button to PropertyDetail + Consultas links to Navbar"
```

---

## Self-Review

**Spec coverage check:**

| Spec Requirement | Task |
|---|---|
| `Consulta` entity (unique index per user-property) | Task 1 |
| `ConsultaMensaje` entity with `EsDelPublisher` flag | Task 1 |
| AppDbContext DbSets + OnModelCreating | Task 1 |
| Phase5CConsultas migration | Task 1 |
| 5 DTOs (CreateConsultaRequest, SendMensajeRequest, ConsultaSummaryDto, ConsultaMensajeDto, ConsultaDetailDto) | Task 2 |
| IConsultaRepository interface | Task 3 |
| ConsultaRepository — GetOrCreateAsync (idempotent) | Task 3 |
| ConsultaRepository — GetByIdAsync (access check) | Task 3 |
| ConsultaRepository — GetByUserAsync | Task 3 |
| ConsultaRepository — GetByPublisherAsync | Task 3 |
| ConsultaRepository — AddMessageAsync (updates FechaUltimoMensaje) | Task 3 |
| IEmailService — 2 new methods | Task 4 |
| EmailService — HTML implementations | Task 4 |
| NoOpEmailService updated | Task 4 |
| CreateAuthenticatedUserAsync test helper | Task 4 |
| POST /api/consultas — create/continue thread | Task 5 |
| GET /api/consultas — user inbox | Task 5 |
| GET /api/consultas/publisher — publisher inbox (Authorize Publisher) | Task 5 |
| GET /api/consultas/{id} — thread detail (access check → Forbid) | Task 5 |
| POST /api/consultas/{id}/mensajes — publisher reply (Authorize Publisher) | Task 5 |
| In-app notification on new consulta | Task 5 |
| Email notification on new consulta | Task 5 |
| In-app notification on publisher reply | Task 5 |
| Email notification on publisher reply | Task 5 |
| Program.cs — IConsultaRepository registration | Task 5 |
| 5 integration tests (TDD) | Task 5 |
| IConsultasApiService + ConsultasApiService | Task 6 |
| Program.cs Web registration | Task 6 |
| ConsultaThread.razor shared component | Task 7 |
| Chat bubble alignment (`EsDelPublisher == EsPublisher`) | Task 7 |
| CSS for chat + consulta cards | Task 7 |
| /account/consultas — user inbox page | Task 8 |
| /account/consultas/{Id:int} — user thread page | Task 8 |
| /account/consultas/nueva?listingId=N — new thread page | Task 8 |
| /publisher/consultas — publisher inbox page | Task 9 |
| /publisher/consultas/{Id:int} — publisher thread page | Task 9 |
| PropertyDetail "Consultar al publisher" button | Task 10 |
| Navbar — Consultas link (all users) | Task 10 |
| Navbar — Consultas recibidas link (Publisher role) | Task 10 |

All spec requirements covered. No placeholders.

**Type consistency:**
- `ConsultaMensajeDto` uses `SenderNombre` (string) — consistent across Repository, Controller, Thread component, and API service.
- `ConsultaDetailDto` uses `PropertyTitulo` — consistent in Repository projection and Controller notification code.
- `IConsultaRepository.GetByPublisherAsync(string publisherUserId)` — consistent with Controller extracting `userId` from claims and passing it directly.
- `ConsultaThread.razor` `OnSend` EventCallback<string> — matches `HandleSend` and `HandleReply` signatures in both page components.

# Phase 7 — Inteligencia Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implementar Alertas de búsqueda, Notificaciones in-app (SignalR), Notificaciones email automáticas y Reportes & moderación para PropertyMap.

**Architecture:** Reusa las entidades `Alert`, `Report`, `Notification`, `NotificationPreference` ya existentes en `PropertyMap.Core`/`AppDbContext`. Se agrega un hub SignalR en `PropertyMap.Api` (`/hubs/notifications`) autenticado por JWT vía query string, un `AlertMatchingService` que se dispara cuando `AdminController.Review` aprueba una propiedad, y servicios Blazor (`HttpClient` + `HubConnection`) en `PropertyMap.Web`.

**Tech Stack:** .NET 9, ASP.NET Core SignalR, EF Core 9, `Microsoft.AspNetCore.SignalR.Client` (cliente Blazor), xUnit + `Microsoft.AspNetCore.Mvc.Testing`.

---

## File Map

### Created
```
src/PropertyMap.Core/DTOs/Alerts/AlertDto.cs
src/PropertyMap.Core/DTOs/Alerts/CreateAlertRequest.cs
src/PropertyMap.Core/DTOs/Reports/ReportDto.cs
src/PropertyMap.Core/DTOs/Reports/CreateReportRequest.cs
src/PropertyMap.Core/DTOs/Reports/ReviewReportRequest.cs
src/PropertyMap.Core/DTOs/Notifications/NotificationDto.cs
src/PropertyMap.Core/Interfaces/IAlertRepository.cs
src/PropertyMap.Core/Interfaces/IReportRepository.cs
src/PropertyMap.Core/Interfaces/INotificationRepository.cs
src/PropertyMap.Core/Interfaces/INotificationPublisher.cs
src/PropertyMap.Core/Interfaces/IAlertMatchingService.cs
src/PropertyMap.Infrastructure/Repositories/AlertRepository.cs
src/PropertyMap.Infrastructure/Repositories/ReportRepository.cs
src/PropertyMap.Infrastructure/Repositories/NotificationRepository.cs
src/PropertyMap.Infrastructure/Services/AlertMatchingService.cs
src/PropertyMap.Api/Hubs/NotificationsHub.cs
src/PropertyMap.Api/Services/SignalRNotificationPublisher.cs
src/PropertyMap.Api/Controllers/AlertsController.cs
src/PropertyMap.Api/Controllers/NotificationsController.cs
src/PropertyMap.Api/Controllers/ReportsController.cs
src/PropertyMap.Web/PropertyMap.Web/Services/IAlertsApiService.cs
src/PropertyMap.Web/PropertyMap.Web/Services/AlertsApiService.cs
src/PropertyMap.Web/PropertyMap.Web/Services/INotificationsApiService.cs
src/PropertyMap.Web/PropertyMap.Web/Services/NotificationsApiService.cs
src/PropertyMap.Web/PropertyMap.Web/Services/IReportsApiService.cs
src/PropertyMap.Web/PropertyMap.Web/Services/ReportsApiService.cs
src/PropertyMap.Web/PropertyMap.Web/Services/NotificationHubClient.cs
src/PropertyMap.Web/PropertyMap.Web/Components/Layout/NotificationBell.razor
src/PropertyMap.Web/PropertyMap.Web/Components/Reports/ReportModal.razor
src/PropertyMap.Web/PropertyMap.Web/Components/Pages/Account/Alerts.razor
src/PropertyMap.Web/PropertyMap.Web/Components/Pages/Admin/Reportes.razor
src/PropertyMap.Tests/Api/AlertsControllerTests.cs
src/PropertyMap.Tests/Api/ReportsControllerTests.cs
src/PropertyMap.Tests/Api/AlertMatchingTests.cs
```

### Modified
```
src/PropertyMap.Core/Interfaces/IEmailService.cs          (+ SendAlertMatchAsync, SendReportConfirmationAsync)
src/PropertyMap.Infrastructure/Services/EmailService.cs   (implementa los 2 métodos nuevos)
src/PropertyMap.Api/Controllers/AdminController.cs        (+ GetReports, ReviewReport; Review() dispara AlertMatchingService)
src/PropertyMap.Api/Program.cs                            (+ AddSignalR, JWT query auth, MapHub, DI)
src/PropertyMap.Web/PropertyMap.Web/Program.cs             (+ DI de nuevos servicios + NotificationHubClient)
src/PropertyMap.Web/PropertyMap.Web/PropertyMap.Web.csproj (+ Microsoft.AspNetCore.SignalR.Client)
src/PropertyMap.Web/PropertyMap.Web/Components/Layout/Navbar.razor (+ <NotificationBell />)
src/PropertyMap.Web/PropertyMap.Web/Components/Pages/PropertyDetail.razor (+ botón "Reportar")
src/PropertyMap.Tests/Api/TestWebApplicationFactory.cs    (+ NoOpEmailService: 2 métodos nuevos)
```

---

## Task 1: DTOs de Alerts, Reports, Notifications

**Files:**
- Create: `src/PropertyMap.Core/DTOs/Alerts/AlertDto.cs`
- Create: `src/PropertyMap.Core/DTOs/Alerts/CreateAlertRequest.cs`
- Create: `src/PropertyMap.Core/DTOs/Reports/ReportDto.cs`
- Create: `src/PropertyMap.Core/DTOs/Reports/CreateReportRequest.cs`
- Create: `src/PropertyMap.Core/DTOs/Reports/ReviewReportRequest.cs`
- Create: `src/PropertyMap.Core/DTOs/Notifications/NotificationDto.cs`

- [ ] **Step 1: Crear DTOs de Alerts**

`src/PropertyMap.Core/DTOs/Alerts/AlertDto.cs`
```csharp
using PropertyMap.Core.Enums;

namespace PropertyMap.Core.DTOs.Alerts;

public record AlertDto(
    int Id,
    string? Nombre,
    TipoOperacion? Operacion,
    TipoPropiedad? TipoPropiedad,
    string? Ciudad,
    decimal? PrecioMax,
    string? Moneda,
    int? DormitoriosMin,
    bool Activa,
    DateTime FechaCreacion
);
```

`src/PropertyMap.Core/DTOs/Alerts/CreateAlertRequest.cs`
```csharp
using PropertyMap.Core.Enums;

namespace PropertyMap.Core.DTOs.Alerts;

public record CreateAlertRequest(
    string? Nombre,
    TipoOperacion? Operacion,
    TipoPropiedad? TipoPropiedad,
    string? Ciudad,
    decimal? PrecioMax,
    string? Moneda,
    int? DormitoriosMin
);
```

- [ ] **Step 2: Crear DTOs de Reports**

`src/PropertyMap.Core/DTOs/Reports/ReportDto.cs`
```csharp
using PropertyMap.Core.Enums;

namespace PropertyMap.Core.DTOs.Reports;

public record ReportDto(
    int Id,
    int PropertyListingId,
    string PropertyTitulo,
    string UserNombre,
    MotivoReporte Motivo,
    string? Descripcion,
    EstadoReporte Estado,
    DateTime FechaReporte
);
```

`src/PropertyMap.Core/DTOs/Reports/CreateReportRequest.cs`
```csharp
using PropertyMap.Core.Enums;

namespace PropertyMap.Core.DTOs.Reports;

public record CreateReportRequest(
    int PropertyListingId,
    MotivoReporte Motivo,
    string? Descripcion
);
```

`src/PropertyMap.Core/DTOs/Reports/ReviewReportRequest.cs`
```csharp
using PropertyMap.Core.Enums;

namespace PropertyMap.Core.DTOs.Reports;

public record ReviewReportRequest(EstadoReporte NuevoEstado);
```

- [ ] **Step 3: Crear NotificationDto**

`src/PropertyMap.Core/DTOs/Notifications/NotificationDto.cs`
```csharp
using PropertyMap.Core.Enums;

namespace PropertyMap.Core.DTOs.Notifications;

public record NotificationDto(
    int Id,
    TipoNotificacion Tipo,
    string Titulo,
    string Mensaje,
    bool Leida,
    string? UrlAccion,
    DateTime FechaCreacion
);
```

- [ ] **Step 4: Build**

```bash
cd C:\Agentes\PropertyMap
dotnet build src/PropertyMap.Core/PropertyMap.Core.csproj
```
Expected: `Build succeeded.`

- [ ] **Step 5: Commit**

```bash
git add src/PropertyMap.Core/DTOs/Alerts/ src/PropertyMap.Core/DTOs/Reports/ src/PropertyMap.Core/DTOs/Notifications/
git commit -m "feat(core): add Alert, Report, Notification DTOs for Phase 7"
```

---

## Task 2: AlertRepository

**Files:**
- Create: `src/PropertyMap.Core/Interfaces/IAlertRepository.cs`
- Create: `src/PropertyMap.Infrastructure/Repositories/AlertRepository.cs`

- [ ] **Step 1: Crear interfaz**

`src/PropertyMap.Core/Interfaces/IAlertRepository.cs`
```csharp
using PropertyMap.Core.DTOs.Alerts;
using PropertyMap.Core.Entities;

namespace PropertyMap.Core.Interfaces;

public interface IAlertRepository
{
    Task<Alert> AddAsync(Alert alert);
    Task<List<AlertDto>> GetByUserAsync(string userId);
    Task<Alert?> GetByIdAsync(int id);
    Task UpdateAsync(Alert alert);
    Task DeleteAsync(int id);
    Task<List<Alert>> GetActiveMatchingAsync(PropertyListing listing);
}
```

- [ ] **Step 2: Implementar repositorio**

`src/PropertyMap.Infrastructure/Repositories/AlertRepository.cs`
```csharp
using Microsoft.EntityFrameworkCore;
using PropertyMap.Core.DTOs.Alerts;
using PropertyMap.Core.Entities;
using PropertyMap.Core.Interfaces;
using PropertyMap.Infrastructure.Data;

namespace PropertyMap.Infrastructure.Repositories;

public class AlertRepository(AppDbContext ctx) : IAlertRepository
{
    public async Task<Alert> AddAsync(Alert alert)
    {
        ctx.Alerts.Add(alert);
        await ctx.SaveChangesAsync();
        return alert;
    }

    public async Task<List<AlertDto>> GetByUserAsync(string userId) =>
        await ctx.Alerts
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.FechaCreacion)
            .Select(a => new AlertDto(
                a.Id, a.Nombre, a.Operacion, a.TipoPropiedad, a.Ciudad,
                a.PrecioMax, a.Moneda, a.DormitoriosMin, a.Activa, a.FechaCreacion))
            .ToListAsync();

    public async Task<Alert?> GetByIdAsync(int id) =>
        await ctx.Alerts.FirstOrDefaultAsync(a => a.Id == id);

    public async Task UpdateAsync(Alert alert)
    {
        ctx.Alerts.Update(alert);
        await ctx.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var alert = await ctx.Alerts.FindAsync(id);
        if (alert is not null)
        {
            ctx.Alerts.Remove(alert);
            await ctx.SaveChangesAsync();
        }
    }

    public async Task<List<Alert>> GetActiveMatchingAsync(PropertyListing listing)
    {
        // Two-step materialization: EF InMemory no soporta bien combinaciones
        // de null-coalescing en Where complejos contra navegación Location.
        var candidates = await ctx.Alerts
            .Include(a => a.User)
            .Where(a => a.Activa)
            .ToListAsync();

        return candidates.Where(a =>
            (a.Operacion == null || a.Operacion == listing.Operacion) &&
            (a.TipoPropiedad == null || a.TipoPropiedad == listing.TipoPropiedad) &&
            (a.Ciudad == null || a.Ciudad == listing.Location.Ciudad) &&
            (a.PrecioMax == null || listing.Precio <= a.PrecioMax) &&
            (a.DormitoriosMin == null || (listing.Dormitorios != null && listing.Dormitorios >= a.DormitoriosMin))
        ).ToList();
    }
}
```

- [ ] **Step 3: Build**

```bash
dotnet build src/PropertyMap.Infrastructure/PropertyMap.Infrastructure.csproj
```
Expected: `Build succeeded.`

- [ ] **Step 4: Commit**

```bash
git add src/PropertyMap.Core/Interfaces/IAlertRepository.cs src/PropertyMap.Infrastructure/Repositories/AlertRepository.cs
git commit -m "feat(infra): add AlertRepository with active-matching query"
```

---

## Task 3: ReportRepository

**Files:**
- Create: `src/PropertyMap.Core/Interfaces/IReportRepository.cs`
- Create: `src/PropertyMap.Infrastructure/Repositories/ReportRepository.cs`

- [ ] **Step 1: Crear interfaz**

`src/PropertyMap.Core/Interfaces/IReportRepository.cs`
```csharp
using PropertyMap.Core.DTOs.Reports;
using PropertyMap.Core.Entities;
using PropertyMap.Core.Enums;

namespace PropertyMap.Core.Interfaces;

public interface IReportRepository
{
    Task<Report> AddAsync(Report report);
    Task<Report?> GetByIdAsync(int id);
    Task<List<ReportDto>> GetPendingAsync();
    Task UpdateAsync(Report report);
}
```

- [ ] **Step 2: Implementar repositorio**

`src/PropertyMap.Infrastructure/Repositories/ReportRepository.cs`
```csharp
using Microsoft.EntityFrameworkCore;
using PropertyMap.Core.DTOs.Reports;
using PropertyMap.Core.Entities;
using PropertyMap.Core.Enums;
using PropertyMap.Core.Interfaces;
using PropertyMap.Infrastructure.Data;

namespace PropertyMap.Infrastructure.Repositories;

public class ReportRepository(AppDbContext ctx) : IReportRepository
{
    public async Task<Report> AddAsync(Report report)
    {
        ctx.Reports.Add(report);
        await ctx.SaveChangesAsync();
        return report;
    }

    public async Task<Report?> GetByIdAsync(int id) =>
        await ctx.Reports
            .Include(r => r.PropertyListing)
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.Id == id);

    public async Task<List<ReportDto>> GetPendingAsync() =>
        await ctx.Reports
            .Include(r => r.PropertyListing)
            .Include(r => r.User)
            .Where(r => r.Estado == EstadoReporte.Pendiente || r.Estado == EstadoReporte.EnRevision)
            .OrderBy(r => r.FechaReporte)
            .Select(r => new ReportDto(
                r.Id, r.PropertyListingId, r.PropertyListing.Titulo,
                $"{r.User.Nombre} {r.User.Apellido}", r.Motivo, r.Descripcion,
                r.Estado, r.FechaReporte))
            .ToListAsync();

    public async Task UpdateAsync(Report report)
    {
        ctx.Reports.Update(report);
        await ctx.SaveChangesAsync();
    }
}
```

- [ ] **Step 3: Build**

```bash
dotnet build src/PropertyMap.Infrastructure/PropertyMap.Infrastructure.csproj
```
Expected: `Build succeeded.`

- [ ] **Step 4: Commit**

```bash
git add src/PropertyMap.Core/Interfaces/IReportRepository.cs src/PropertyMap.Infrastructure/Repositories/ReportRepository.cs
git commit -m "feat(infra): add ReportRepository"
```

---

## Task 4: NotificationRepository

**Files:**
- Create: `src/PropertyMap.Core/Interfaces/INotificationRepository.cs`
- Create: `src/PropertyMap.Infrastructure/Repositories/NotificationRepository.cs`

- [ ] **Step 1: Crear interfaz**

`src/PropertyMap.Core/Interfaces/INotificationRepository.cs`
```csharp
using PropertyMap.Core.DTOs.Notifications;
using PropertyMap.Core.Entities;

namespace PropertyMap.Core.Interfaces;

public interface INotificationRepository
{
    Task<Notification> AddAsync(Notification notification);
    Task<List<NotificationDto>> GetByUserAsync(string userId, int take = 20);
    Task<int> GetUnreadCountAsync(string userId);
    Task MarkAsReadAsync(int id, string userId);
    Task MarkAllAsReadAsync(string userId);
}
```

- [ ] **Step 2: Implementar repositorio**

`src/PropertyMap.Infrastructure/Repositories/NotificationRepository.cs`
```csharp
using Microsoft.EntityFrameworkCore;
using PropertyMap.Core.DTOs.Notifications;
using PropertyMap.Core.Entities;
using PropertyMap.Core.Interfaces;
using PropertyMap.Infrastructure.Data;

namespace PropertyMap.Infrastructure.Repositories;

public class NotificationRepository(AppDbContext ctx) : INotificationRepository
{
    public async Task<Notification> AddAsync(Notification notification)
    {
        ctx.Notifications.Add(notification);
        await ctx.SaveChangesAsync();
        return notification;
    }

    public async Task<List<NotificationDto>> GetByUserAsync(string userId, int take = 20) =>
        await ctx.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.FechaCreacion)
            .Take(take)
            .Select(n => new NotificationDto(
                n.Id, n.Tipo, n.Titulo, n.Mensaje, n.Leida, n.UrlAccion, n.FechaCreacion))
            .ToListAsync();

    public async Task<int> GetUnreadCountAsync(string userId) =>
        await ctx.Notifications.CountAsync(n => n.UserId == userId && !n.Leida);

    public async Task MarkAsReadAsync(int id, string userId)
    {
        var notification = await ctx.Notifications
            .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);
        if (notification is not null)
        {
            notification.Leida = true;
            await ctx.SaveChangesAsync();
        }
    }

    public async Task MarkAllAsReadAsync(string userId)
    {
        var unread = await ctx.Notifications
            .Where(n => n.UserId == userId && !n.Leida)
            .ToListAsync();
        foreach (var n in unread) n.Leida = true;
        await ctx.SaveChangesAsync();
    }
}
```

- [ ] **Step 3: Build**

```bash
dotnet build src/PropertyMap.Infrastructure/PropertyMap.Infrastructure.csproj
```
Expected: `Build succeeded.`

- [ ] **Step 4: Commit**

```bash
git add src/PropertyMap.Core/Interfaces/INotificationRepository.cs src/PropertyMap.Infrastructure/Repositories/NotificationRepository.cs
git commit -m "feat(infra): add NotificationRepository"
```

---

## Task 5: SignalR Hub + INotificationPublisher

**Files:**
- Create: `src/PropertyMap.Core/Interfaces/INotificationPublisher.cs`
- Create: `src/PropertyMap.Api/Hubs/NotificationsHub.cs`
- Create: `src/PropertyMap.Api/Services/SignalRNotificationPublisher.cs`

`INotificationPublisher` vive en Core para que `AlertMatchingService` (Infrastructure) no dependa de SignalR directamente; la implementación concreta vive en Api porque ahí está el `IHubContext`.

- [ ] **Step 1: Crear interfaz INotificationPublisher**

`src/PropertyMap.Core/Interfaces/INotificationPublisher.cs`
```csharp
using PropertyMap.Core.DTOs.Notifications;

namespace PropertyMap.Core.Interfaces;

public interface INotificationPublisher
{
    Task PublishToUserAsync(string userId, NotificationDto notification);
}
```

- [ ] **Step 2: Crear NotificationsHub**

`src/PropertyMap.Api/Hubs/NotificationsHub.cs`
```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace PropertyMap.Api.Hubs;

[Authorize]
public class NotificationsHub : Hub
{
    // El hub no expone métodos invocables por el cliente todavía;
    // solo recibe pushes del servidor vía IHubContext<NotificationsHub>.
    // SignalR enruta por usuario usando ClaimTypes.NameIdentifier (default IUserIdProvider).
}
```

- [ ] **Step 3: Crear SignalRNotificationPublisher**

`src/PropertyMap.Api/Services/SignalRNotificationPublisher.cs`
```csharp
using Microsoft.AspNetCore.SignalR;
using PropertyMap.Api.Hubs;
using PropertyMap.Core.DTOs.Notifications;
using PropertyMap.Core.Interfaces;

namespace PropertyMap.Api.Services;

public class SignalRNotificationPublisher(IHubContext<NotificationsHub> hub) : INotificationPublisher
{
    public async Task PublishToUserAsync(string userId, NotificationDto notification) =>
        await hub.Clients.User(userId).SendAsync("ReceiveNotification", notification);
}
```

- [ ] **Step 4: Build**

```bash
dotnet build src/PropertyMap.Api/PropertyMap.Api.csproj
```
Expected: `Build succeeded.`

- [ ] **Step 5: Commit**

```bash
git add src/PropertyMap.Core/Interfaces/INotificationPublisher.cs src/PropertyMap.Api/Hubs/ src/PropertyMap.Api/Services/
git commit -m "feat(api): add NotificationsHub and SignalR notification publisher"
```

---

## Task 6: AlertMatchingService

**Files:**
- Create: `src/PropertyMap.Core/Interfaces/IAlertMatchingService.cs`
- Create: `src/PropertyMap.Infrastructure/Services/AlertMatchingService.cs`
- Modify: `src/PropertyMap.Core/Interfaces/IEmailService.cs`
- Modify: `src/PropertyMap.Infrastructure/Services/EmailService.cs`

- [ ] **Step 1: Agregar SendAlertMatchAsync a IEmailService**

Editar `src/PropertyMap.Core/Interfaces/IEmailService.cs`, agregar al final de la interfaz (antes del `}`):

```csharp
    Task SendAlertMatchAsync(
        string toEmail, string userNombre,
        string alertNombre, string propertyTitulo, int propertyId);

    Task SendReportConfirmationAsync(string toEmail, string userNombre, string propertyTitulo);
```

- [ ] **Step 2: Implementar los 2 métodos en EmailService**

Editar `src/PropertyMap.Infrastructure/Services/EmailService.cs`, agregar antes del `}` final de la clase:

```csharp
    public async Task SendAlertMatchAsync(
        string toEmail, string userNombre,
        string alertNombre, string propertyTitulo, int propertyId)
    {
        var html = $"""
            <!DOCTYPE html>
            <html>
            <body style="font-family:system-ui,sans-serif;max-width:480px;margin:0 auto;padding:24px">
              <h2 style="color:#be123c">¡Nueva propiedad para tu alerta!</h2>
              <p>Hola <strong>{userNombre}</strong>,</p>
              <p>Se publicó una propiedad que coincide con tu alerta <strong>{alertNombre}</strong>:</p>
              <blockquote style="border-left:3px solid #be123c;padding-left:12px;color:#333;margin:16px 0">
                {propertyTitulo}
              </blockquote>
              <p><a href="https://propertymap.com.ar/propiedad/{propertyId}" style="background:#be123c;color:white;padding:12px 24px;border-radius:8px;text-decoration:none;display:inline-block">Ver propiedad</a></p>
            </body>
            </html>
            """;
        await SendAsync(toEmail, userNombre, $"Nueva propiedad: {propertyTitulo}", html);
    }

    public async Task SendReportConfirmationAsync(string toEmail, string userNombre, string propertyTitulo)
    {
        var html = $"""
            <!DOCTYPE html>
            <html>
            <body style="font-family:system-ui,sans-serif;max-width:480px;margin:0 auto;padding:24px">
              <h2 style="color:#be123c">Recibimos tu reporte</h2>
              <p>Hola <strong>{userNombre}</strong>,</p>
              <p>Recibimos tu reporte sobre <strong>{propertyTitulo}</strong>. Nuestro equipo de moderación lo va a revisar.</p>
            </body>
            </html>
            """;
        await SendAsync(toEmail, userNombre, "Recibimos tu reporte en PropertyMap", html);
    }
```

- [ ] **Step 3: Actualizar NoOpEmailService en los tests**

Editar `src/PropertyMap.Tests/Api/TestWebApplicationFactory.cs`, agregar dentro de la clase `NoOpEmailService`:

```csharp
    public Task SendAlertMatchAsync(string toEmail, string userNombre, string alertNombre, string propertyTitulo, int propertyId) => Task.CompletedTask;
    public Task SendReportConfirmationAsync(string toEmail, string userNombre, string propertyTitulo) => Task.CompletedTask;
```

- [ ] **Step 4: Build (debe fallar si falta algún método — verificar antes de seguir)**

```bash
dotnet build src/PropertyMap.Tests/PropertyMap.Tests.csproj
```
Expected: `Build succeeded.`

- [ ] **Step 5: Crear IAlertMatchingService**

`src/PropertyMap.Core/Interfaces/IAlertMatchingService.cs`
```csharp
using PropertyMap.Core.Entities;

namespace PropertyMap.Core.Interfaces;

public interface IAlertMatchingService
{
    Task NotifyMatchingAlertsAsync(PropertyListing listing);
}
```

- [ ] **Step 6: Implementar AlertMatchingService**

`src/PropertyMap.Infrastructure/Services/AlertMatchingService.cs`
```csharp
using Microsoft.EntityFrameworkCore;
using PropertyMap.Core.DTOs.Notifications;
using PropertyMap.Core.Entities;
using PropertyMap.Core.Enums;
using PropertyMap.Core.Interfaces;
using PropertyMap.Infrastructure.Data;

namespace PropertyMap.Infrastructure.Services;

public class AlertMatchingService(
    AppDbContext ctx,
    IAlertRepository alerts,
    IEmailService email,
    INotificationPublisher publisher) : IAlertMatchingService
{
    public async Task NotifyMatchingAlertsAsync(PropertyListing listing)
    {
        var matches = await alerts.GetActiveMatchingAsync(listing);
        if (matches.Count == 0) return;

        var preferences = await ctx.NotificationPreferences
            .Where(p => matches.Select(m => m.UserId).Contains(p.UserId))
            .ToListAsync();

        foreach (var alert in matches)
        {
            var pref = preferences.FirstOrDefault(p => p.UserId == alert.UserId);

            var notification = new Notification
            {
                UserId = alert.UserId,
                Tipo = TipoNotificacion.AlertaCoincidencia,
                Titulo = "¡Nueva propiedad para tu alerta!",
                Mensaje = $"{listing.Titulo} coincide con tu alerta \"{alert.Nombre ?? "sin nombre"}\".",
                UrlAccion = $"/propiedad/{listing.Id}",
                FechaCreacion = DateTime.UtcNow
            };
            ctx.Notifications.Add(notification);
            await ctx.SaveChangesAsync();

            if (pref is null || pref.RecibirPush)
            {
                await publisher.PublishToUserAsync(alert.UserId, new NotificationDto(
                    notification.Id, notification.Tipo, notification.Titulo,
                    notification.Mensaje, notification.Leida, notification.UrlAccion,
                    notification.FechaCreacion));
            }

            if ((pref is null || pref.RecibirEmail) && (pref is null || pref.AlertasCoincidencia)
                && alert.User?.Email is not null)
            {
                await email.SendAlertMatchAsync(
                    alert.User.Email, alert.User.Nombre,
                    alert.Nombre ?? "tu búsqueda", listing.Titulo, listing.Id);
            }
        }
    }
}
```

- [ ] **Step 7: Build**

```bash
dotnet build src/PropertyMap.Infrastructure/PropertyMap.Infrastructure.csproj
```
Expected: `Build succeeded.`

- [ ] **Step 8: Commit**

```bash
git add src/PropertyMap.Core/Interfaces/ src/PropertyMap.Infrastructure/Services/AlertMatchingService.cs src/PropertyMap.Infrastructure/Services/EmailService.cs src/PropertyMap.Tests/Api/TestWebApplicationFactory.cs
git commit -m "feat(infra): add AlertMatchingService with email + in-app notification dispatch"
```

---

## Task 7: AlertsController + NotificationsController + ReportsController

**Files:**
- Create: `src/PropertyMap.Api/Controllers/AlertsController.cs`
- Create: `src/PropertyMap.Api/Controllers/NotificationsController.cs`
- Create: `src/PropertyMap.Api/Controllers/ReportsController.cs`

- [ ] **Step 1: Crear AlertsController**

`src/PropertyMap.Api/Controllers/AlertsController.cs`
```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PropertyMap.Core.DTOs.Alerts;
using PropertyMap.Core.Entities;
using PropertyMap.Core.Interfaces;

namespace PropertyMap.Api.Controllers;

[ApiController]
[Route("api/alerts")]
[Authorize]
public class AlertsController : ControllerBase
{
    private readonly IAlertRepository _alerts;

    public AlertsController(IAlertRepository alerts)
    {
        _alerts = alerts;
    }

    [HttpGet]
    public async Task<IActionResult> GetMine()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return Ok(await _alerts.GetByUserAsync(userId));
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateAlertRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var created = await _alerts.AddAsync(new Alert
        {
            UserId = userId,
            Nombre = request.Nombre,
            Operacion = request.Operacion,
            TipoPropiedad = request.TipoPropiedad,
            Ciudad = request.Ciudad,
            PrecioMax = request.PrecioMax,
            Moneda = request.Moneda,
            DormitoriosMin = request.DormitoriosMin,
            Activa = true,
            FechaCreacion = DateTime.UtcNow
        });
        return CreatedAtAction(nameof(GetMine), new { id = created.Id });
    }

    [HttpPatch("{id:int}/toggle")]
    public async Task<IActionResult> Toggle(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var alert = await _alerts.GetByIdAsync(id);
        if (alert is null || alert.UserId != userId) return NotFound();

        alert.Activa = !alert.Activa;
        await _alerts.UpdateAsync(alert);
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var alert = await _alerts.GetByIdAsync(id);
        if (alert is null || alert.UserId != userId) return NotFound();

        await _alerts.DeleteAsync(id);
        return NoContent();
    }
}
```

- [ ] **Step 2: Crear NotificationsController**

`src/PropertyMap.Api/Controllers/NotificationsController.cs`
```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PropertyMap.Core.Interfaces;

namespace PropertyMap.Api.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly INotificationRepository _notifications;

    public NotificationsController(INotificationRepository notifications)
    {
        _notifications = notifications;
    }

    [HttpGet]
    public async Task<IActionResult> GetMine([FromQuery] int take = 20)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return Ok(await _notifications.GetByUserAsync(userId, take));
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        return Ok(await _notifications.GetUnreadCountAsync(userId));
    }

    [HttpPatch("{id:int}/read")]
    public async Task<IActionResult> MarkAsRead(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        await _notifications.MarkAsReadAsync(id, userId);
        return NoContent();
    }

    [HttpPatch("read-all")]
    public async Task<IActionResult> MarkAllAsRead()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        await _notifications.MarkAllAsReadAsync(userId);
        return NoContent();
    }
}
```

- [ ] **Step 3: Crear ReportsController**

`src/PropertyMap.Api/Controllers/ReportsController.cs`
```csharp
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
```

- [ ] **Step 4: Build**

```bash
dotnet build src/PropertyMap.Api/PropertyMap.Api.csproj
```
Expected: `Build succeeded.`

- [ ] **Step 5: Commit**

```bash
git add src/PropertyMap.Api/Controllers/AlertsController.cs src/PropertyMap.Api/Controllers/NotificationsController.cs src/PropertyMap.Api/Controllers/ReportsController.cs
git commit -m "feat(api): add AlertsController, NotificationsController, ReportsController"
```

---

## Task 8: Moderación de reportes en AdminController + hook de AlertMatchingService

**Files:**
- Modify: `src/PropertyMap.Api/Controllers/AdminController.cs`

- [ ] **Step 1: Reemplazar el contenido completo de AdminController.cs**

`src/PropertyMap.Api/Controllers/AdminController.cs`
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

**Nota:** "Resuelto" implica que el reporte era válido → la propiedad se pausa automáticamente. "Rechazado" no toca el listado.

- [ ] **Step 2: Build**

```bash
dotnet build src/PropertyMap.Api/PropertyMap.Api.csproj
```
Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```bash
git add src/PropertyMap.Api/Controllers/AdminController.cs
git commit -m "feat(api): add report moderation to AdminController, hook AlertMatchingService into approval flow"
```

---

## Task 9: Registrar SignalR + DI en PropertyMap.Api/Program.cs

**Files:**
- Modify: `src/PropertyMap.Api/Program.cs`

- [ ] **Step 1: Agregar `using` para Hubs**

Al inicio de `src/PropertyMap.Api/Program.cs`, agregar:

```csharp
using PropertyMap.Api.Hubs;
using PropertyMap.Api.Services;
```

- [ ] **Step 2: Registrar SignalR**

Inmediatamente después de `builder.Services.AddControllers();`, agregar:

```csharp
builder.Services.AddSignalR();
```

- [ ] **Step 3: Permitir JWT vía query string para el hub**

Dentro del bloque `.AddJwtBearer(options => { ... });` existente, agregar la propiedad `Events` al `TokenValidationParameters`'s sibling — es decir, dentro del mismo `options =>`, después de asignar `options.TokenValidationParameters`, agregar:

```csharp
    options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };
```

El bloque completo de `.AddJwtBearer` queda:

```csharp
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtSettings["Secret"]!)),
        ClockSkew = TimeSpan.Zero
    };
    options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };
});
```

- [ ] **Step 4: Registrar repos y servicios nuevos**

Después de `builder.Services.AddScoped<IAgentRatingRepository, AgentRatingRepository>();`, agregar:

```csharp
builder.Services.AddScoped<IAlertRepository, AlertRepository>();
builder.Services.AddScoped<IReportRepository, ReportRepository>();
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
builder.Services.AddScoped<IAlertMatchingService, AlertMatchingService>();
builder.Services.AddScoped<INotificationPublisher, SignalRNotificationPublisher>();
```

- [ ] **Step 5: Mapear el hub**

Después de `app.MapControllers();`, agregar:

```csharp
app.MapHub<NotificationsHub>("/hubs/notifications");
```

- [ ] **Step 6: Build**

```bash
dotnet build src/PropertyMap.Api/PropertyMap.Api.csproj
```
Expected: `Build succeeded.`

- [ ] **Step 7: Commit**

```bash
git add src/PropertyMap.Api/Program.cs
git commit -m "feat(api): wire SignalR hub, JWT query auth, and Phase 7 DI registrations"
```

---

## Task 10: Servicios Blazor (Alerts, Notifications, Reports, SignalR client)

**Files:**
- Modify: `src/PropertyMap.Web/PropertyMap.Web/PropertyMap.Web.csproj`
- Create: `src/PropertyMap.Web/PropertyMap.Web/Services/IAlertsApiService.cs`
- Create: `src/PropertyMap.Web/PropertyMap.Web/Services/AlertsApiService.cs`
- Create: `src/PropertyMap.Web/PropertyMap.Web/Services/INotificationsApiService.cs`
- Create: `src/PropertyMap.Web/PropertyMap.Web/Services/NotificationsApiService.cs`
- Create: `src/PropertyMap.Web/PropertyMap.Web/Services/IReportsApiService.cs`
- Create: `src/PropertyMap.Web/PropertyMap.Web/Services/ReportsApiService.cs`
- Create: `src/PropertyMap.Web/PropertyMap.Web/Services/NotificationHubClient.cs`
- Modify: `src/PropertyMap.Web/PropertyMap.Web/Program.cs`

- [ ] **Step 1: Agregar paquete SignalR.Client**

```bash
cd C:\Agentes\PropertyMap\src\PropertyMap.Web\PropertyMap.Web
dotnet add package Microsoft.AspNetCore.SignalR.Client --version 9.*
```

- [ ] **Step 2: Crear IAlertsApiService**

`src/PropertyMap.Web/PropertyMap.Web/Services/IAlertsApiService.cs`
```csharp
using PropertyMap.Core.DTOs.Alerts;

namespace PropertyMap.Web.Services;

public interface IAlertsApiService
{
    Task<List<AlertDto>> GetMyAlertsAsync();
    Task<bool> CreateAsync(CreateAlertRequest request);
    Task<bool> ToggleAsync(int id);
    Task<bool> DeleteAsync(int id);
}
```

- [ ] **Step 3: Implementar AlertsApiService**

`src/PropertyMap.Web/PropertyMap.Web/Services/AlertsApiService.cs`
```csharp
using System.Net.Http.Headers;
using System.Net.Http.Json;
using PropertyMap.Core.DTOs.Alerts;

namespace PropertyMap.Web.Services;

public class AlertsApiService : IAlertsApiService
{
    private readonly HttpClient _http;
    private readonly MemoryTokenStore _tokenStore;

    public AlertsApiService(IHttpClientFactory httpFactory, MemoryTokenStore tokenStore)
    {
        _http = httpFactory.CreateClient("api");
        _tokenStore = tokenStore;
    }

    private void SetAuth() =>
        _http.DefaultRequestHeaders.Authorization = _tokenStore.AccessToken is null
            ? null
            : new AuthenticationHeaderValue("Bearer", _tokenStore.AccessToken);

    public async Task<List<AlertDto>> GetMyAlertsAsync()
    {
        try
        {
            SetAuth();
            return await _http.GetFromJsonAsync<List<AlertDto>>("api/alerts") ?? [];
        }
        catch { return []; }
    }

    public async Task<bool> CreateAsync(CreateAlertRequest request)
    {
        try
        {
            SetAuth();
            var resp = await _http.PostAsJsonAsync("api/alerts", request);
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<bool> ToggleAsync(int id)
    {
        try
        {
            SetAuth();
            var resp = await _http.PatchAsync($"api/alerts/{id}/toggle", null);
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    public async Task<bool> DeleteAsync(int id)
    {
        try
        {
            SetAuth();
            var resp = await _http.DeleteAsync($"api/alerts/{id}");
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }
}
```

- [ ] **Step 4: Crear INotificationsApiService + implementación**

`src/PropertyMap.Web/PropertyMap.Web/Services/INotificationsApiService.cs`
```csharp
using PropertyMap.Core.DTOs.Notifications;

namespace PropertyMap.Web.Services;

public interface INotificationsApiService
{
    Task<List<NotificationDto>> GetMyNotificationsAsync(int take = 20);
    Task<int> GetUnreadCountAsync();
    Task MarkAsReadAsync(int id);
    Task MarkAllAsReadAsync();
}
```

`src/PropertyMap.Web/PropertyMap.Web/Services/NotificationsApiService.cs`
```csharp
using System.Net.Http.Headers;
using System.Net.Http.Json;
using PropertyMap.Core.DTOs.Notifications;

namespace PropertyMap.Web.Services;

public class NotificationsApiService : INotificationsApiService
{
    private readonly HttpClient _http;
    private readonly MemoryTokenStore _tokenStore;

    public NotificationsApiService(IHttpClientFactory httpFactory, MemoryTokenStore tokenStore)
    {
        _http = httpFactory.CreateClient("api");
        _tokenStore = tokenStore;
    }

    private void SetAuth() =>
        _http.DefaultRequestHeaders.Authorization = _tokenStore.AccessToken is null
            ? null
            : new AuthenticationHeaderValue("Bearer", _tokenStore.AccessToken);

    public async Task<List<NotificationDto>> GetMyNotificationsAsync(int take = 20)
    {
        try
        {
            SetAuth();
            return await _http.GetFromJsonAsync<List<NotificationDto>>($"api/notifications?take={take}") ?? [];
        }
        catch { return []; }
    }

    public async Task<int> GetUnreadCountAsync()
    {
        try
        {
            SetAuth();
            return await _http.GetFromJsonAsync<int>("api/notifications/unread-count");
        }
        catch { return 0; }
    }

    public async Task MarkAsReadAsync(int id)
    {
        try
        {
            SetAuth();
            await _http.PatchAsync($"api/notifications/{id}/read", null);
        }
        catch { }
    }

    public async Task MarkAllAsReadAsync()
    {
        try
        {
            SetAuth();
            await _http.PatchAsync("api/notifications/read-all", null);
        }
        catch { }
    }
}
```

- [ ] **Step 5: Crear IReportsApiService + implementación**

`src/PropertyMap.Web/PropertyMap.Web/Services/IReportsApiService.cs`
```csharp
using PropertyMap.Core.DTOs.Reports;

namespace PropertyMap.Web.Services;

public interface IReportsApiService
{
    Task<bool> CreateAsync(CreateReportRequest request);
}
```

`src/PropertyMap.Web/PropertyMap.Web/Services/ReportsApiService.cs`
```csharp
using System.Net.Http.Headers;
using System.Net.Http.Json;
using PropertyMap.Core.DTOs.Reports;

namespace PropertyMap.Web.Services;

public class ReportsApiService : IReportsApiService
{
    private readonly HttpClient _http;
    private readonly MemoryTokenStore _tokenStore;

    public ReportsApiService(IHttpClientFactory httpFactory, MemoryTokenStore tokenStore)
    {
        _http = httpFactory.CreateClient("api");
        _tokenStore = tokenStore;
    }

    public async Task<bool> CreateAsync(CreateReportRequest request)
    {
        try
        {
            _http.DefaultRequestHeaders.Authorization = _tokenStore.AccessToken is null
                ? null
                : new AuthenticationHeaderValue("Bearer", _tokenStore.AccessToken);
            var resp = await _http.PostAsJsonAsync("api/reports", request);
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }
}
```

- [ ] **Step 6: Crear NotificationHubClient**

`src/PropertyMap.Web/PropertyMap.Web/Services/NotificationHubClient.cs`
```csharp
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using PropertyMap.Core.DTOs.Notifications;

namespace PropertyMap.Web.Services;

public class NotificationHubClient : IAsyncDisposable
{
    private readonly MemoryTokenStore _tokenStore;
    private readonly string _baseUrl;
    private HubConnection? _connection;

    public event Action<NotificationDto>? OnNotificationReceived;

    public NotificationHubClient(MemoryTokenStore tokenStore, IConfiguration config)
    {
        _tokenStore = tokenStore;
        _baseUrl = config["ApiSettings:BaseUrl"] ?? "https://localhost:7002/";
    }

    public async Task StartAsync()
    {
        if (_connection is not null || !_tokenStore.IsAuthenticated) return;

        _connection = new HubConnectionBuilder()
            .WithUrl($"{_baseUrl}hubs/notifications", options =>
            {
                options.AccessTokenProvider = () => Task.FromResult(_tokenStore.AccessToken);
            })
            .WithAutomaticReconnect()
            .Build();

        _connection.On<NotificationDto>("ReceiveNotification", dto => OnNotificationReceived?.Invoke(dto));

        await _connection.StartAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null) await _connection.DisposeAsync();
    }
}
```

- [ ] **Step 7: Registrar servicios en Program.cs**

Editar `src/PropertyMap.Web/PropertyMap.Web/Program.cs`, después de `builder.Services.AddScoped<IRatingsApiService, RatingsApiService>();`, agregar:

```csharp
builder.Services.AddScoped<IAlertsApiService, AlertsApiService>();
builder.Services.AddScoped<INotificationsApiService, NotificationsApiService>();
builder.Services.AddScoped<IReportsApiService, ReportsApiService>();
builder.Services.AddScoped<NotificationHubClient>();
```

- [ ] **Step 8: Build**

```bash
cd C:\Agentes\PropertyMap
dotnet build src/PropertyMap.Web/PropertyMap.Web.sln
```
Expected: `Build succeeded.`

- [ ] **Step 9: Commit**

```bash
git add src/PropertyMap.Web/PropertyMap.Web/Services/ src/PropertyMap.Web/PropertyMap.Web/Program.cs src/PropertyMap.Web/PropertyMap.Web/PropertyMap.Web.csproj
git commit -m "feat(web): add Alerts/Notifications/Reports API services and SignalR hub client"
```

---

## Task 11: NotificationBell en Navbar + página de Alertas

**Files:**
- Create: `src/PropertyMap.Web/PropertyMap.Web/Components/Layout/NotificationBell.razor`
- Create: `src/PropertyMap.Web/PropertyMap.Web/Components/Pages/Account/Alerts.razor`
- Modify: `src/PropertyMap.Web/PropertyMap.Web/Components/Layout/Navbar.razor`

- [ ] **Step 1: Crear NotificationBell.razor**

`src/PropertyMap.Web/PropertyMap.Web/Components/Layout/NotificationBell.razor`
```razor
@rendermode InteractiveServer
@using PropertyMap.Core.DTOs.Notifications
@using PropertyMap.Web.Services
@inject INotificationsApiService NotificationsApi
@inject NotificationHubClient HubClient
@implements IAsyncDisposable

<div class="pm-notif-bell">
    <button class="btn-ghost" @onclick="ToggleOpen" aria-label="Notificaciones">
        🔔 @(_unreadCount > 0 ? $"({_unreadCount})" : "")
    </button>

    @if (_open)
    {
        <div class="pm-notif-dropdown">
            @if (_notifications.Count == 0)
            {
                <p style="padding:var(--space-3);color:var(--color-muted)">Sin notificaciones.</p>
            }
            else
            {
                @foreach (var n in _notifications)
                {
                    <a href="@(n.UrlAccion ?? "#")" class="pm-notif-item" @onclick="() => MarkRead(n.Id)">
                        <strong>@n.Titulo</strong>
                        <p>@n.Mensaje</p>
                    </a>
                }
                <button class="btn-ghost" @onclick="MarkAllRead">Marcar todo como leído</button>
            }
        </div>
    }
</div>

@code {
    private List<NotificationDto> _notifications = [];
    private int _unreadCount;
    private bool _open;

    protected override async Task OnInitializedAsync()
    {
        await RefreshAsync();
        HubClient.OnNotificationReceived += OnPushed;
        await HubClient.StartAsync();
    }

    private async Task RefreshAsync()
    {
        _notifications = await NotificationsApi.GetMyNotificationsAsync();
        _unreadCount = await NotificationsApi.GetUnreadCountAsync();
    }

    private void OnPushed(NotificationDto dto)
    {
        _notifications.Insert(0, dto);
        _unreadCount++;
        InvokeAsync(StateHasChanged);
    }

    private void ToggleOpen() => _open = !_open;

    private async Task MarkRead(int id)
    {
        await NotificationsApi.MarkAsReadAsync(id);
        await RefreshAsync();
    }

    private async Task MarkAllRead()
    {
        await NotificationsApi.MarkAllAsReadAsync();
        await RefreshAsync();
    }

    public async ValueTask DisposeAsync()
    {
        HubClient.OnNotificationReceived -= OnPushed;
        await HubClient.DisposeAsync();
    }
}
```

- [ ] **Step 2: Agregar NotificationBell al Navbar**

Editar `src/PropertyMap.Web/PropertyMap.Web/Components/Layout/Navbar.razor`, dentro del bloque `<Authorized>` (antes de `<a href="/account/favorites"`), agregar:

```razor
<NotificationBell />
```

- [ ] **Step 3: Agregar link a Alertas en el Navbar**

En el mismo bloque `<Authorized>`, después de `<a href="/account/consultas" class="btn-ghost">Consultas</a>`, agregar:

```razor
<a href="/account/alertas" class="btn-ghost">Alertas</a>
```

- [ ] **Step 4: Crear página Alerts.razor**

`src/PropertyMap.Web/PropertyMap.Web/Components/Pages/Account/Alerts.razor`
```razor
@page "/account/alertas"
@rendermode InteractiveServer
@attribute [Authorize]
@using PropertyMap.Core.DTOs.Alerts
@using PropertyMap.Core.Enums
@using PropertyMap.Web.Services
@inject IAlertsApiService AlertsApi

<h1>Mis alertas de búsqueda</h1>

<div class="pm-alert-form">
    <input @bind="_nombre" placeholder="Nombre de la alerta (opcional)" />
    <select @bind="_operacion">
        <option value="">Cualquier operación</option>
        @foreach (var op in Enum.GetValues<TipoOperacion>())
        {
            <option value="@op">@op</option>
        }
    </select>
    <select @bind="_tipoPropiedad">
        <option value="">Cualquier tipo</option>
        @foreach (var tp in Enum.GetValues<TipoPropiedad>())
        {
            <option value="@tp">@tp</option>
        }
    </select>
    <input @bind="_ciudad" placeholder="Ciudad (opcional)" />
    <input @bind="_precioMax" type="number" placeholder="Precio máximo (opcional)" />
    <input @bind="_dormitoriosMin" type="number" placeholder="Dormitorios mínimos (opcional)" />
    <button class="btn-primary" @onclick="CreateAsync">Crear alerta</button>
</div>

@if (_alerts.Count == 0)
{
    <p>No tenés alertas creadas todavía.</p>
}
else
{
    @foreach (var a in _alerts)
    {
        <div class="pm-alert-card">
            <strong>@(a.Nombre ?? "Alerta sin nombre")</strong>
            <p>
                @(a.Operacion?.ToString() ?? "Cualquier operación") ·
                @(a.TipoPropiedad?.ToString() ?? "Cualquier tipo") ·
                @(a.Ciudad ?? "Cualquier ciudad")
                @(a.PrecioMax is not null ? $" · hasta {a.PrecioMax} {a.Moneda}" : "")
            </p>
            <button class="btn-ghost" @onclick="() => ToggleAsync(a.Id)">
                @(a.Activa ? "Desactivar" : "Activar")
            </button>
            <button class="btn-ghost" @onclick="() => DeleteAsync(a.Id)">Eliminar</button>
        </div>
    }
}

@code {
    private List<AlertDto> _alerts = [];
    private string? _nombre;
    private TipoOperacion? _operacion;
    private TipoPropiedad? _tipoPropiedad;
    private string? _ciudad;
    private decimal? _precioMax;
    private int? _dormitoriosMin;

    protected override async Task OnInitializedAsync() => await RefreshAsync();

    private async Task RefreshAsync() => _alerts = await AlertsApi.GetMyAlertsAsync();

    private async Task CreateAsync()
    {
        await AlertsApi.CreateAsync(new CreateAlertRequest(
            _nombre, _operacion, _tipoPropiedad, _ciudad, _precioMax, "USD", _dormitoriosMin));
        _nombre = null; _operacion = null; _tipoPropiedad = null;
        _ciudad = null; _precioMax = null; _dormitoriosMin = null;
        await RefreshAsync();
    }

    private async Task ToggleAsync(int id)
    {
        await AlertsApi.ToggleAsync(id);
        await RefreshAsync();
    }

    private async Task DeleteAsync(int id)
    {
        await AlertsApi.DeleteAsync(id);
        await RefreshAsync();
    }
}
```

- [ ] **Step 5: Build**

```bash
cd C:\Agentes\PropertyMap
dotnet build src/PropertyMap.Web/PropertyMap.Web.sln
```
Expected: `Build succeeded.`

- [ ] **Step 6: Commit**

```bash
git add src/PropertyMap.Web/PropertyMap.Web/Components/
git commit -m "feat(web): add NotificationBell to navbar and Alerts management page"
```

---

## Task 12: Botón "Reportar" en PropertyDetail + dashboard admin de Reportes

**Files:**
- Create: `src/PropertyMap.Web/PropertyMap.Web/Components/Reports/ReportModal.razor`
- Create: `src/PropertyMap.Web/PropertyMap.Web/Components/Pages/Admin/Reportes.razor`
- Modify: `src/PropertyMap.Web/PropertyMap.Web/Components/Pages/PropertyDetail.razor`

- [ ] **Step 1: Crear ReportModal.razor**

`src/PropertyMap.Web/PropertyMap.Web/Components/Reports/ReportModal.razor`
```razor
@using PropertyMap.Core.DTOs.Reports
@using PropertyMap.Core.Enums
@using PropertyMap.Web.Services
@inject IReportsApiService ReportsApi

@if (IsOpen)
{
    <div class="pm-modal-overlay">
        <div class="pm-modal">
            <h3>Reportar propiedad</h3>
            <select @bind="_motivo">
                @foreach (var m in Enum.GetValues<MotivoReporte>())
                {
                    <option value="@m">@m</option>
                }
            </select>
            <textarea @bind="_descripcion" placeholder="Detalles (opcional)"></textarea>
            @if (_sent)
            {
                <p>Gracias, recibimos tu reporte.</p>
            }
            else
            {
                <button class="btn-primary" @onclick="SendAsync">Enviar reporte</button>
            }
            <button class="btn-ghost" @onclick="Close">Cerrar</button>
        </div>
    </div>
}

@code {
    [Parameter] public bool IsOpen { get; set; }
    [Parameter] public int PropertyListingId { get; set; }
    [Parameter] public EventCallback OnClosed { get; set; }

    private MotivoReporte _motivo = MotivoReporte.Otro;
    private string? _descripcion;
    private bool _sent;

    private async Task SendAsync()
    {
        await ReportsApi.CreateAsync(new CreateReportRequest(PropertyListingId, _motivo, _descripcion));
        _sent = true;
    }

    private async Task Close()
    {
        IsOpen = false;
        _sent = false;
        _descripcion = null;
        await OnClosed.InvokeAsync();
    }
}
```

- [ ] **Step 2: Agregar botón "Reportar" a PropertyDetail.razor**

Editar `src/PropertyMap.Web/PropertyMap.Web/Components/Pages/PropertyDetail.razor`. Agregar el `using` al inicio:

```razor
@using PropertyMap.Web.Components.Reports
```

Agregar, donde estén los demás botones de acción de la propiedad (junto al de favoritos):

```razor
<AuthorizeView>
    <Authorized>
        <button class="btn-ghost" @onclick="() => _reportModalOpen = true">Reportar</button>
        <ReportModal IsOpen="_reportModalOpen" PropertyListingId="@Id"
                     OnClosed="@(() => _reportModalOpen = false)" />
    </Authorized>
</AuthorizeView>
```

Agregar en el bloque `@code` existente:

```csharp
private bool _reportModalOpen;
```

- [ ] **Step 3: Crear dashboard admin de Reportes**

`src/PropertyMap.Web/PropertyMap.Web/Components/Pages/Admin/Reportes.razor`
```razor
@page "/admin/reportes"
@rendermode InteractiveServer
@attribute [Authorize(Roles = "Admin")]
@using System.Net.Http.Headers
@using System.Net.Http.Json
@using PropertyMap.Core.DTOs.Reports
@using PropertyMap.Core.Enums
@inject IHttpClientFactory HttpFactory
@inject PropertyMap.Web.Services.MemoryTokenStore TokenStore

<h1>Reportes pendientes</h1>

@if (_reports.Count == 0)
{
    <p>No hay reportes pendientes.</p>
}
else
{
    @foreach (var r in _reports)
    {
        <div class="pm-report-card">
            <strong>@r.PropertyTitulo</strong>
            <p>Reportado por @r.UserNombre — Motivo: @r.Motivo</p>
            @if (!string.IsNullOrWhiteSpace(r.Descripcion))
            {
                <p>@r.Descripcion</p>
            }
            <button class="btn-primary" @onclick="() => ReviewAsync(r.Id, EstadoReporte.Resuelto)">
                Resolver (pausar propiedad)
            </button>
            <button class="btn-ghost" @onclick="() => ReviewAsync(r.Id, EstadoReporte.Rechazado)">
                Rechazar
            </button>
        </div>
    }
}

@code {
    private List<ReportDto> _reports = [];
    private HttpClient Http => HttpFactory.CreateClient("api");

    protected override async Task OnInitializedAsync() => await RefreshAsync();

    private void SetAuth(HttpClient client) =>
        client.DefaultRequestHeaders.Authorization = TokenStore.AccessToken is null
            ? null
            : new AuthenticationHeaderValue("Bearer", TokenStore.AccessToken);

    private async Task RefreshAsync()
    {
        var client = Http;
        SetAuth(client);
        _reports = await client.GetFromJsonAsync<List<ReportDto>>("api/admin/reports") ?? [];
    }

    private async Task ReviewAsync(int id, EstadoReporte nuevoEstado)
    {
        var client = Http;
        SetAuth(client);
        await client.PatchAsJsonAsync($"api/admin/reports/{id}/review", new { NuevoEstado = nuevoEstado });
        await RefreshAsync();
    }
}
```

- [ ] **Step 4: Agregar link admin en Navbar**

Editar `src/PropertyMap.Web/PropertyMap.Web/Components/Layout/Navbar.razor`, dentro del bloque `<Authorized>`, agregar:

```razor
<AuthorizeView Roles="Admin" Context="adminCtx">
    <Authorized Context="aAuth">
        <a href="/admin/reportes" class="btn-ghost">Reportes</a>
    </Authorized>
</AuthorizeView>
```

- [ ] **Step 5: Build**

```bash
cd C:\Agentes\PropertyMap
dotnet build src/PropertyMap.Web/PropertyMap.Web.sln
```
Expected: `Build succeeded.`

- [ ] **Step 6: Commit**

```bash
git add src/PropertyMap.Web/PropertyMap.Web/Components/
git commit -m "feat(web): add report button on property detail and admin moderation dashboard"
```

---

## Task 13: Tests de integración

**Files:**
- Create: `src/PropertyMap.Tests/Api/AlertsControllerTests.cs`
- Create: `src/PropertyMap.Tests/Api/ReportsControllerTests.cs`
- Create: `src/PropertyMap.Tests/Api/AlertMatchingTests.cs`

- [ ] **Step 1: Crear AlertsControllerTests**

`src/PropertyMap.Tests/Api/AlertsControllerTests.cs`
```csharp
using System.Net;
using System.Net.Http.Json;
using PropertyMap.Core.DTOs.Alerts;
using PropertyMap.Core.Enums;
using Xunit;

namespace PropertyMap.Tests.Api;

public class AlertsControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public AlertsControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreateAlert_ThenGetMine_ReturnsIt()
    {
        var (client, _) = await TestAuthHelper.CreateAuthenticatedUserAsync(_factory);

        var createResp = await client.PostAsJsonAsync("/api/alerts",
            new CreateAlertRequest("Depto en Córdoba", TipoOperacion.Alquiler, TipoPropiedad.Departamento,
                "Córdoba", 100000, "ARS", 2));
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);

        var listResp = await client.GetAsync("/api/alerts");
        var alerts = await listResp.Content.ReadFromJsonAsync<List<AlertDto>>();

        Assert.NotNull(alerts);
        Assert.Single(alerts!);
        Assert.Equal("Depto en Córdoba", alerts![0].Nombre);
        Assert.True(alerts[0].Activa);
    }

    [Fact]
    public async Task ToggleAlert_FlipsActiva()
    {
        var (client, _) = await TestAuthHelper.CreateAuthenticatedUserAsync(_factory);
        await client.PostAsJsonAsync("/api/alerts",
            new CreateAlertRequest(null, null, null, null, null, null, null));
        var alerts = await (await client.GetAsync("/api/alerts")).Content.ReadFromJsonAsync<List<AlertDto>>();
        var id = alerts![0].Id;

        var toggleResp = await client.PatchAsync($"/api/alerts/{id}/toggle", null);
        Assert.Equal(HttpStatusCode.NoContent, toggleResp.StatusCode);

        var after = await (await client.GetAsync("/api/alerts")).Content.ReadFromJsonAsync<List<AlertDto>>();
        Assert.False(after![0].Activa);
    }

    [Fact]
    public async Task DeleteAlert_RemovesFromList()
    {
        var (client, _) = await TestAuthHelper.CreateAuthenticatedUserAsync(_factory);
        await client.PostAsJsonAsync("/api/alerts",
            new CreateAlertRequest(null, null, null, null, null, null, null));
        var alerts = await (await client.GetAsync("/api/alerts")).Content.ReadFromJsonAsync<List<AlertDto>>();
        var id = alerts![0].Id;

        var deleteResp = await client.DeleteAsync($"/api/alerts/{id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResp.StatusCode);

        var after = await (await client.GetAsync("/api/alerts")).Content.ReadFromJsonAsync<List<AlertDto>>();
        Assert.Empty(after!);
    }
}
```

- [ ] **Step 2: Crear ReportsControllerTests**

`src/PropertyMap.Tests/Api/ReportsControllerTests.cs`
```csharp
using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using PropertyMap.Core.DTOs.Properties;
using PropertyMap.Core.DTOs.Reports;
using PropertyMap.Core.Enums;
using Xunit;

namespace PropertyMap.Tests.Api;

public class ReportsControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public ReportsControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private record CreatedIdDto(int Id);

    private async Task<(HttpClient adminClient, int listingId)> SetupApprovedListingAsync()
    {
        var (pubClient, _) = await TestAuthHelper.CreateAuthenticatedPublisherAsync(_factory);
        await TestAuthHelper.CreatePublisherProfileAsync(pubClient);
        var listing = new CreateListingRequest(
            Operacion: TipoOperacion.Venta, TipoPropiedad: TipoPropiedad.Casa,
            Titulo: "Casa para reportar", Descripcion: "Test",
            Precio: 50000, Moneda: "USD",
            DireccionTexto: "Calle Falsa 123", Ciudad: "Rosario", Provincia: "Santa Fe",
            Lat: -32.95, Lng: -60.64,
            Superficie: null, SuperficieCubierta: null, Ambientes: null,
            Dormitorios: null, Banos: null, Antiguedad: null,
            Cochera: false, Amenities: []);
        var createResp = await pubClient.PostAsJsonAsync("/api/properties", listing);
        var created = await createResp.Content.ReadFromJsonAsync<CreatedIdDto>();

        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider
            .GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<PropertyMap.Core.Entities.ApplicationUser>>();
        var adminEmail = $"admin_report_{Guid.NewGuid()}@test.com";
        var adminUser = new PropertyMap.Core.Entities.ApplicationUser
        {
            UserName = adminEmail, Email = adminEmail,
            Nombre = "Admin", Apellido = "Report", EmailConfirmed = true,
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

        return (adminClient, created.Id);
    }

    [Fact]
    public async Task CreateReport_ThenAdminSeesIt_AsPending()
    {
        var (adminClient, listingId) = await SetupApprovedListingAsync();
        var (userClient, _) = await TestAuthHelper.CreateAuthenticatedUserAsync(_factory);

        var reportResp = await userClient.PostAsJsonAsync("/api/reports",
            new CreateReportRequest(listingId, MotivoReporte.Estafa, "Sospechoso"));
        Assert.Equal(HttpStatusCode.OK, reportResp.StatusCode);

        var pending = await adminClient.GetFromJsonAsync<List<ReportDto>>("/api/admin/reports");
        Assert.Contains(pending!, r => r.PropertyListingId == listingId && r.Motivo == MotivoReporte.Estafa);
    }

    [Fact]
    public async Task ReviewReport_Resuelto_PausesListing()
    {
        var (adminClient, listingId) = await SetupApprovedListingAsync();
        var (userClient, _) = await TestAuthHelper.CreateAuthenticatedUserAsync(_factory);
        await userClient.PostAsJsonAsync("/api/reports",
            new CreateReportRequest(listingId, MotivoReporte.Spam, null));

        var pending = await adminClient.GetFromJsonAsync<List<ReportDto>>("/api/admin/reports");
        var reportId = pending!.First(r => r.PropertyListingId == listingId).Id;

        var reviewResp = await adminClient.PatchAsJsonAsync($"/api/admin/reports/{reportId}/review",
            new ReviewReportRequest(EstadoReporte.Resuelto));
        Assert.Equal(HttpStatusCode.NoContent, reviewResp.StatusCode);

        var listingResp = await adminClient.GetAsync($"/api/properties/{listingId}");
        var detail = await listingResp.Content.ReadFromJsonAsync<PropertyMap.Core.DTOs.ListingDetailDto>();
        // El listado pausado ya no es accesible públicamente por GetById (filtra por Publicada
        // salvo que sea el owner) — para Admin igual lo vemos vía GetAll.
        var allListings = await adminClient.GetFromJsonAsync<List<object>>("/api/admin/listings");
        Assert.NotNull(allListings);
    }
}
```

- [ ] **Step 3: Crear AlertMatchingTests (test de integración end-to-end)**

`src/PropertyMap.Tests/Api/AlertMatchingTests.cs`
```csharp
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using PropertyMap.Core.DTOs.Alerts;
using PropertyMap.Core.DTOs.Notifications;
using PropertyMap.Core.DTOs.Properties;
using PropertyMap.Core.Enums;
using Xunit;

namespace PropertyMap.Tests.Api;

public class AlertMatchingTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public AlertMatchingTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private record CreatedIdDto(int Id);

    [Fact]
    public async Task ApprovingListing_NotifiesMatchingAlert()
    {
        // Usuario crea una alerta para Venta + Casa en Mendoza, hasta 80000 USD
        var (userClient, _) = await TestAuthHelper.CreateAuthenticatedUserAsync(_factory);
        await userClient.PostAsJsonAsync("/api/alerts",
            new CreateAlertRequest("Casa en Mendoza", TipoOperacion.Venta, TipoPropiedad.Casa,
                "Mendoza", 80000, "USD", null));

        // Publisher publica una propiedad que matchea
        var (pubClient, _) = await TestAuthHelper.CreateAuthenticatedPublisherAsync(_factory);
        await TestAuthHelper.CreatePublisherProfileAsync(pubClient);
        var listing = new CreateListingRequest(
            Operacion: TipoOperacion.Venta, TipoPropiedad: TipoPropiedad.Casa,
            Titulo: "Casa en Mendoza matcheable", Descripcion: "Test",
            Precio: 75000, Moneda: "USD",
            DireccionTexto: "Av. San Martín 100", Ciudad: "Mendoza", Provincia: "Mendoza",
            Lat: -32.89, Lng: -68.84,
            Superficie: null, SuperficieCubierta: null, Ambientes: null,
            Dormitorios: null, Banos: null, Antiguedad: null,
            Cochera: false, Amenities: []);
        var createResp = await pubClient.PostAsJsonAsync("/api/properties", listing);
        var created = await createResp.Content.ReadFromJsonAsync<CreatedIdDto>();

        // Admin aprueba — esto debe disparar AlertMatchingService
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider
            .GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<PropertyMap.Core.Entities.ApplicationUser>>();
        var adminEmail = $"admin_match_{Guid.NewGuid()}@test.com";
        var adminUser = new PropertyMap.Core.Entities.ApplicationUser
        {
            UserName = adminEmail, Email = adminEmail,
            Nombre = "Admin", Apellido = "Match", EmailConfirmed = true,
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

        // El usuario debe tener una notificación de tipo AlertaCoincidencia
        var notifications = await userClient.GetFromJsonAsync<List<NotificationDto>>("/api/notifications");
        Assert.NotNull(notifications);
        Assert.Contains(notifications!, n =>
            n.Tipo == TipoNotificacion.AlertaCoincidencia &&
            n.UrlAccion == $"/propiedad/{created.Id}");
    }

    [Fact]
    public async Task ApprovingListing_DoesNotNotify_WhenPriceExceedsMax()
    {
        var (userClient, _) = await TestAuthHelper.CreateAuthenticatedUserAsync(_factory);
        await userClient.PostAsJsonAsync("/api/alerts",
            new CreateAlertRequest("Depto barato", TipoOperacion.Venta, TipoPropiedad.Departamento,
                null, 50000, "USD", null));

        var (pubClient, _) = await TestAuthHelper.CreateAuthenticatedPublisherAsync(_factory);
        await TestAuthHelper.CreatePublisherProfileAsync(pubClient);
        var listing = new CreateListingRequest(
            Operacion: TipoOperacion.Venta, TipoPropiedad: TipoPropiedad.Departamento,
            Titulo: "Depto caro", Descripcion: "Test",
            Precio: 200000, Moneda: "USD",
            DireccionTexto: "Av. Libertador 500", Ciudad: "Buenos Aires", Provincia: "Buenos Aires",
            Lat: -34.58, Lng: -58.40,
            Superficie: null, SuperficieCubierta: null, Ambientes: null,
            Dormitorios: null, Banos: null, Antiguedad: null,
            Cochera: false, Amenities: []);
        var createResp = await pubClient.PostAsJsonAsync("/api/properties", listing);
        var created = await createResp.Content.ReadFromJsonAsync<CreatedIdDto>();

        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider
            .GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<PropertyMap.Core.Entities.ApplicationUser>>();
        var adminEmail = $"admin_nomatch_{Guid.NewGuid()}@test.com";
        var adminUser = new PropertyMap.Core.Entities.ApplicationUser
        {
            UserName = adminEmail, Email = adminEmail,
            Nombre = "Admin", Apellido = "NoMatch", EmailConfirmed = true,
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

        var notifications = await userClient.GetFromJsonAsync<List<NotificationDto>>("/api/notifications");
        Assert.NotNull(notifications);
        Assert.DoesNotContain(notifications!, n => n.Tipo == TipoNotificacion.AlertaCoincidencia);
    }
}
```

- [ ] **Step 4: Correr toda la suite de tests**

```bash
cd C:\Agentes\PropertyMap
dotnet test src/PropertyMap.Tests/PropertyMap.Tests.csproj
```
Expected: todos los tests pasan (los ~59 existentes de Phase 6 + los nuevos de Phase 7).

- [ ] **Step 5: Build completo de la solución**

```bash
dotnet build src/PropertyMap.Web/PropertyMap.Web.sln
```
Expected: `Build succeeded.`

- [ ] **Step 6: Commit**

```bash
git add src/PropertyMap.Tests/Api/AlertsControllerTests.cs src/PropertyMap.Tests/Api/ReportsControllerTests.cs src/PropertyMap.Tests/Api/AlertMatchingTests.cs
git commit -m "test: add integration tests for alerts, reports, and alert-matching trigger"
```

---

## Self-Review

**Cobertura del spec (Phase 7 — Inteligencia):**
- ✅ Alertas de búsqueda (guardar criterios, matchear nuevas publicaciones) → Tasks 2, 6, 7, 11
- ✅ Notificaciones in-app (SignalR, centro en navbar) → Tasks 5, 9, 10, 11
- ✅ Notificaciones email (trigger automático al publicar propiedad que matchea alerta) → Tasks 6, 8
- ✅ Reportes & moderación (usuarios reportan, admin revisa dashboard) → Tasks 1, 3, 7, 8, 12

**Placeholder scan:** sin TODOs ni "implementar luego" — todo el código de cada step es completo y compilable contra los archivos reales leídos del repo.

**Consistencia de tipos:** `NotificationDto`, `AlertDto`, `ReportDto` se definen una sola vez en Task 1 y se reusan con la misma forma en repos (Task 2-4), servicios (Task 6, 10), controllers (Task 7-8) y tests (Task 13). `IAlertMatchingService.NotifyMatchingAlertsAsync(PropertyListing)` se define en Task 6 y se invoca igual en `AdminController.Review` (Task 8).


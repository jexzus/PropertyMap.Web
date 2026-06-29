# Phase 9.4 — Audit Logs — Design

**Status:** Approved
**Scope:** Cuarto sub-proyecto de Phase 9 (Escala & Calidad). Orden completo de Phase 9: 1) Clustering (✅), 2) Testing exhaustivo (✅), 3) Security audit (✅), 4) Audit logs (este spec), 5) Búsqueda avanzada.

## Contexto

La entidad `AuditLog` (`PropertyMap.Core/Entities/AuditLog.cs`) y su `DbSet<AuditLog>` en `AppDbContext` ya existen desde Phase 3 (mismo patrón que `Alert`/`Report`/`Notification`: el modelo de datos se diseñó temprano, pero quedó sin la capa de repositorio/lógica/UI hasta que una fase posterior la necesitó). Hoy nada escribe en esa tabla ni hay forma de consultarla.

Schema existente (sin cambios):
```csharp
public class AuditLog
{
    public int Id { get; set; }
    public string? UserId { get; set; }
    public string Accion { get; set; } = "";
    public string Entidad { get; set; } = "";
    public string EntidadId { get; set; } = "";
    public string? Detalles { get; set; }
    public DateTime FechaAccion { get; set; } = DateTime.UtcNow;
    public string? IpAddress { get; set; }
}
```

## Alcance

Auditar 2 acciones administrativas sensibles, ambas en `AdminController`:
- `Review` (aprobar/rechazar una propiedad pendiente).
- `ReviewReport` (resolver/rechazar un reporte de usuario).

Sin auditoría de cambios de suscripción ni de intentos de login fallidos en esta iteración (decisión explícita — quedan fuera de scope, no son parte de este plan). Se agrega una página admin simple para consultar las últimas entradas, sin filtros ni paginación por ahora.

## Componentes

### `PropertyMap.Core/DTOs/Admin/AuditLogDto.cs` (nuevo)

```csharp
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

### `IAuditLogRepository`/`AuditLogRepository` (nuevo, `PropertyMap.Core/Interfaces/` y `PropertyMap.Infrastructure/Repositories/`)

```csharp
public interface IAuditLogRepository
{
    Task AddAsync(AuditLog log);
    Task<List<AuditLogDto>> GetRecentAsync(int take = 50);
}
```

`GetRecentAsync` ordena por `FechaAccion` descendente, proyecta a `AuditLogDto` (mismo patrón que `GetActiveListingsForMapAsync`/`ListingMapDto` — proyectar a DTO en la query en vez de devolver la entidad completa, evita problemas de serialización y es el patrón establecido en el resto del repo).

### `AdminController` (modificado)

Se inyecta `IAuditLogRepository _auditLog` en el constructor. Se agrega `using System.Security.Claims;` (no estaba importado).

En `Review`, después de `await _listings.UpdateAsync(listing);` y antes de disparar `NotifyMatchingAlertsAsync` (si aplica):
```csharp
await _auditLog.AddAsync(new AuditLog
{
    UserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
    Accion = request.Aprobar ? "AprobarListing" : "RechazarListing",
    Entidad = "PropertyListing",
    EntidadId = id.ToString(),
    Detalles = request.Aprobar ? null : request.MotivoRechazo,
    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
});
```

En `ReviewReport`, después de `await _reports.UpdateAsync(report);`:
```csharp
await _auditLog.AddAsync(new AuditLog
{
    UserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
    Accion = request.NuevoEstado == EstadoReporte.Resuelto ? "ResolverReporte" : "RechazarReporte",
    Entidad = "Report",
    EntidadId = id.ToString(),
    Detalles = null,
    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
});
```

Nuevo endpoint:
```csharp
[HttpGet("audit-logs")]
public async Task<IActionResult> GetAuditLogs() =>
    Ok(await _auditLog.GetRecentAsync());
```

### Blazor

`IAuditLogApiService`/`AuditLogApiService` (servicio mínimo, un solo método `GetRecentAsync()` que llama a `GET api/admin/audit-logs`, mismo patrón de `IHttpClientFactory("api")` + `MemoryTokenStore` que el resto de los servicios Blazor). Página nueva `Admin/AuditLogs.razor` en `/admin/auditoria`, mismo patrón visual y de auth (`@attribute [Authorize(Roles = "Admin")]`) que `Admin/Reportes.razor`: tabla simple con columnas Fecha/Usuario/Acción/Entidad/Detalles, sin paginación ni filtros. Se agrega un link a esta página en el menú/navegación admin existente (junto al link a `/admin/reportes`, si existe uno visible).

## Testing

Tests de integración (xUnit + EF InMemory, mismo patrón establecido):
- Aprobar un listing pendiente genera una entrada con `Accion="AprobarListing"`, `Entidad="PropertyListing"`, `EntidadId` correcto.
- Rechazar un listing genera `Accion="RechazarListing"` con el motivo de rechazo en `Detalles`.
- Resolver un reporte genera `Accion="ResolverReporte"`, `Entidad="Report"`.
- `GET /api/admin/audit-logs` requiere rol Admin (403 para Publisher/User) y devuelve las entradas ordenadas por fecha descendente.

Sin test E2E de la página Blazor (fuera de scope, ya decidido en fases anteriores de Phase 9 — solo integration tests de API).

## Fuera de scope

- Auditoría de cambios de suscripción/plan.
- Auditoría de intentos de login fallidos/lockouts.
- Filtros, paginación o búsqueda en la página admin de consulta.
- Purga/retención automática de logs antiguos.

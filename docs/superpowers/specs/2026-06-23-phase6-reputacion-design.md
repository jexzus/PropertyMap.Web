# PropertyMap Phase 6 — Reputación (valoraciones y ranking de agentes)

## Goal

Permitir a usuarios registrados valorar propiedades de tipo AlquilerTemporario (restringido a quienes tengan una Consulta activa) y valorar agentes (restringido a quienes hayan consultado al menos una propiedad del agente). Calcular un ranking automático de agentes basado en fórmula ponderada. Exponer un endpoint público con el top de agentes filtrable por ciudad.

## Architecture

Opción A: ranking calculado on-the-fly en cada llamada al endpoint público. Sin campo extra en Publisher. Las entidades `PropertyRating` y `AgentRating` ya existen en la DB con sus constraints — no se necesita migración nueva. Se agregan dos repositorios, un controlador, DTOs, un servicio Blazor y componentes de UI.

---

## Entidades (ya existen)

### `PropertyRating` (`PropertyMap.Core/Entities/PropertyRating.cs`)

```csharp
public class PropertyRating
{
    public int Id { get; set; }
    public int PropertyListingId { get; set; }
    public string UserId { get; set; } = "";
    public int PuntajeUbicacion { get; set; }    // 1–5
    public int PuntajeEstado { get; set; }        // 1–5
    public int PuntajePrecioCalidad { get; set; } // 1–5
    public string? Comentario { get; set; }
    public DateTime FechaValoracion { get; set; }

    public PropertyListing PropertyListing { get; set; } = null!;
    public ApplicationUser User { get; set; } = null!;
}
```

Unique index en `(UserId, PropertyListingId)` — un solo rating por usuario-propiedad.

### `AgentRating` (`PropertyMap.Core/Entities/AgentRating.cs`)

```csharp
public class AgentRating
{
    public int Id { get; set; }
    public int PublisherId { get; set; }
    public string UserId { get; set; } = "";
    public int PuntajeAtencion { get; set; }         // 1–5
    public int PuntajeRapidez { get; set; }           // 1–5
    public int PuntajeTransparencia { get; set; }     // 1–5
    public int PuntajeProfesionalismo { get; set; }   // 1–5
    public string? Comentario { get; set; }
    public DateTime FechaValoracion { get; set; }

    public Publisher Publisher { get; set; } = null!;
    public ApplicationUser User { get; set; } = null!;
}
```

Unique index en `(UserId, PublisherId)`.

### AppDbContext — sin cambios

Ambos `DbSet` y sus configuraciones ya están en `AppDbContext.OnModelCreating`.

---

## Backend

### DTOs (`PropertyMap.Core/DTOs/Ratings/RatingDtos.cs`)

```csharp
// Requests
public record RatePropertyRequest(
    int ListingId,
    [Range(1,5)] int PuntajeUbicacion,
    [Range(1,5)] int PuntajeEstado,
    [Range(1,5)] int PuntajePrecioCalidad,
    string? Comentario);

public record RateAgentRequest(
    int PublisherId,
    [Range(1,5)] int PuntajeAtencion,
    [Range(1,5)] int PuntajeRapidez,
    [Range(1,5)] int PuntajeTransparencia,
    [Range(1,5)] int PuntajeProfesionalismo,
    string? Comentario);

// Stats
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

// Ranking
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

### IPropertyRatingRepository (`PropertyMap.Core/Interfaces/IPropertyRatingRepository.cs`)

```csharp
public interface IPropertyRatingRepository
{
    Task<bool> HasConsultaAsync(int listingId, string userId);
    Task<PropertyRating?> GetByUserAndListingAsync(int listingId, string userId);
    Task AddOrUpdateAsync(PropertyRating rating);
    Task<PropertyRatingStatsDto> GetStatsAsync(int listingId);
}
```

### PropertyRatingRepository (`PropertyMap.Infrastructure/Repositories/PropertyRatingRepository.cs`)

- `HasConsultaAsync`: verifica que exista una `Consulta` con ese `PropertyListingId` y `UserId`.
- `GetByUserAndListingAsync`: busca por `(UserId, PropertyListingId)`.
- `AddOrUpdateAsync`: upsert — si ya existe, actualiza puntajes, comentario y `FechaValoracion`; si no, inserta. `SaveChangesAsync`.
- `GetStatsAsync`: proyección de promedios + count usando EF Core.

### IAgentRatingRepository (`PropertyMap.Core/Interfaces/IAgentRatingRepository.cs`)

```csharp
public interface IAgentRatingRepository
{
    Task<bool> HasConsultaWithPublisherAsync(int publisherId, string userId);
    Task<AgentRating?> GetByUserAndPublisherAsync(int publisherId, string userId);
    Task AddOrUpdateAsync(AgentRating rating);
    Task<AgentRatingStatsDto> GetStatsAsync(int publisherId);
    Task<List<AgentRankingItemDto>> GetRankingAsync(string? ciudad, int top = 20);
}
```

### AgentRatingRepository (`PropertyMap.Infrastructure/Repositories/AgentRatingRepository.cs`)

- `HasConsultaWithPublisherAsync`: verifica que el usuario tenga al menos una `Consulta` cuya propiedad pertenece al publisher dado (join `Consultas → PropertyListings → Publisher`).
- `GetByUserAndPublisherAsync`: busca por `(UserId, PublisherId)`.
- `AddOrUpdateAsync`: upsert.
- `GetStatsAsync`: proyección de promedios + count.
- `GetRankingAsync`: query on-the-fly que calcula el `RankingScore` para cada publisher con al menos un rating de agente.

### Fórmula de Ranking (todo normalizado a 0–100)

```
ratingScore       = (promedioGeneral − 1) / 4 × 100
responseScore     = max(0, (72 − avgHorasRespuesta) / 72 × 100)
                    // 0h → 100pts, 72h+ → 0pts; sin respuestas → 0
operacionesScore  = min(100, operaciones / 50 × 100)
                    // operaciones = listings con Estado Vendida o Alquilada
antiguedadScore   = min(100, añosEnPlataforma / 5 × 100)
                    // años desde Publisher.FechaRegistro, cap 5 años

RankingScore = 0.40 × ratingScore
             + 0.30 × responseScore
             + 0.20 × operacionesScore
             + 0.10 × antiguedadScore
```

`avgHorasRespuesta` se calcula como el promedio de tiempo entre cada mensaje de usuario (`EsDelPublisher = false`) y la siguiente respuesta del publisher (`EsDelPublisher = true`) en el mismo hilo, a través de todas las consultas del agente.

El filtro `?ciudad` filtra por `PropertyListing.Ciudad` de las propiedades del publisher.

### RatingsController (`PropertyMap.Api/Controllers/RatingsController.cs`)

Ruta base: `api/ratings`.

| Método | Ruta | Auth | Descripción |
|--------|------|------|-------------|
| POST | `/api/ratings/property` | `[Authorize]` | Upsert rating de propiedad |
| GET | `/api/ratings/property/{listingId}/stats` | Público | Stats de una propiedad |
| POST | `/api/ratings/agent` | `[Authorize]` | Upsert rating de agente |
| GET | `/api/ratings/agent/{publisherId}/stats` | Público | Stats de un agente |
| GET | `/api/ratings/ranking` | Público | Top agentes (`?ciudad=&top=20`) |

**Flujo POST `/api/ratings/property`:**
1. Extrae `userId` del claim.
2. Verifica que la propiedad sea `AlquilerTemporario` (via `IListingRepository.GetByIdAsync`). Si no → 400.
3. Llama `HasConsultaAsync` → si false → 403.
4. `AddOrUpdateAsync` con los puntajes.
5. Retorna `PropertyRatingStatsDto` actualizado.

**Flujo POST `/api/ratings/agent`:**
1. Extrae `userId` del claim.
2. Llama `HasConsultaWithPublisherAsync` → si false → 403.
3. `AddOrUpdateAsync`.
4. Retorna `AgentRatingStatsDto` actualizado.

### Registro en Program.cs (API)

```csharp
builder.Services.AddScoped<IPropertyRatingRepository, PropertyRatingRepository>();
builder.Services.AddScoped<IAgentRatingRepository, AgentRatingRepository>();
```

---

## Frontend (Blazor)

### Nuevo servicio

**`IRatingsApiService`** / **`RatingsApiService`** — sigue el patrón `IHttpClientFactory("api") + MemoryTokenStore.SetAuth()`.

```csharp
public interface IRatingsApiService
{
    Task<PropertyRatingStatsDto?> RatePropertyAsync(RatePropertyRequest request);
    Task<PropertyRatingStatsDto?> GetPropertyStatsAsync(int listingId);
    Task<AgentRatingStatsDto?> RateAgentAsync(RateAgentRequest request);
    Task<AgentRatingStatsDto?> GetAgentStatsAsync(int publisherId);
    Task<List<AgentRankingItemDto>> GetRankingAsync(string? ciudad = null, int top = 20);
}
```

### File map

**Creados:**
```
PropertyMap.Web/.../Services/IRatingsApiService.cs
PropertyMap.Web/.../Services/RatingsApiService.cs
PropertyMap.Web/.../Components/Shared/RatingStars.razor
PropertyMap.Web/.../Components/Shared/PropertyRatingForm.razor
PropertyMap.Web/.../Components/Shared/AgentRatingForm.razor
PropertyMap.Web/.../Components/Pages/TopAgentes.razor           (/top-agentes)
```

**Modificados:**
```
PropertyMap.Web/.../Program.cs                                   (+ registro servicio)
PropertyMap.Web/.../Components/Pages/Account/ConsultaDetalle.razor (+ formularios rating)
PropertyMap.Web/.../Components/Pages/PropertyDetail.razor        (+ bloque stats rating)
PropertyMap.Web/.../Components/Layout/Navbar.razor               (+ link "Top Agentes")
PropertyMap.Web/.../wwwroot/css/app.css                          (+ estilos stars y rating)
```

### Componentes compartidos

**`RatingStars.razor`**
```razor
[Parameter] public double Value { get; set; }        // 0–5, soporta decimales en modo lectura
[Parameter] public bool ReadOnly { get; set; }
[Parameter] public EventCallback<int> OnChange { get; set; }
```
Render: 5 estrellas SVG. En modo interactivo, hover + click setean el valor. En modo lectura, relleno parcial para mostrar promedios.

**`PropertyRatingForm.razor`**
- Parámetros: `int ListingId`, `PropertyRatingStatsDto? Stats` (para mostrar promedio actual)
- 3 criterios con `RatingStars` interactivo: Ubicación, Estado, Precio/Calidad
- Campo de comentario opcional
- Botón "Valorar" → llama `IRatingsApiService.RatePropertyAsync`
- Muestra mensaje de éxito y actualiza stats

**`AgentRatingForm.razor`**
- Parámetros: `int PublisherId`, `AgentRatingStatsDto? Stats`
- 4 criterios: Atención, Rapidez, Transparencia, Profesionalismo
- Mismo patrón que PropertyRatingForm

### Páginas modificadas

**`/account/consultas/{Id:int}`** — al pie del hilo, después de `<ConsultaThread>`:
```razor
@if (detail?.OperacionPropiedad == "AlquilerTemporario")
{
    <PropertyRatingForm ListingId="detail.PropertyListingId" />
}
<AgentRatingForm PublisherId="publisherId" />
```
El `publisherId` se resuelve al cargar el hilo (viene del detalle de la consulta).

**`PropertyDetail.razor`** — agrega bloque de rating después del precio:
```razor
@if (stats?.TotalValoraciones > 0)
{
    <RatingStars Value="stats.PromedioGeneral" ReadOnly="true" />
    <span>@stats.PromedioGeneral:F1 (@stats.TotalValoraciones valoraciones)</span>
}
```
Carga `GetPropertyStatsAsync(Id)` en `OnInitializedAsync`.

### `/top-agentes` — Página pública

- `@rendermode InteractiveServer`
- Carga `GetRankingAsync()` en `OnAfterRenderAsync` (sin auth requerida, `TryRestoreSessionAsync` no necesario)
- Input de ciudad para filtrar en tiempo real (llama `GetRankingAsync(ciudad)`)
- Tabla: Posición | Nombre | Tipo | Score | Rating | Tiempo resp. | Operaciones | Antigüedad
- Enlace desde Navbar (visible para todos, sin auth)

### Navbar

Agregar fuera del bloque `<AuthorizeView>` (visible siempre):
```razor
<a href="/top-agentes" class="btn-ghost">Top Agentes</a>
```

---

## Testing

**Integración (API):**
- `RatingsControllerTests`:
  - POST property rating crea/actualiza correctamente
  - POST property rating falla (400) si la propiedad no es AlquilerTemporario
  - POST property rating falla (403) si el usuario no tiene Consulta con esa propiedad
  - GET property stats retorna promedios correctos
  - POST agent rating crea/actualiza correctamente
  - POST agent rating falla (403) si el usuario no consultó al agente
  - GET ranking retorna agentes ordenados por score descendente
  - GET ranking filtra por ciudad

**No se testea:**
- Frontend Blazor (manual)
- Cálculo exacto de la fórmula de ranking en tests unitarios (se verifica indirectamente via integración)

---

## Decisiones explícitas

- **Sin migración nueva**: las entidades ya existen en la DB con constraints y configuración correcta.
- **Upsert en lugar de insert**: un usuario puede actualizar su valoración. Unique index en DB garantiza un solo registro por par usuario-entidad.
- **AlquilerTemporario**: validación en el controlador via `IListingRepository` — la propiedad debe ser de ese tipo para admitir valoración.
- **Acceso via Consulta**: verificación en repositorio (no en controlador) para mantener la lógica de acceso encapsulada.
- **Ranking on-the-fly**: sin campo `RankingScore` en Publisher. La query se ejecuta cada vez. Escalable con caché si se necesita en el futuro.
- **TiempoRespuesta cap 72h**: publishers que tardan más de 3 días en responder obtienen 0 puntos en ese componente. Publishers sin ninguna respuesta también obtienen 0.
- **`ConsultaDetalle` incluye `OperacionPropiedad`**: `ConsultaDetailDto` necesita exponer la operación de la propiedad para que el frontend pueda decidir si mostrar el formulario de rating. Si aún no está en el DTO, se agrega.

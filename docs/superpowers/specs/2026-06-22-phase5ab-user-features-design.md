# PropertyMap Phase 5A+5B — User Features: Profile, Favorites & View Tracking

## Goal

Darle a los usuarios registrados tres capacidades de engagement: editar su perfil (con avatar), guardar propiedades como favoritas, y registrar vistas únicas por propiedad (1 por usuario/IP por día).

## Architecture

Tres features independientes construidas sobre entidades que ya existen en el modelo (`PropertyFavorite`, `PropertyView`, `ApplicationUser`). El enfoque es **Opción A: controladores separados + servicio de tracking**:

- `UserController` y `FavoritesController` son nuevos controladores en la API
- `IViewTrackingService` se inyecta en el `ListingsController` existente como side-effect del GET `/api/listings/{id}`
- En el frontend: dos nuevas páginas (`/account/profile`, `/account/favorites`), dos nuevos servicios (`UserApiService`, `FavoritesApiService`), y un componente `FavoriteButton` reutilizable

La API del publisher y la del admin pueden leer conteos de favoritos/vistas sin endpoints nuevos — se incluyen en los DTOs existentes de detalle de listing.

---

## Backend

### Migración

Nueva migration `Phase5UserFeatures` con un índice de dedup en `PropertyView`:
- `UNIQUE(PropertyListingId, UserId, Fecha)` — para usuarios autenticados (UserId not null)
- `UNIQUE(PropertyListingId, IpAddress, Fecha)` — para usuarios anónimos (UserId null)

Ambas columnas son nullable. `Fecha` es `DateOnly` (fecha sin hora).

### Nuevas interfaces

**`IFavoriteRepository`** (`PropertyMap.Core/Interfaces/`)
```csharp
Task AddAsync(PropertyFavorite favorite);
Task RemoveAsync(int listingId, string userId);
Task<List<PropertyFavorite>> GetByUserAsync(string userId);
Task<bool> IsFavoritedAsync(int listingId, string userId);
Task<int> GetCountAsync(int listingId);
```

**`IViewTrackingService`** (`PropertyMap.Core/Interfaces/`)
```csharp
Task TrackViewAsync(int listingId, string? userId, string ipAddress, DateOnly date);
```

### Implementaciones

**`FavoriteRepository`** (`PropertyMap.Infrastructure/Repositories/`)
- `AddAsync`: inserta si no existe (chequea IsFavoritedAsync primero para idempotencia)
- `RemoveAsync`: elimina si existe, no lanza si no existe
- `GetByUserAsync`: incluye `PropertyListing` con sus imágenes para armar el DTO
- `GetCountAsync`: COUNT de la tabla por listingId

**`ViewTrackingService`** (`PropertyMap.Infrastructure/Services/`)
- Chequea si ya existe un registro para `(listingId, userId, date)` o `(listingId, ip, date)`
- Si no existe, inserta. Si existe, no hace nada.
- Todo el método está en try/catch — un error de tracking no debe propagar

### ImageService — extensión para avatares

Agregar método `SaveAvatarAsync(string userId, IFormFile file)` a `IImageService`:
- Guarda en `uploads/avatars/{userId}/avatar.{ext}`
- Elimina el avatar anterior del mismo usuario si existe
- Mismas validaciones de extensión y tamaño que las fotos de propiedades

### Nuevos controladores

**`UserController`** — `/api/user` — `[Authorize]`

| Método | Ruta | Descripción |
|--------|------|-------------|
| GET | `/api/user/profile` | Devuelve `UserProfileResponse` |
| PUT | `/api/user/profile` | Actualiza nombre y apellido |
| POST | `/api/user/avatar` | Sube imagen, devuelve `{ avatarUrl }` |

DTOs (`PropertyMap.Core/DTOs/User/`):
```csharp
// UserProfileResponse.cs
record UserProfileResponse(string Id, string Nombre, string Apellido, string Email, string? AvatarUrl);

// UpdateProfileRequest.cs
record UpdateProfileRequest(string Nombre, string Apellido);

// FavoriteStatusResponse.cs
record FavoriteStatusResponse(bool IsFavorited, int Count);
```

**`FavoritesController`** — `/api/favorites` — `[Authorize]`

| Método | Ruta | Descripción |
|--------|------|-------------|
| GET | `/api/favorites` | Lista de listings favoriteados |
| POST | `/api/favorites/{listingId}` | Agrega favorito (idempotente) |
| DELETE | `/api/favorites/{listingId}` | Quita favorito (idempotente) |
| GET | `/api/favorites/{listingId}/status` | `{ isFavorited, count }` |

El GET `/api/favorites` devuelve `List<MyListingDto>` (DTO ya existente).
El GET `/api/favorites/{listingId}/status` es público (permite anónimos para mostrar conteo) pero `isFavorited` siempre es `false` para anónimos.

**`ListingsController` — modificación**

En el método `GetById` existente, agregar al final (antes del return):
```csharp
try
{
    var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "";
    await _viewTracking.TrackViewAsync(id, userId, ip, DateOnly.FromDateTime(DateTime.UtcNow));
}
catch { }
```
El try/catch vacío garantiza que un error de tracking no rompa la respuesta.

### Registro en Program.cs (API)

```csharp
builder.Services.AddScoped<IFavoriteRepository, FavoriteRepository>();
builder.Services.AddScoped<IViewTrackingService, ViewTrackingService>();
```

---

## Frontend (Blazor)

### File map

**Creados:**
```
PropertyMap.Web/PropertyMap.Web/Services/IUserApiService.cs
PropertyMap.Web/PropertyMap.Web/Services/UserApiService.cs
PropertyMap.Web/PropertyMap.Web/Services/IFavoritesApiService.cs
PropertyMap.Web/PropertyMap.Web/Services/FavoritesApiService.cs
PropertyMap.Web/PropertyMap.Web/Components/Pages/Account/Profile.razor
PropertyMap.Web/PropertyMap.Web/Components/Pages/Account/Favorites.razor
PropertyMap.Web/PropertyMap.Web/Components/Shared/FavoriteButton.razor
```

**Modificados:**
```
PropertyMap.Web/PropertyMap.Web/Program.cs  (registrar nuevos servicios)
PropertyMap.Web/PropertyMap.Web/Components/Layout/NavMenu.razor (o Navbar)  (links de perfil/favoritos)
PropertyMap.Web/PropertyMap.Web/Components/Pages/PropertyDetail.razor  (FavoriteButton)
PropertyMap.Web/PropertyMap.Web/wwwroot/css/app.css  (estilos del botón favorito)
```

### IUserApiService

```csharp
public interface IUserApiService
{
    Task<UserProfileResponse?> GetProfileAsync();
    Task<(bool Success, string? Error)> UpdateProfileAsync(string nombre, string apellido);
    Task<(bool Success, string? AvatarUrl, string? Error)> UploadAvatarAsync(IBrowserFile file);
}
```

### IFavoritesApiService

```csharp
public interface IFavoritesApiService
{
    Task<List<MyListingDto>> GetFavoritesAsync();
    Task<bool> ToggleFavoriteAsync(int listingId, bool currentlyFavorited);
    Task<(bool IsFavorited, int Count)> GetStatusAsync(int listingId);
}
```

### Profile.razor — `/account/profile`

- `@rendermode InteractiveServer`
- `OnAfterRenderAsync(firstRender)`: restaura sesión + carga perfil via `UserApiService.GetProfileAsync()`
- Formulario: Nombre, Apellido — botón Guardar → `UpdateProfileAsync`
- Avatar: muestra imagen actual si existe (o placeholder), `InputFile` para subir nueva → `UploadAvatarAsync` → actualiza preview
- Wrap en `<AuthorizeView>` con redirect a login si no autenticado

### Favorites.razor — `/account/favorites`

- `@rendermode InteractiveServer`
- `OnAfterRenderAsync(firstRender)`: restaura sesión + carga favoritos via `FavoritesApiService.GetFavoritesAsync()`
- Cards estilo dashboard (reusar estilos `.dashboard-listing-card`)
- Botón "Quitar" en cada card → `ToggleFavoriteAsync` → remueve de la lista local
- Estado vacío: "Todavía no guardaste ninguna propiedad"
- Wrap en `<AuthorizeView>`

### FavoriteButton.razor

```razor
@* Componente reutilizable para toggle de favorito *@
[Parameter] public int ListingId { get; set; }
```

- Estado inicial cargado desde `FavoritesApiService.GetStatusAsync(ListingId)` en `OnInitializedAsync`
- Render: ícono SVG corazón (relleno si favoriteado, outline si no) + conteo
- Click: llama `ToggleFavoriteAsync`, actualiza estado localmente
- Si el usuario no está autenticado: navega a `/Account/Login?returnUrl=/property/{ListingId}`

### PropertyDetail.razor — modificación

- Agregar `<FavoriteButton ListingId="@listingId" />` cerca del título/precio
- No se necesita nada para view tracking — el server-side tracking ocurre al llamar `GET /api/listings/{id}` que ya se llama para cargar el detalle

### NavMenu/Navbar — modificación

Dentro de `<AuthorizeView>`:
```razor
<a href="/account/profile">Mi perfil</a>
<a href="/account/favorites">Mis favoritos</a>
```

### Registro en Program.cs (Web)

```csharp
builder.Services.AddScoped<IUserApiService, UserApiService>();
builder.Services.AddScoped<IFavoritesApiService, FavoritesApiService>();
```

---

## Testing

**Integración (API):**
- `UserControllerTests`: GET profile devuelve datos correctos, PUT actualiza nombre, POST avatar guarda URL
- `FavoritesControllerTests`: POST agrega, DELETE quita, POST idempotente, GET status devuelve count correcto
- `ViewTrackingTests`: misma IP+listingId+fecha no duplica registro, diferente fecha sí crea nuevo

**No se testea:**
- Frontend Blazor (manual)
- Fire-and-forget del tracking (el servicio sí se testea en unidad)

---

## Decisiones explícitas

- **`isFavorited` en status es público** (anónimos pueden ver el conteo, pero `isFavorited = false` siempre para ellos)
- **Avatar reemplaza**: subir nuevo avatar borra el anterior del disco; no se acumula historial
- **Tracking awaited con swallow**: se usa `await` + try/catch vacío — no fire-and-forget (evita unobserved task exceptions en ASP.NET Core)
- **`DateOnly` para dedup de vistas**: la ventana es el día calendario (UTC), no 24hs rodantes
- **FavoriteButton anónimo**: redirige al login en lugar de mostrar error

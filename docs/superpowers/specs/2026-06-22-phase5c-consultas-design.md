# PropertyMap Phase 5C — Consultas (Hilos privados usuario-publisher)

## Goal

Permitir a usuarios registrados abrir un hilo de mensajes privado con el publisher de cualquier propiedad publicada. El hilo es único por par usuario-propiedad. Ambas partes pueden enviar mensajes de forma ilimitada. Se envían notificaciones in-app y email a cada nueva interacción.

## Architecture

Opción B: dos entidades nuevas (`Consulta` + `ConsultaMensaje`), un controlador nuevo (`ConsultasController`), dos servicios de email nuevos, y cuatro páginas Blazor (bandejas + vistas de hilo para usuario y publisher). Las entidades `PropertyQuestion`/`PropertyAnswer` existentes no se tocan — son independientes.

---

## Entidades

### `Consulta` (`PropertyMap.Core/Entities/Consulta.cs`)

```csharp
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

Unique index en `(PropertyListingId, UserId)` — un solo hilo por par.

### `ConsultaMensaje` (`PropertyMap.Core/Entities/ConsultaMensaje.cs`)

```csharp
public class ConsultaMensaje
{
    public int Id { get; set; }
    public int ConsultaId { get; set; }
    public string SenderId { get; set; } = "";   // ApplicationUser.Id de quien envía
    public bool EsDelPublisher { get; set; }
    public string Mensaje { get; set; } = "";
    public DateTime FechaEnvio { get; set; }

    public Consulta Consulta { get; set; } = null!;
    public ApplicationUser Sender { get; set; } = null!;
}
```

### AppDbContext — cambios

```csharp
public DbSet<Consulta> Consultas => Set<Consulta>();
public DbSet<ConsultaMensaje> ConsultaMensajes => Set<ConsultaMensaje>();
```

Configuración en `OnModelCreating`:
```csharp
modelBuilder.Entity<Consulta>()
    .HasIndex(c => new { c.PropertyListingId, c.UserId }).IsUnique();

modelBuilder.Entity<Consulta>()
    .HasMany(c => c.Mensajes)
    .WithOne(m => m.Consulta)
    .HasForeignKey(m => m.ConsultaId)
    .OnDelete(DeleteBehavior.Cascade);

modelBuilder.Entity<ConsultaMensaje>()
    .HasOne(m => m.Sender)
    .WithMany()
    .HasForeignKey(m => m.SenderId)
    .OnDelete(DeleteBehavior.NoAction);
```

### Migration

`Phase5CConsultas` — agrega tablas `Consultas` y `ConsultaMensajes`. No modifica tablas existentes.

---

## Backend

### DTOs (`PropertyMap.Core/DTOs/Consultas/`)

```csharp
// Request: usuario abre o continúa hilo
public record CreateConsultaRequest(int ListingId, string Mensaje);

// Request: publisher responde
public record SendMensajeRequest(string Mensaje);

// Ítem de bandeja (lista de hilos)
public record ConsultaSummaryDto(
    int Id,
    int PropertyListingId,
    string PropertyTitulo,
    string UltimoMensaje,
    bool UltimoEsDelPublisher,
    DateTime FechaUltimoMensaje);

// Mensaje individual dentro de un hilo
public record ConsultaMensajeDto(
    int Id,
    string SenderNombre,
    bool EsDelPublisher,
    string Mensaje,
    DateTime FechaEnvio);

// Hilo completo
public record ConsultaDetailDto(
    int Id,
    int PropertyListingId,
    string PropertyTitulo,
    List<ConsultaMensajeDto> Mensajes);
```

### IConsultaRepository (`PropertyMap.Core/Interfaces/IConsultaRepository.cs`)

```csharp
public interface IConsultaRepository
{
    Task<Consulta> GetOrCreateAsync(int listingId, string userId);
    Task<ConsultaDetailDto?> GetByIdAsync(int consultaId, string requesterId);
    Task<List<ConsultaSummaryDto>> GetByUserAsync(string userId);
    Task<List<ConsultaSummaryDto>> GetByPublisherAsync(int publisherId);
    Task AddMessageAsync(ConsultaMensaje message);
}
```

### ConsultaRepository (`PropertyMap.Infrastructure/Repositories/ConsultaRepository.cs`)

- `GetOrCreateAsync`: busca por `(listingId, userId)`, crea si no existe.
- `GetByIdAsync`: carga el hilo con mensajes ordenados por `FechaEnvio`. Verifica que `requesterId` sea el usuario dueño del hilo o el publisher de la propiedad; retorna `null` si no tiene acceso.
- `GetByUserAsync`: proyecta `ConsultaSummaryDto` ordenado por `FechaUltimoMensaje` desc.
- `GetByPublisherAsync`: filtra consultas cuya propiedad pertenece al publisher dado, proyecta `ConsultaSummaryDto`.
- `AddMessageAsync`: inserta el `ConsultaMensaje` **y** actualiza `Consulta.FechaUltimoMensaje` en la misma operación (`SaveChangesAsync` único).

### IEmailService — nuevos métodos

```csharp
Task SendNuevaConsultaAsync(
    string toEmail, string publisherNombre,
    string propertyTitulo, string userNombre, string mensaje);

Task SendNuevaRespuestaAsync(
    string toEmail, string userNombre,
    string propertyTitulo, string publisherNombre, string mensaje);
```

### ConsultasController (`PropertyMap.Api/Controllers/ConsultasController.cs`)

Ruta base: `api/consultas` — `[Authorize]` a nivel clase.

| Método | Ruta | Descripción |
|--------|------|-------------|
| POST | `/api/consultas` | Crea o continúa hilo. `CreateConsultaRequest`. |
| GET | `/api/consultas` | Bandeja del usuario autenticado. |
| GET | `/api/consultas/publisher` | Bandeja del publisher autenticado. Requiere rol Publisher. |
| GET | `/api/consultas/{id}` | Hilo completo. Solo accesible al dueño del hilo o al publisher de esa propiedad. |
| POST | `/api/consultas/{id}/mensajes` | Publisher responde. `SendMensajeRequest`. Solo publisher de esa propiedad. |

**Flujo POST `/api/consultas`:**
1. Extrae `userId` del claim.
2. Llama `GetOrCreateAsync(listingId, userId)` → obtiene/crea el hilo.
3. Agrega `ConsultaMensaje` con `EsDelPublisher = false` (incluye actualización de `FechaUltimoMensaje`).
4. Busca el publisher de la propiedad → crea notificación in-app `NuevaConsulta` + envía email `SendNuevaConsultaAsync` en try/catch.
6. Retorna `ConsultaDetailDto`.

**Flujo POST `/api/consultas/{id}/mensajes`:**
1. Carga el hilo → verifica que el publisher autenticado sea dueño de la propiedad.
2. Si no tiene acceso: 403.
3. Agrega `ConsultaMensaje` con `EsDelPublisher = true` (incluye actualización de `FechaUltimoMensaje`).
4. Notifica al usuario: notificación in-app `NuevaRespuesta` + email `SendNuevaRespuestaAsync` en try/catch.
6. Retorna el `ConsultaMensajeDto` agregado.

### Registro en Program.cs (API)

```csharp
builder.Services.AddScoped<IConsultaRepository, ConsultaRepository>();
```

---

## Frontend (Blazor)

### Nuevo servicio

**`IConsultasApiService`** (`PropertyMap.Web/PropertyMap.Web/Services/`)

```csharp
public interface IConsultasApiService
{
    Task<ConsultaDetailDto?> CreateOrContinueAsync(int listingId, string mensaje);
    Task<List<ConsultaSummaryDto>> GetMyConsultasAsync();
    Task<List<ConsultaSummaryDto>> GetPublisherConsultasAsync();
    Task<ConsultaDetailDto?> GetDetailAsync(int consultaId);
    Task<ConsultaMensajeDto?> ReplyAsync(int consultaId, string mensaje);
}
```

`ConsultasApiService` sigue el mismo patrón `IHttpClientFactory("api") + MemoryTokenStore.SetAuth()` que los demás servicios.

### File map

**Creados:**
```
PropertyMap.Web/.../Services/IConsultasApiService.cs
PropertyMap.Web/.../Services/ConsultasApiService.cs
PropertyMap.Web/.../Components/Pages/Account/Consultas.razor        (/account/consultas)
PropertyMap.Web/.../Components/Pages/Account/ConsultaDetalle.razor  (/account/consultas/{Id:int})
PropertyMap.Web/.../Components/Pages/Publisher/Consultas.razor      (/publisher/consultas)
PropertyMap.Web/.../Components/Pages/Publisher/ConsultaDetalle.razor(/publisher/consultas/{Id:int})
PropertyMap.Web/.../Components/Shared/ConsultaThread.razor
```

**Modificados:**
```
PropertyMap.Web/.../Program.cs                        (+ registro servicio)
PropertyMap.Web/.../Components/Layout/Navbar.razor    (+ link "Mis consultas")
PropertyMap.Web/.../Components/Pages/PropertyDetail.razor (+ botón "Consultar")
PropertyMap.Web/.../wwwroot/css/app.css               (+ estilos chat)
```

### Páginas

**`/account/consultas`** — Bandeja usuario
- `@rendermode InteractiveServer`, `<AuthorizeView>`
- `OnAfterRenderAsync`: `TryRestoreSessionAsync()` → `GetMyConsultasAsync()` → `StateHasChanged()`
- Lista de `ConsultaSummaryDto` con link a `/account/consultas/{id}`
- Estado vacío: "Todavía no iniciaste ninguna consulta."

**`/account/consultas/{Id:int}`** — Hilo (usuario)
- Carga `GetDetailAsync(Id)` en `OnAfterRenderAsync`
- Burbujas: mensajes del publisher a la izquierda (fondo gris), mensajes del usuario a la derecha (fondo azul)
- Input + botón "Enviar" abajo → `CreateOrContinueAsync(listing, mensaje)`

**`/publisher/consultas`** — Bandeja publisher
- Igual que la del usuario pero llama `GetPublisherConsultasAsync()`

**`/publisher/consultas/{Id:int}`** — Hilo (publisher)
- Carga `GetDetailAsync(Id)`
- Input + botón "Responder" → `ReplyAsync(Id, mensaje)`

### ConsultaThread.razor (componente compartido)

```razor
@* Parámetros *@
[Parameter] public List<ConsultaMensajeDto> Mensajes { get; set; } = [];
[Parameter] public bool EsPublisher { get; set; }   // invierte el lado de las burbujas
[Parameter] public EventCallback<string> OnSend { get; set; }
```

Render: bubbles alineadas según `EsDelPublisher XOR EsPublisher`. Input y botón "Enviar".

### PropertyDetail.razor — botón Consultar

Agregar dentro del bloque `<AuthorizeView>` (visible para todos los usuarios autenticados):

```razor
<a href="/account/consultas/nueva?listingId=@Id" class="btn-ghost">Consultar al publisher</a>
```

La ruta `/account/consultas/nueva?listingId={id}` lee el query param, llama `CreateOrContinueAsync` con un mensaje vacío inicial (o redirige al hilo ya existente si ya había uno). La alternativa más simple: el usuario navega a `/account/consultas` y desde ahí puede escribir la consulta con el listingId pre-seleccionado.

**Decisión de implementación:** la página `/account/consultas/{Id:int}` (hilo de usuario) acepta también la ruta `/account/consultas/nueva` con `[SupplyParameterFromQuery] int ListingId`. En `OnAfterRenderAsync`, si `ListingId > 0` y no tiene `Id` de consulta todavía, pre-carga el hilo (o lo crea al enviar el primer mensaje).

### Navbar

Dentro de `<AuthorizeView>` para todos los usuarios autenticados, agregar junto a "♥ Favoritos":
```razor
<a href="/account/consultas" class="btn-ghost">Consultas</a>
```

Y para el Publisher, agregar "Consultas" junto a "Mi panel".

---

## Testing

**Integración (API):**
- `ConsultasControllerTests`:
  - POST crea hilo y primer mensaje
  - POST al mismo listingId devuelve el mismo hilo con el nuevo mensaje (idempotent thread)
  - GET `/api/consultas/{id}` deniega acceso a terceros (403)
  - Publisher puede responder vía POST `/{id}/mensajes`
  - Usuario no puede usar el endpoint de publisher y viceversa (403)

**No se testea:**
- Frontend Blazor (manual)
- Email (NoOpEmailService en tests)

---

## Decisiones explícitas

- **Un hilo por usuario-propiedad**: unique index DB + `GetOrCreateAsync` garantiza esto.
- **Acceso al hilo**: solo el usuario dueño y el publisher de esa propiedad pueden ver/escribir.
- **Publisher identity**: se resuelve a través de `Publisher.UserId == currentUserId` — el publisher ve las consultas de las propiedades que le pertenecen.
- **Email fire-and-forget**: emails se envían en try/catch swallowed (como el view tracking) — un fallo de email no rompe la respuesta HTTP.
- **`EsDelPublisher` flag**: permite identificar el lado de la burbuja sin lookups adicionales al renderizar el chat.
- **PropertyQuestion/PropertyAnswer**: no se tocan — son entidades independientes del dominio (para futuro FAQ público si se quisiera).

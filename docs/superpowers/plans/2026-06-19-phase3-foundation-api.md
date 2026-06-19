# PropertyMap Phase 3 — Foundation & API Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Migrar PropertyMap a arquitectura API REST separada con JWT auth, domain model completo con 15 nuevas entidades, y refactorizar Blazor para consumir la API via HttpClient.

**Architecture:** Nuevo proyecto `PropertyMap.Api` (ASP.NET Core Web API) comparte `PropertyMap.Core` e `PropertyMap.Infrastructure` con el Blazor existente. Auth usa ASP.NET Core Identity + JWT access tokens (15 min) + refresh tokens rotativos (7 días) almacenados en `ApplicationUser`. Blazor reemplaza inyección directa de repos por `IListingApiService` (HttpClient wrapper).

**Tech Stack:** .NET 9, ASP.NET Core Web API, EF Core 9, ASP.NET Core Identity, `Microsoft.AspNetCore.Authentication.JwtBearer` 9.x, `System.IdentityModel.Tokens.Jwt` 8.x, MailKit 4.x, Swashbuckle 6.x, xUnit + `Microsoft.AspNetCore.Mvc.Testing`

---

## File Map

### Created
```
src/PropertyMap.Core/Entities/ApplicationUser.cs
src/PropertyMap.Core/Entities/PropertyImage.cs
src/PropertyMap.Core/Entities/PropertyView.cs
src/PropertyMap.Core/Entities/PropertyFavorite.cs
src/PropertyMap.Core/Entities/PropertyRating.cs
src/PropertyMap.Core/Entities/AgentRating.cs
src/PropertyMap.Core/Entities/PropertyQuestion.cs
src/PropertyMap.Core/Entities/PropertyAnswer.cs
src/PropertyMap.Core/Entities/Notification.cs
src/PropertyMap.Core/Entities/NotificationPreference.cs
src/PropertyMap.Core/Entities/Alert.cs
src/PropertyMap.Core/Entities/Report.cs
src/PropertyMap.Core/Entities/Plan.cs
src/PropertyMap.Core/Entities/Subscription.cs
src/PropertyMap.Core/Entities/AuditLog.cs
src/PropertyMap.Core/Enums/EstadoUsuario.cs
src/PropertyMap.Core/Enums/EstadoPublicacion.cs
src/PropertyMap.Core/Enums/TipoNotificacion.cs
src/PropertyMap.Core/Enums/MotivoReporte.cs
src/PropertyMap.Core/Enums/EstadoReporte.cs
src/PropertyMap.Core/Enums/EstadoSuscripcion.cs
src/PropertyMap.Core/DTOs/Auth/RegisterRequest.cs
src/PropertyMap.Core/DTOs/Auth/LoginRequest.cs
src/PropertyMap.Core/DTOs/Auth/AuthResponse.cs
src/PropertyMap.Core/DTOs/Auth/VerifyEmailRequest.cs
src/PropertyMap.Core/DTOs/Auth/ForgotPasswordRequest.cs
src/PropertyMap.Core/DTOs/Auth/ResetPasswordRequest.cs
src/PropertyMap.Core/DTOs/Auth/RefreshTokenRequest.cs
src/PropertyMap.Core/DTOs/ListingDetailDto.cs
src/PropertyMap.Core/Interfaces/ITokenService.cs
src/PropertyMap.Core/Interfaces/IEmailService.cs
src/PropertyMap.Infrastructure/Services/TokenService.cs
src/PropertyMap.Infrastructure/Services/EmailService.cs
src/PropertyMap.Api/PropertyMap.Api.csproj
src/PropertyMap.Api/Program.cs
src/PropertyMap.Api/appsettings.json
src/PropertyMap.Api/appsettings.Development.json
src/PropertyMap.Api/Controllers/AuthController.cs
src/PropertyMap.Api/Controllers/ListingsController.cs
src/PropertyMap.Web/Services/IListingApiService.cs
src/PropertyMap.Web/Services/ListingApiService.cs
src/PropertyMap.Tests/Api/TestWebApplicationFactory.cs
src/PropertyMap.Tests/Api/AuthControllerTests.cs
```

### Modified
```
src/PropertyMap.Core/Enums/TipoOperacion.cs         (+ 3 valores)
src/PropertyMap.Core/Enums/TipoPropiedad.cs          (+ 2 valores)
src/PropertyMap.Core/Entities/PropertyListing.cs     (EstadoPropiedad→EstadoPublicacion, Fotos→Images nav)
src/PropertyMap.Core/Entities/Publisher.cs           (+ User nav property)
src/PropertyMap.Infrastructure/Data/AppDbContext.cs  (IdentityDbContext<ApplicationUser> + 14 DbSets nuevos)
src/PropertyMap.Infrastructure/Data/DbSeeder.cs      (crea ApplicationUser real para Publisher seed)
src/PropertyMap.Infrastructure/PropertyMap.Infrastructure.csproj  (+ MailKit, JWT packages)
src/PropertyMap.Tests/PropertyMap.Tests.csproj       (+ Mvc.Testing)
src/PropertyMap.Web/Program.cs                       (+ HttpClient para ListingApiService)
src/PropertyMap.Web/Components/Pages/Home.razor      (usa IListingApiService)
src/PropertyMap.Web/Components/Pages/PropertyDetail.razor  (usa IListingApiService)
src/PropertyMap.Web/Components/Listings/PropertyCard.razor (usa FotoUrls en vez de Fotos)
PropertyMap.sln                                      (+ PropertyMap.Api)
```

---

## Task 1: Nuevos Enums (6 crear + 2 expandir)

**Files:**
- Create: `src/PropertyMap.Core/Enums/EstadoUsuario.cs`
- Create: `src/PropertyMap.Core/Enums/EstadoPublicacion.cs`
- Create: `src/PropertyMap.Core/Enums/TipoNotificacion.cs`
- Create: `src/PropertyMap.Core/Enums/MotivoReporte.cs`
- Create: `src/PropertyMap.Core/Enums/EstadoReporte.cs`
- Create: `src/PropertyMap.Core/Enums/EstadoSuscripcion.cs`
- Modify: `src/PropertyMap.Core/Enums/TipoOperacion.cs`
- Modify: `src/PropertyMap.Core/Enums/TipoPropiedad.cs`

- [ ] **Step 1: Crear los 6 enums nuevos**

`src/PropertyMap.Core/Enums/EstadoUsuario.cs`
```csharp
namespace PropertyMap.Core.Enums;

public enum EstadoUsuario
{
    PendienteVerificacion = 0,
    Activo = 1,
    Suspendido = 2,
    Eliminado = 3
}
```

`src/PropertyMap.Core/Enums/EstadoPublicacion.cs`
```csharp
namespace PropertyMap.Core.Enums;

public enum EstadoPublicacion
{
    Borrador = 0,
    PendienteAprobacion = 1,
    Publicada = 2,
    Pausada = 3,
    Vendida = 4,
    Alquilada = 5,
    Eliminada = 6
}
```

`src/PropertyMap.Core/Enums/TipoNotificacion.cs`
```csharp
namespace PropertyMap.Core.Enums;

public enum TipoNotificacion
{
    NuevaConsulta = 0,
    NuevaRespuesta = 1,
    AlertaCoincidencia = 2,
    Aprobacion = 3,
    Suspension = 4
}
```

`src/PropertyMap.Core/Enums/MotivoReporte.cs`
```csharp
namespace PropertyMap.Core.Enums;

public enum MotivoReporte
{
    Estafa = 0,
    InformacionFalsa = 1,
    Duplicado = 2,
    Spam = 3,
    Otro = 4
}
```

`src/PropertyMap.Core/Enums/EstadoReporte.cs`
```csharp
namespace PropertyMap.Core.Enums;

public enum EstadoReporte
{
    Pendiente = 0,
    EnRevision = 1,
    Resuelto = 2,
    Rechazado = 3
}
```

`src/PropertyMap.Core/Enums/EstadoSuscripcion.cs`
```csharp
namespace PropertyMap.Core.Enums;

public enum EstadoSuscripcion
{
    Activa = 0,
    Vencida = 1,
    Cancelada = 2,
    PendientePago = 3
}
```

- [ ] **Step 2: Expandir TipoOperacion** — agregar al final del enum existente:

```csharp
namespace PropertyMap.Core.Enums;

public enum TipoOperacion
{
    Venta = 0,
    Alquiler = 1,
    AlquilerTemporario = 2,
    Permuta = 3,
    Subasta = 4,
    ProyectoEnConstruccion = 5
}
```

- [ ] **Step 3: Expandir TipoPropiedad** — agregar al final del enum existente:

```csharp
namespace PropertyMap.Core.Enums;

public enum TipoPropiedad
{
    Departamento = 0,
    Casa = 1,
    Duplex = 2,
    PH = 3,
    Complejo = 4,
    Terreno = 5,
    Campo = 6,
    Local = 7,
    Oficina = 8,
    Cochera = 9,
    Otro = 10,
    Monoambiente = 11,
    Galpon = 12
}
```

- [ ] **Step 4: Build para verificar que compila**

```bash
cd C:\Agentes\PropertyMap
dotnet build src/PropertyMap.Core/PropertyMap.Core.csproj
```
Expected: `Build succeeded.`

- [ ] **Step 5: Commit**

```bash
git add src/PropertyMap.Core/Enums/
git commit -m "feat(core): add 6 new enums and expand TipoOperacion/TipoPropiedad"
```

---

## Task 2: ApplicationUser entity + actualizar Publisher

**Files:**
- Create: `src/PropertyMap.Core/Entities/ApplicationUser.cs`
- Modify: `src/PropertyMap.Core/Entities/Publisher.cs`

- [ ] **Step 1: Crear ApplicationUser**

`src/PropertyMap.Core/Entities/ApplicationUser.cs`
```csharp
using Microsoft.AspNetCore.Identity;
using PropertyMap.Core.Enums;

namespace PropertyMap.Core.Entities;

public class ApplicationUser : IdentityUser
{
    public string Nombre { get; set; } = "";
    public string Apellido { get; set; } = "";
    public string? AvatarUrl { get; set; }
    public EstadoUsuario Estado { get; set; } = EstadoUsuario.PendienteVerificacion;
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiry { get; set; }
    public string? EmailVerificationToken { get; set; }
    public DateTime? EmailVerificationExpiry { get; set; }
    public string? PasswordResetToken { get; set; }
    public DateTime? PasswordResetExpiry { get; set; }

    public Publisher? Publisher { get; set; }
    public ICollection<Notification> Notifications { get; set; } = [];
    public NotificationPreference? NotificationPreference { get; set; }
    public ICollection<PropertyFavorite> Favorites { get; set; } = [];
    public ICollection<PropertyRating> PropertyRatings { get; set; } = [];
    public ICollection<AgentRating> AgentRatings { get; set; } = [];
    public ICollection<PropertyQuestion> Questions { get; set; } = [];
    public ICollection<Alert> Alerts { get; set; } = [];
    public Subscription? Subscription { get; set; }
}
```

- [ ] **Step 2: Agregar navegación User a Publisher**

Abrir `src/PropertyMap.Core/Entities/Publisher.cs` y agregar la propiedad de navegación al final de la clase (la FK `UserId: string` ya existe — solo se agrega la nav):

```csharp
// Agregar junto a las propiedades existentes:
public ApplicationUser? User { get; set; }
```

- [ ] **Step 3: Build**

```bash
dotnet build src/PropertyMap.Core/PropertyMap.Core.csproj
```
Expected: `Build succeeded.`

- [ ] **Step 4: Commit**

```bash
git add src/PropertyMap.Core/Entities/
git commit -m "feat(core): add ApplicationUser entity and Publisher nav property"
```

---

## Task 3: PropertyImage entity + actualizar PropertyListing

**Files:**
- Create: `src/PropertyMap.Core/Entities/PropertyImage.cs`
- Modify: `src/PropertyMap.Core/Entities/PropertyListing.cs`

- [ ] **Step 1: Crear PropertyImage**

`src/PropertyMap.Core/Entities/PropertyImage.cs`
```csharp
namespace PropertyMap.Core.Entities;

public class PropertyImage
{
    public int Id { get; set; }
    public int PropertyListingId { get; set; }
    public PropertyListing PropertyListing { get; set; } = null!;
    public string Url { get; set; } = "";
    public int Orden { get; set; }
    public bool EsPrincipal { get; set; }
}
```

- [ ] **Step 2: Actualizar PropertyListing**

En `src/PropertyMap.Core/Entities/PropertyListing.cs`:
- Cambiar `public EstadoPropiedad Estado` → `public EstadoPublicacion Estado`
- Cambiar el default de `Estado` → `EstadoPublicacion.Borrador`
- Reemplazar `public List<string> Fotos { get; set; } = [];` por la nav property:
  `public ICollection<PropertyImage> Images { get; set; } = [];`
- Agregar `public DateTime FechaActualizacion { get; set; } = DateTime.UtcNow;`

El archivo completo actualizado:
```csharp
using PropertyMap.Core.Enums;

namespace PropertyMap.Core.Entities;

public class PropertyListing
{
    public int Id { get; set; }
    public int PublisherId { get; set; }
    public Publisher Publisher { get; set; } = null!;
    public int LocationId { get; set; }
    public Location Location { get; set; } = null!;
    public string Titulo { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public decimal Precio { get; set; }
    public string Moneda { get; set; } = "USD";
    public TipoPropiedad TipoPropiedad { get; set; }
    public TipoOperacion Operacion { get; set; }
    public decimal? Superficie { get; set; }
    public decimal? SuperficieCubierta { get; set; }
    public int? Ambientes { get; set; }
    public int? Dormitorios { get; set; }
    public int? Banos { get; set; }
    public int? Antiguedad { get; set; }
    public bool Cochera { get; set; }
    public List<string> Amenities { get; set; } = [];
    public EstadoPublicacion Estado { get; set; } = EstadoPublicacion.Borrador;
    public DateTime FechaPublicacion { get; set; } = DateTime.UtcNow;
    public DateTime FechaActualizacion { get; set; } = DateTime.UtcNow;

    public ICollection<PropertyImage> Images { get; set; } = [];
    public ICollection<PropertyView> Views { get; set; } = [];
    public ICollection<PropertyFavorite> Favorites { get; set; } = [];
    public ICollection<PropertyRating> Ratings { get; set; } = [];
    public ICollection<PropertyQuestion> Questions { get; set; } = [];
    public ICollection<Report> Reports { get; set; } = [];
}
```

- [ ] **Step 3: Build**

```bash
dotnet build src/PropertyMap.Core/PropertyMap.Core.csproj
```
Expected: `Build succeeded.` Si hay errores de `EstadoPropiedad` en otros archivos, reemplazar con `EstadoPublicacion`.

- [ ] **Step 4: Commit**

```bash
git add src/PropertyMap.Core/Entities/
git commit -m "feat(core): add PropertyImage entity, migrate PropertyListing to EstadoPublicacion"
```

---

## Task 4: Entidades sociales (PropertyView, PropertyFavorite)

**Files:**
- Create: `src/PropertyMap.Core/Entities/PropertyView.cs`
- Create: `src/PropertyMap.Core/Entities/PropertyFavorite.cs`

- [ ] **Step 1: Crear PropertyView**

`src/PropertyMap.Core/Entities/PropertyView.cs`
```csharp
namespace PropertyMap.Core.Entities;

public class PropertyView
{
    public int Id { get; set; }
    public int PropertyListingId { get; set; }
    public PropertyListing PropertyListing { get; set; } = null!;
    public string? UserId { get; set; }
    public ApplicationUser? User { get; set; }
    public DateTime FechaVista { get; set; } = DateTime.UtcNow;
    public string? IpAddress { get; set; }
}
```

- [ ] **Step 2: Crear PropertyFavorite**

`src/PropertyMap.Core/Entities/PropertyFavorite.cs`
```csharp
namespace PropertyMap.Core.Entities;

public class PropertyFavorite
{
    public int Id { get; set; }
    public int PropertyListingId { get; set; }
    public PropertyListing PropertyListing { get; set; } = null!;
    public string UserId { get; set; } = "";
    public ApplicationUser User { get; set; } = null!;
    public DateTime FechaAgregado { get; set; } = DateTime.UtcNow;
}
```

- [ ] **Step 3: Build**

```bash
dotnet build src/PropertyMap.Core/PropertyMap.Core.csproj
```
Expected: `Build succeeded.`

- [ ] **Step 4: Commit**

```bash
git add src/PropertyMap.Core/Entities/PropertyView.cs src/PropertyMap.Core/Entities/PropertyFavorite.cs
git commit -m "feat(core): add PropertyView and PropertyFavorite entities"
```

---

## Task 5: Entidades de valoración (PropertyRating, AgentRating)

**Files:**
- Create: `src/PropertyMap.Core/Entities/PropertyRating.cs`
- Create: `src/PropertyMap.Core/Entities/AgentRating.cs`

- [ ] **Step 1: Crear PropertyRating**

`src/PropertyMap.Core/Entities/PropertyRating.cs`
```csharp
namespace PropertyMap.Core.Entities;

public class PropertyRating
{
    public int Id { get; set; }
    public int PropertyListingId { get; set; }
    public PropertyListing PropertyListing { get; set; } = null!;
    public string UserId { get; set; } = "";
    public ApplicationUser User { get; set; } = null!;
    public int PuntajeUbicacion { get; set; }
    public int PuntajeEstado { get; set; }
    public int PuntajePrecioCalidad { get; set; }
    public string? Comentario { get; set; }
    public DateTime FechaValoracion { get; set; } = DateTime.UtcNow;
}
```

- [ ] **Step 2: Crear AgentRating**

`src/PropertyMap.Core/Entities/AgentRating.cs`
```csharp
namespace PropertyMap.Core.Entities;

public class AgentRating
{
    public int Id { get; set; }
    public int PublisherId { get; set; }
    public Publisher Publisher { get; set; } = null!;
    public string UserId { get; set; } = "";
    public ApplicationUser User { get; set; } = null!;
    public int PuntajeAtencion { get; set; }
    public int PuntajeRapidez { get; set; }
    public int PuntajeTransparencia { get; set; }
    public int PuntajeProfesionalismo { get; set; }
    public string? Comentario { get; set; }
    public DateTime FechaValoracion { get; set; } = DateTime.UtcNow;
}
```

- [ ] **Step 3: Agregar AgentRatings a Publisher** — en `src/PropertyMap.Core/Entities/Publisher.cs` agregar:

```csharp
public ICollection<AgentRating> Ratings { get; set; } = [];
```

- [ ] **Step 4: Build**

```bash
dotnet build src/PropertyMap.Core/PropertyMap.Core.csproj
```
Expected: `Build succeeded.`

- [ ] **Step 5: Commit**

```bash
git add src/PropertyMap.Core/Entities/
git commit -m "feat(core): add PropertyRating and AgentRating entities"
```

---

## Task 6: Entidades de consultas (PropertyQuestion, PropertyAnswer)

**Files:**
- Create: `src/PropertyMap.Core/Entities/PropertyQuestion.cs`
- Create: `src/PropertyMap.Core/Entities/PropertyAnswer.cs`

- [ ] **Step 1: Crear PropertyQuestion**

`src/PropertyMap.Core/Entities/PropertyQuestion.cs`
```csharp
namespace PropertyMap.Core.Entities;

public class PropertyQuestion
{
    public int Id { get; set; }
    public int PropertyListingId { get; set; }
    public PropertyListing PropertyListing { get; set; } = null!;
    public string UserId { get; set; } = "";
    public ApplicationUser User { get; set; } = null!;
    public string Mensaje { get; set; } = "";
    public DateTime FechaPregunta { get; set; } = DateTime.UtcNow;

    public ICollection<PropertyAnswer> Answers { get; set; } = [];
}
```

- [ ] **Step 2: Crear PropertyAnswer**

`src/PropertyMap.Core/Entities/PropertyAnswer.cs`
```csharp
namespace PropertyMap.Core.Entities;

public class PropertyAnswer
{
    public int Id { get; set; }
    public int PropertyQuestionId { get; set; }
    public PropertyQuestion PropertyQuestion { get; set; } = null!;
    public int PublisherId { get; set; }
    public Publisher Publisher { get; set; } = null!;
    public string Mensaje { get; set; } = "";
    public DateTime FechaRespuesta { get; set; } = DateTime.UtcNow;
}
```

- [ ] **Step 3: Build**

```bash
dotnet build src/PropertyMap.Core/PropertyMap.Core.csproj
```
Expected: `Build succeeded.`

- [ ] **Step 4: Commit**

```bash
git add src/PropertyMap.Core/Entities/PropertyQuestion.cs src/PropertyMap.Core/Entities/PropertyAnswer.cs
git commit -m "feat(core): add PropertyQuestion and PropertyAnswer entities"
```

---

## Task 7: Entidades de notificaciones y alertas

**Files:**
- Create: `src/PropertyMap.Core/Entities/Notification.cs`
- Create: `src/PropertyMap.Core/Entities/NotificationPreference.cs`
- Create: `src/PropertyMap.Core/Entities/Alert.cs`

- [ ] **Step 1: Crear Notification**

`src/PropertyMap.Core/Entities/Notification.cs`
```csharp
using PropertyMap.Core.Enums;

namespace PropertyMap.Core.Entities;

public class Notification
{
    public int Id { get; set; }
    public string UserId { get; set; } = "";
    public ApplicationUser User { get; set; } = null!;
    public TipoNotificacion Tipo { get; set; }
    public string Titulo { get; set; } = "";
    public string Mensaje { get; set; } = "";
    public bool Leida { get; set; }
    public string? UrlAccion { get; set; }
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
}
```

- [ ] **Step 2: Crear NotificationPreference**

`src/PropertyMap.Core/Entities/NotificationPreference.cs`
```csharp
namespace PropertyMap.Core.Entities;

public class NotificationPreference
{
    public int Id { get; set; }
    public string UserId { get; set; } = "";
    public ApplicationUser User { get; set; } = null!;
    public bool RecibirEmail { get; set; } = true;
    public bool RecibirPush { get; set; } = true;
    public bool NuevasConsultas { get; set; } = true;
    public bool NuevasRespuestas { get; set; } = true;
    public bool AlertasCoincidencia { get; set; } = true;
}
```

- [ ] **Step 3: Crear Alert**

`src/PropertyMap.Core/Entities/Alert.cs`
```csharp
using PropertyMap.Core.Enums;

namespace PropertyMap.Core.Entities;

public class Alert
{
    public int Id { get; set; }
    public string UserId { get; set; } = "";
    public ApplicationUser User { get; set; } = null!;
    public string? Nombre { get; set; }
    public TipoOperacion? Operacion { get; set; }
    public TipoPropiedad? TipoPropiedad { get; set; }
    public string? Ciudad { get; set; }
    public decimal? PrecioMax { get; set; }
    public string? Moneda { get; set; }
    public int? DormitoriosMin { get; set; }
    public bool Activa { get; set; } = true;
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
}
```

- [ ] **Step 4: Build**

```bash
dotnet build src/PropertyMap.Core/PropertyMap.Core.csproj
```
Expected: `Build succeeded.`

- [ ] **Step 5: Commit**

```bash
git add src/PropertyMap.Core/Entities/Notification.cs src/PropertyMap.Core/Entities/NotificationPreference.cs src/PropertyMap.Core/Entities/Alert.cs
git commit -m "feat(core): add Notification, NotificationPreference, and Alert entities"
```

---

## Task 8: Entidades de administración (Report, Plan, Subscription, AuditLog)

**Files:**
- Create: `src/PropertyMap.Core/Entities/Report.cs`
- Create: `src/PropertyMap.Core/Entities/Plan.cs`
- Create: `src/PropertyMap.Core/Entities/Subscription.cs`
- Create: `src/PropertyMap.Core/Entities/AuditLog.cs`

- [ ] **Step 1: Crear Report**

`src/PropertyMap.Core/Entities/Report.cs`
```csharp
using PropertyMap.Core.Enums;

namespace PropertyMap.Core.Entities;

public class Report
{
    public int Id { get; set; }
    public int PropertyListingId { get; set; }
    public PropertyListing PropertyListing { get; set; } = null!;
    public string UserId { get; set; } = "";
    public ApplicationUser User { get; set; } = null!;
    public MotivoReporte Motivo { get; set; }
    public string? Descripcion { get; set; }
    public EstadoReporte Estado { get; set; } = EstadoReporte.Pendiente;
    public DateTime FechaReporte { get; set; } = DateTime.UtcNow;
}
```

- [ ] **Step 2: Crear Plan**

`src/PropertyMap.Core/Entities/Plan.cs`
```csharp
namespace PropertyMap.Core.Entities;

public class Plan
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public string Slug { get; set; } = "";
    public decimal PrecioMensual { get; set; }
    public string Moneda { get; set; } = "ARS";
    public int? MaxPublicaciones { get; set; }
    public int DestacadosIncluidos { get; set; }
    public bool EstadisticasAvanzadas { get; set; }
    public bool Activo { get; set; } = true;

    public ICollection<Subscription> Subscriptions { get; set; } = [];
}
```

- [ ] **Step 3: Crear Subscription**

`src/PropertyMap.Core/Entities/Subscription.cs`
```csharp
using PropertyMap.Core.Enums;

namespace PropertyMap.Core.Entities;

public class Subscription
{
    public int Id { get; set; }
    public string UserId { get; set; } = "";
    public ApplicationUser User { get; set; } = null!;
    public int PlanId { get; set; }
    public Plan Plan { get; set; } = null!;
    public EstadoSuscripcion Estado { get; set; } = EstadoSuscripcion.Activa;
    public DateTime FechaInicio { get; set; } = DateTime.UtcNow;
    public DateTime FechaVencimiento { get; set; }
    public bool AutoRenovar { get; set; } = true;
}
```

- [ ] **Step 4: Crear AuditLog**

`src/PropertyMap.Core/Entities/AuditLog.cs`
```csharp
namespace PropertyMap.Core.Entities;

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

- [ ] **Step 5: Build completo de Core**

```bash
dotnet build src/PropertyMap.Core/PropertyMap.Core.csproj
```
Expected: `Build succeeded.`

- [ ] **Step 6: Commit**

```bash
git add src/PropertyMap.Core/Entities/
git commit -m "feat(core): add Report, Plan, Subscription, and AuditLog entities"
```

---

## Task 9: Actualizar AppDbContext + migración + DbSeeder

**Files:**
- Modify: `src/PropertyMap.Infrastructure/Data/AppDbContext.cs`
- Modify: `src/PropertyMap.Infrastructure/Data/DbSeeder.cs`
- Modify: `src/PropertyMap.Infrastructure/PropertyMap.Infrastructure.csproj`

- [ ] **Step 1: Agregar paquetes a Infrastructure**

```bash
cd C:\Agentes\PropertyMap\src\PropertyMap.Infrastructure
dotnet add package MailKit --version 4.*
dotnet add package System.IdentityModel.Tokens.Jwt --version 8.*
dotnet add package Microsoft.IdentityModel.Tokens --version 8.*
```

- [ ] **Step 2: Actualizar AppDbContext**

Reemplazar el contenido de `src/PropertyMap.Infrastructure/Data/AppDbContext.cs`:

```csharp
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using PropertyMap.Core.Entities;
using PropertyMap.Core.Enums;

namespace PropertyMap.Infrastructure.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Location> Locations => Set<Location>();
    public DbSet<Publisher> Publishers => Set<Publisher>();
    public DbSet<PropertyListing> PropertyListings => Set<PropertyListing>();
    public DbSet<PropertyImage> PropertyImages => Set<PropertyImage>();
    public DbSet<PropertyView> PropertyViews => Set<PropertyView>();
    public DbSet<PropertyFavorite> PropertyFavorites => Set<PropertyFavorite>();
    public DbSet<PropertyRating> PropertyRatings => Set<PropertyRating>();
    public DbSet<AgentRating> AgentRatings => Set<AgentRating>();
    public DbSet<PropertyQuestion> PropertyQuestions => Set<PropertyQuestion>();
    public DbSet<PropertyAnswer> PropertyAnswers => Set<PropertyAnswer>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();
    public DbSet<Alert> Alerts => Set<Alert>();
    public DbSet<Report> Reports => Set<Report>();
    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // PropertyListing
        modelBuilder.Entity<PropertyListing>()
            .Property(p => p.Precio).HasColumnType("decimal(18,2)");
        modelBuilder.Entity<PropertyListing>()
            .Property(p => p.Superficie).HasColumnType("decimal(10,2)");
        modelBuilder.Entity<PropertyListing>()
            .Property(p => p.SuperficieCubierta).HasColumnType("decimal(10,2)");

        var listComparer = new ValueComparer<List<string>>(
            (c1, c2) => c1!.SequenceEqual(c2!),
            c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
            c => c.ToList());

        modelBuilder.Entity<PropertyListing>()
            .Property(p => p.Amenities)
            .HasConversion(
                v => System.Text.Json.JsonSerializer.Serialize(v, (System.Text.Json.JsonSerializerOptions?)null),
                v => System.Text.Json.JsonSerializer.Deserialize<List<string>>(v, (System.Text.Json.JsonSerializerOptions?)null) ?? new List<string>())
            .Metadata.SetValueComparer(listComparer);

        // Publisher → ApplicationUser (1:1)
        modelBuilder.Entity<Publisher>()
            .HasOne(p => p.User)
            .WithOne(u => u.Publisher)
            .HasForeignKey<Publisher>(p => p.UserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        // PropertyFavorite unique constraint
        modelBuilder.Entity<PropertyFavorite>()
            .HasIndex(f => new { f.UserId, f.PropertyListingId })
            .IsUnique();

        // PropertyRating unique constraint
        modelBuilder.Entity<PropertyRating>()
            .HasIndex(r => new { r.UserId, r.PropertyListingId })
            .IsUnique();

        // AgentRating unique constraint
        modelBuilder.Entity<AgentRating>()
            .HasIndex(r => new { r.UserId, r.PublisherId })
            .IsUnique();

        // NotificationPreference unique constraint
        modelBuilder.Entity<NotificationPreference>()
            .HasIndex(np => np.UserId)
            .IsUnique();

        // Subscription unique constraint (one active subscription per user)
        modelBuilder.Entity<Subscription>()
            .HasIndex(s => s.UserId)
            .IsUnique();

        // Plan
        modelBuilder.Entity<Plan>()
            .Property(p => p.PrecioMensual).HasColumnType("decimal(18,2)");
        modelBuilder.Entity<Plan>()
            .HasIndex(p => p.Slug).IsUnique();

        // AuditLog — no FK to ApplicationUser (string nullable, for deleted users)
        modelBuilder.Entity<AuditLog>()
            .HasNoKey();
        modelBuilder.Entity<AuditLog>()
            .ToTable("AuditLogs")
            .HasKey(a => a.Id);

        // PropertyView — optional FK (anonymous views)
        modelBuilder.Entity<PropertyView>()
            .HasOne(v => v.User)
            .WithMany()
            .HasForeignKey(v => v.UserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        // AgentRating cascade
        modelBuilder.Entity<AgentRating>()
            .HasOne(r => r.Publisher)
            .WithMany(p => p.Ratings)
            .HasForeignKey(r => r.PublisherId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<AgentRating>()
            .HasOne(r => r.User)
            .WithMany(u => u.AgentRatings)
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.NoAction);

        // PropertyRating cascade
        modelBuilder.Entity<PropertyRating>()
            .HasOne(r => r.User)
            .WithMany(u => u.PropertyRatings)
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.NoAction);

        // PropertyQuestion cascade
        modelBuilder.Entity<PropertyQuestion>()
            .HasOne(q => q.User)
            .WithMany(u => u.Questions)
            .HasForeignKey(q => q.UserId)
            .OnDelete(DeleteBehavior.NoAction);

        // Report cascade
        modelBuilder.Entity<Report>()
            .HasOne(r => r.User)
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.NoAction);

        // Notification cascade
        modelBuilder.Entity<Notification>()
            .HasOne(n => n.User)
            .WithMany(u => u.Notifications)
            .HasForeignKey(n => n.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Alert cascade
        modelBuilder.Entity<Alert>()
            .HasOne(a => a.User)
            .WithMany(u => u.Alerts)
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // PropertyFavorite cascade
        modelBuilder.Entity<PropertyFavorite>()
            .HasOne(f => f.User)
            .WithMany(u => u.Favorites)
            .HasForeignKey(f => f.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // NotificationPreference cascade
        modelBuilder.Entity<NotificationPreference>()
            .HasOne(np => np.User)
            .WithOne(u => u.NotificationPreference)
            .HasForeignKey<NotificationPreference>(np => np.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Subscription cascade
        modelBuilder.Entity<Subscription>()
            .HasOne(s => s.User)
            .WithOne(u => u.Subscription)
            .HasForeignKey<Subscription>(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
```

- [ ] **Step 3: Actualizar DbSeeder**

Reemplazar el contenido de `src/PropertyMap.Infrastructure/Data/DbSeeder.cs`. El seeder ahora crea un `ApplicationUser` real en lugar del `UserId = "seed-user"` suelto. El contenido de propiedades y fotos se migra a `PropertyImage` rows:

```csharp
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PropertyMap.Core.Entities;
using PropertyMap.Core.Enums;

namespace PropertyMap.Infrastructure.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(AppDbContext context, UserManager<ApplicationUser> userManager)
    {
        if (await context.PropertyListings.AnyAsync()) return;

        // Seed publisher user
        var publisherUser = new ApplicationUser
        {
            Id = "seed-publisher-user",
            UserName = "contacto@savoiaaltolaguirre.com.ar",
            Email = "contacto@savoiaaltolaguirre.com.ar",
            EmailConfirmed = true,
            Nombre = "Savoia Alto",
            Apellido = "Laguirre",
            Estado = EstadoUsuario.Activo,
            FechaRegistro = DateTime.UtcNow,
            PhoneNumber = "+54 9 2954 520117"
        };

        if (await userManager.FindByIdAsync(publisherUser.Id) == null)
        {
            await userManager.CreateAsync(publisherUser, "Savoia123!");
            await userManager.AddToRoleAsync(publisherUser, "Publisher");
        }

        var publisher = new Publisher
        {
            Nombre = "Savoia Alto Laguirre",
            Email = "contacto@savoiaaltolaguirre.com.ar",
            Telefono = "+54 9 2954 520117",
            Tipo = TipoPublicador.Inmobiliaria,
            UserId = publisherUser.Id
        };
        context.Publishers.Add(publisher);
        await context.SaveChangesAsync();

        // Seed locations
        var locations = new List<Location>
        {
            new() { DireccionTexto = "Armesto 586", Ciudad = "Santa Rosa", Provincia = "La Pampa", Latitud = -36.64269, Longitud = -64.31722 },
            new() { DireccionTexto = "Av. España 316", Ciudad = "Santa Rosa", Provincia = "La Pampa", Latitud = -36.62551, Longitud = -64.28710 },
            new() { DireccionTexto = "Emilio Civit 2765", Ciudad = "Santa Rosa", Provincia = "La Pampa", Latitud = -36.64751, Longitud = -64.31145 },
            new() { DireccionTexto = "Tierra del Fuego 133", Ciudad = "Santa Rosa", Provincia = "La Pampa", Latitud = -36.60744, Longitud = -64.28384 },
            new() { DireccionTexto = "Arenales 437", Ciudad = "Santa Rosa", Provincia = "La Pampa", Latitud = -36.61671, Longitud = -64.27236 },
            new() { DireccionTexto = "La Rioja 307", Ciudad = "Santa Rosa", Provincia = "La Pampa", Latitud = -36.61399, Longitud = -64.29120 },
            new() { DireccionTexto = "General Pico 464", Ciudad = "Santa Rosa", Provincia = "La Pampa", Latitud = -36.62163, Longitud = -64.29390 },
            new() { DireccionTexto = "Paloma Torcaza 725", Ciudad = "Toay", Provincia = "La Pampa", Latitud = -36.64019, Longitud = -64.37139 }
        };
        context.Locations.AddRange(locations);
        await context.SaveChangesAsync();

        // Seed listings with PropertyImage rows
        var listingsData = new[]
        {
            new
            {
                Titulo = "Casa en Nueva Vista - Inti Hue", Tipo = TipoPropiedad.Casa, Op = TipoOperacion.Venta,
                Precio = 85000m, Moneda = "USD", Sup = 97m, Dorms = 1, Banos = 1, Ambientes = 2,
                Slug = "armesto-586", LocationIndex = 0,
                Fotos = new[] { "img1.jpg", "img2.jpg", "img3.jpg" }
            },
            new
            {
                Titulo = "Torre Av. España - Depto 2 dorm", Tipo = TipoPropiedad.Departamento, Op = TipoOperacion.Venta,
                Precio = 168000m, Moneda = "USD", Sup = 168m, Dorms = 2, Banos = 2, Ambientes = 4,
                Slug = "av-espana-316", LocationIndex = 1,
                Fotos = new[] { "img1.jpg", "img2.jpg", "img3.jpg", "img4.jpg" }
            },
            new
            {
                Titulo = "Casa 3 dorm con cochera - E. Civit", Tipo = TipoPropiedad.Casa, Op = TipoOperacion.Venta,
                Precio = 120000m, Moneda = "USD", Sup = 142m, Dorms = 3, Banos = 2, Ambientes = 5,
                Slug = "emilio-civit-2765", LocationIndex = 2,
                Fotos = new[] { "img1.jpg", "img2.jpg" }
            },
            new
            {
                Titulo = "Depto 1 dorm - Tierra del Fuego", Tipo = TipoPropiedad.Departamento, Op = TipoOperacion.Venta,
                Precio = 55000m, Moneda = "USD", Sup = 71m, Dorms = 1, Banos = 1, Ambientes = 2,
                Slug = "tierra-del-fuego-133", LocationIndex = 3,
                Fotos = new[] { "img1.jpg", "img2.jpg", "img3.jpg" }
            },
            new
            {
                Titulo = "Monoambiente - Villa Alonso", Tipo = TipoPropiedad.Monoambiente, Op = TipoOperacion.Venta,
                Precio = 35000m, Moneda = "USD", Sup = 28m, Dorms = 1, Banos = 1, Ambientes = 1,
                Slug = "arenales-437", LocationIndex = 4,
                Fotos = new[] { "img1.jpg", "img2.jpg" }
            },
            new
            {
                Titulo = "Depto céntrico - La Rioja esq. Catamarca", Tipo = TipoPropiedad.Departamento, Op = TipoOperacion.Alquiler,
                Precio = 180000m, Moneda = "ARS", Sup = 32m, Dorms = 1, Banos = 1, Ambientes = 2,
                Slug = "la-rioja-307", LocationIndex = 5,
                Fotos = new[] { "img1.jpg", "img2.jpg", "img3.jpg" }
            },
            new
            {
                Titulo = "Casa 3 dorm con pileta - Gral. Pico", Tipo = TipoPropiedad.Casa, Op = TipoOperacion.Venta,
                Precio = 98000m, Moneda = "USD", Sup = 114m, Dorms = 3, Banos = 2, Ambientes = 5,
                Slug = "pico-464", LocationIndex = 6,
                Fotos = new[] { "img1.jpg", "img2.jpg", "img3.jpg", "img4.jpg" }
            },
            new
            {
                Titulo = "Casa premium con pileta - Toay", Tipo = TipoPropiedad.Casa, Op = TipoOperacion.Venta,
                Precio = 195000m, Moneda = "USD", Sup = 239m, Dorms = 3, Banos = 2, Ambientes = 7,
                Slug = "torcaza-725", LocationIndex = 7,
                Fotos = new[] { "img1.jpg", "img2.jpg", "img3.jpg", "img4.jpg", "img5.jpg" }
            }
        };

        foreach (var data in listingsData)
        {
            var listing = new PropertyListing
            {
                Publisher = publisher,
                Location = locations[data.LocationIndex],
                Titulo = data.Titulo,
                Descripcion = $"Propiedad en {locations[data.LocationIndex].DireccionTexto}, {locations[data.LocationIndex].Ciudad}.",
                Precio = data.Precio,
                Moneda = data.Moneda,
                TipoPropiedad = data.Tipo,
                Operacion = data.Op,
                Superficie = data.Sup,
                Ambientes = data.Ambientes,
                Dormitorios = data.Dorms,
                Banos = data.Banos,
                Cochera = false,
                Estado = EstadoPublicacion.Publicada,
                FechaPublicacion = DateTime.UtcNow
            };

            for (int i = 0; i < data.Fotos.Length; i++)
            {
                listing.Images.Add(new PropertyImage
                {
                    Url = $"/images/properties/{data.Slug}/{data.Fotos[i]}",
                    Orden = i,
                    EsPrincipal = i == 0
                });
            }

            context.PropertyListings.Add(listing);
        }

        await context.SaveChangesAsync();
    }
}
```

- [ ] **Step 4: Generar migración**

```bash
cd C:\Agentes\PropertyMap
dotnet ef migrations add Phase3DomainModel --project src/PropertyMap.Infrastructure --startup-project src/PropertyMap.Web
```

Expected: Crear archivo `src/PropertyMap.Infrastructure/Migrations/YYYYMMDD_Phase3DomainModel.cs`

- [ ] **Step 5: Agregar data migration en el archivo generado**

Abrir el archivo de migración recién creado y agregar en el método `Up()`, al final antes del cierre, la siguiente línea para migrar el estado de las propiedades existentes (Activa=0 → Publicada=2):

```csharp
// Migrar EstadoPropiedad viejo a EstadoPublicacion nuevo
// Old: Activa=0, Pausada=1, Vendida=2
// New: Publicada=2, Pausada=3, Vendida=4
migrationBuilder.Sql("UPDATE PropertyListings SET Estado = CASE Estado WHEN 2 THEN 4 WHEN 1 THEN 3 WHEN 0 THEN 2 ELSE 2 END");
```

- [ ] **Step 6: Aplicar migración**

```bash
dotnet ef database update --project src/PropertyMap.Infrastructure --startup-project src/PropertyMap.Web
```

Expected: `Done. Migration 'Phase3DomainModel' applied.`

- [ ] **Step 7: Verificar build completo**

```bash
dotnet build C:\Agentes\PropertyMap\PropertyMap.sln
```
Expected: `Build succeeded.`

- [ ] **Step 8: Commit**

```bash
git add src/PropertyMap.Infrastructure/
git commit -m "feat(infra): update AppDbContext to IdentityDbContext<ApplicationUser>, add 14 new DbSets, Phase3 migration"
```

---

## Task 10: Auth DTOs + Interfaces en Core

**Files:**
- Create: `src/PropertyMap.Core/DTOs/Auth/RegisterRequest.cs`
- Create: `src/PropertyMap.Core/DTOs/Auth/LoginRequest.cs`
- Create: `src/PropertyMap.Core/DTOs/Auth/AuthResponse.cs`
- Create: `src/PropertyMap.Core/DTOs/Auth/VerifyEmailRequest.cs`
- Create: `src/PropertyMap.Core/DTOs/Auth/ForgotPasswordRequest.cs`
- Create: `src/PropertyMap.Core/DTOs/Auth/ResetPasswordRequest.cs`
- Create: `src/PropertyMap.Core/DTOs/Auth/RefreshTokenRequest.cs`
- Create: `src/PropertyMap.Core/DTOs/ListingDetailDto.cs`
- Create: `src/PropertyMap.Core/Interfaces/ITokenService.cs`
- Create: `src/PropertyMap.Core/Interfaces/IEmailService.cs`

- [ ] **Step 1: Crear carpeta y DTOs de Auth**

```bash
mkdir "C:\Agentes\PropertyMap\src\PropertyMap.Core\DTOs\Auth"
```

`src/PropertyMap.Core/DTOs/Auth/RegisterRequest.cs`
```csharp
namespace PropertyMap.Core.DTOs.Auth;

public record RegisterRequest(
    string Nombre,
    string Apellido,
    string Email,
    string Password,
    string ConfirmPassword
);
```

`src/PropertyMap.Core/DTOs/Auth/LoginRequest.cs`
```csharp
namespace PropertyMap.Core.DTOs.Auth;

public record LoginRequest(string Email, string Password);
```

`src/PropertyMap.Core/DTOs/Auth/AuthResponse.cs`
```csharp
namespace PropertyMap.Core.DTOs.Auth;

public record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiry,
    string UserId,
    string Email,
    string NombreCompleto,
    IList<string> Roles
);
```

`src/PropertyMap.Core/DTOs/Auth/VerifyEmailRequest.cs`
```csharp
namespace PropertyMap.Core.DTOs.Auth;

public record VerifyEmailRequest(string Email, string Token);
```

`src/PropertyMap.Core/DTOs/Auth/ForgotPasswordRequest.cs`
```csharp
namespace PropertyMap.Core.DTOs.Auth;

public record ForgotPasswordRequest(string Email);
```

`src/PropertyMap.Core/DTOs/Auth/ResetPasswordRequest.cs`
```csharp
namespace PropertyMap.Core.DTOs.Auth;

public record ResetPasswordRequest(
    string Email,
    string Token,
    string NewPassword,
    string ConfirmNewPassword
);
```

`src/PropertyMap.Core/DTOs/Auth/RefreshTokenRequest.cs`
```csharp
namespace PropertyMap.Core.DTOs.Auth;

public record RefreshTokenRequest(string AccessToken, string RefreshToken);
```

- [ ] **Step 2: Crear ListingDetailDto**

`src/PropertyMap.Core/DTOs/ListingDetailDto.cs`
```csharp
namespace PropertyMap.Core.DTOs;

public record ListingDetailDto(
    int Id,
    string Titulo,
    string Descripcion,
    decimal Precio,
    string Moneda,
    string TipoPropiedad,
    string Operacion,
    string DireccionTexto,
    string Ciudad,
    string Provincia,
    double Lat,
    double Lng,
    decimal? Superficie,
    decimal? SuperficieCubierta,
    int? Ambientes,
    int? Dormitorios,
    int? Banos,
    int? Antiguedad,
    bool Cochera,
    List<string> Amenities,
    List<string> FotoUrls,
    string PublisherNombre,
    string? PublisherTelefono,
    string? PublisherLogoUrl,
    DateTime FechaPublicacion
);
```

- [ ] **Step 3: Crear ITokenService**

`src/PropertyMap.Core/Interfaces/ITokenService.cs`
```csharp
using System.Security.Claims;
using PropertyMap.Core.Entities;

namespace PropertyMap.Core.Interfaces;

public interface ITokenService
{
    string GenerateAccessToken(ApplicationUser user, IList<string> roles);
    string GenerateRefreshToken();
    ClaimsPrincipal? ValidateExpiredToken(string token);
}
```

- [ ] **Step 4: Crear IEmailService**

`src/PropertyMap.Core/Interfaces/IEmailService.cs`
```csharp
namespace PropertyMap.Core.Interfaces;

public interface IEmailService
{
    Task SendEmailVerificationAsync(string toEmail, string toName, string token);
    Task SendPasswordResetAsync(string toEmail, string toName, string token, string resetUrl);
    Task SendWelcomeAsync(string toEmail, string toName);
}
```

- [ ] **Step 5: Build**

```bash
dotnet build src/PropertyMap.Core/PropertyMap.Core.csproj
```
Expected: `Build succeeded.`

- [ ] **Step 6: Commit**

```bash
git add src/PropertyMap.Core/DTOs/ src/PropertyMap.Core/Interfaces/
git commit -m "feat(core): add auth DTOs, ListingDetailDto, ITokenService, IEmailService"
```

---

## Task 11: Crear proyecto PropertyMap.Api

**Files:**
- Create: `src/PropertyMap.Api/PropertyMap.Api.csproj`
- Create: `src/PropertyMap.Api/Program.cs`
- Create: `src/PropertyMap.Api/appsettings.json`
- Create: `src/PropertyMap.Api/appsettings.Development.json`
- Modify: `PropertyMap.sln`

- [ ] **Step 1: Crear proyecto**

```bash
cd C:\Agentes\PropertyMap\src
dotnet new webapi -n PropertyMap.Api --framework net9.0 --no-openapi
cd ..
dotnet sln add src/PropertyMap.Api/PropertyMap.Api.csproj
```

- [ ] **Step 2: Agregar referencias y paquetes**

```bash
cd src/PropertyMap.Api
dotnet add reference ../PropertyMap.Core/PropertyMap.Core.csproj
dotnet add reference ../PropertyMap.Infrastructure/PropertyMap.Infrastructure.csproj
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer --version 9.*
dotnet add package Swashbuckle.AspNetCore --version 6.*
dotnet add package Microsoft.AspNetCore.Identity.EntityFrameworkCore --version 9.*
```

- [ ] **Step 3: Crear appsettings.json**

`src/PropertyMap.Api/appsettings.json`
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=PropertyMapDb;Trusted_Connection=True;MultipleActiveResultSets=true"
  },
  "JwtSettings": {
    "Secret": "propertymap-super-secret-key-change-in-production-32chars",
    "Issuer": "PropertyMap",
    "Audience": "PropertyMapUsers",
    "AccessTokenExpiryMinutes": 15,
    "RefreshTokenExpiryDays": 7
  },
  "SmtpSettings": {
    "Host": "mail.monsterasp.net",
    "Port": 587,
    "Username": "",
    "Password": "",
    "FromEmail": "noreply@propertymap.com.ar",
    "FromName": "PropertyMap"
  },
  "AppSettings": {
    "FrontendUrl": "https://localhost:7001",
    "BlazorUrl": "https://localhost:7001"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

`src/PropertyMap.Api/appsettings.Development.json`
```json
{
  "SmtpSettings": {
    "Host": "localhost",
    "Port": 1025,
    "Username": "test",
    "Password": "test",
    "FromEmail": "dev@propertymap.local",
    "FromName": "PropertyMap Dev"
  }
}
```

- [ ] **Step 4: Crear Program.cs**

`src/PropertyMap.Api/Program.cs`
```csharp
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using PropertyMap.Core.Entities;
using PropertyMap.Core.Interfaces;
using PropertyMap.Infrastructure.Data;
using PropertyMap.Infrastructure.Repositories;
using PropertyMap.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "PropertyMap API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization: Bearer {token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequiredLength = 8;
    options.Password.RequireUppercase = true;
    options.Password.RequireDigit = true;
    options.Password.RequireNonAlphanumeric = true;
    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedEmail = true;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

var jwtSettings = builder.Configuration.GetSection("JwtSettings");
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
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
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("BlazorClient", policy =>
        policy.WithOrigins(
                builder.Configuration["AppSettings:BlazorUrl"] ?? "https://localhost:7001")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials());
});

builder.Services.AddScoped<IListingRepository, ListingRepository>();
builder.Services.AddScoped<ILocationRepository, LocationRepository>();
builder.Services.AddScoped<IPublisherRepository, PublisherRepository>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IEmailService, EmailService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("BlazorClient");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Seed roles
using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    foreach (var role in new[] { "Admin", "Publisher", "User" })
    {
        if (!await roleManager.RoleExistsAsync(role))
            await roleManager.CreateAsync(new IdentityRole(role));
    }
}

app.Run();

public partial class Program { }
```

- [ ] **Step 5: Build**

```bash
cd C:\Agentes\PropertyMap
dotnet build src/PropertyMap.Api/PropertyMap.Api.csproj
```
Expected: `Build succeeded.`

- [ ] **Step 6: Commit**

```bash
git add src/PropertyMap.Api/ PropertyMap.sln
git commit -m "feat(api): create PropertyMap.Api project with JWT auth and Swagger"
```

---

## Task 12: TokenService

**Files:**
- Create: `src/PropertyMap.Infrastructure/Services/TokenService.cs`

- [ ] **Step 1: Crear TokenService**

`src/PropertyMap.Infrastructure/Services/TokenService.cs`
```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using PropertyMap.Core.Entities;
using PropertyMap.Core.Interfaces;

namespace PropertyMap.Infrastructure.Services;

public class TokenService : ITokenService
{
    private readonly IConfiguration _config;

    public TokenService(IConfiguration config)
    {
        _config = config;
    }

    public string GenerateAccessToken(ApplicationUser user, IList<string> roles)
    {
        var jwtSettings = _config.GetSection("JwtSettings");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["Secret"]!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Email, user.Email!),
            new(ClaimTypes.Name, $"{user.Nombre} {user.Apellido}"),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        foreach (var role in roles)
            claims.Add(new Claim(ClaimTypes.Role, role));

        var expiry = DateTime.UtcNow.AddMinutes(
            int.Parse(jwtSettings["AccessTokenExpiryMinutes"] ?? "15"));

        var token = new JwtSecurityToken(
            issuer: jwtSettings["Issuer"],
            audience: jwtSettings["Audience"],
            claims: claims,
            expires: expiry,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        var bytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }

    public ClaimsPrincipal? ValidateExpiredToken(string token)
    {
        var jwtSettings = _config.GetSection("JwtSettings");
        var handler = new JwtSecurityTokenHandler();
        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = false,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidAudience = jwtSettings["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSettings["Secret"]!))
        };

        try
        {
            return handler.ValidateToken(token, parameters, out _);
        }
        catch
        {
            return null;
        }
    }
}
```

- [ ] **Step 2: Build**

```bash
dotnet build src/PropertyMap.Infrastructure/PropertyMap.Infrastructure.csproj
```
Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```bash
git add src/PropertyMap.Infrastructure/Services/TokenService.cs
git commit -m "feat(infra): implement TokenService with JWT access token and refresh token"
```

---

## Task 13: EmailService

**Files:**
- Create: `src/PropertyMap.Infrastructure/Services/EmailService.cs`

- [ ] **Step 1: Crear EmailService**

`src/PropertyMap.Infrastructure/Services/EmailService.cs`
```csharp
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using MimeKit;
using PropertyMap.Core.Interfaces;

namespace PropertyMap.Infrastructure.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _config;

    public EmailService(IConfiguration config)
    {
        _config = config;
    }

    private async Task SendAsync(string toEmail, string toName, string subject, string htmlBody)
    {
        var smtp = _config.GetSection("SmtpSettings");
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(smtp["FromName"], smtp["FromEmail"]));
        message.To.Add(new MailboxAddress(toName, toEmail));
        message.Subject = subject;
        message.Body = new TextPart("html") { Text = htmlBody };

        using var client = new SmtpClient();
        await client.ConnectAsync(smtp["Host"], int.Parse(smtp["Port"] ?? "587"), SecureSocketOptions.StartTls);
        await client.AuthenticateAsync(smtp["Username"], smtp["Password"]);
        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }

    public async Task SendEmailVerificationAsync(string toEmail, string toName, string token)
    {
        var html = $"""
            <!DOCTYPE html>
            <html>
            <body style="font-family:system-ui,sans-serif;max-width:480px;margin:0 auto;padding:24px">
              <h2 style="color:#be123c">Verificá tu cuenta</h2>
              <p>Hola <strong>{toName}</strong>,</p>
              <p>Tu código de verificación es:</p>
              <div style="font-size:40px;font-weight:bold;letter-spacing:12px;color:#1a1a1a;padding:16px 0">{token}</div>
              <p style="color:#666">El código expira en 24 horas. Si no creaste esta cuenta, ignorá este email.</p>
            </body>
            </html>
            """;
        await SendAsync(toEmail, toName, "Verificá tu cuenta en PropertyMap", html);
    }

    public async Task SendPasswordResetAsync(string toEmail, string toName, string token, string resetUrl)
    {
        var link = $"{resetUrl}?token={Uri.EscapeDataString(token)}&email={Uri.EscapeDataString(toEmail)}";
        var html = $"""
            <!DOCTYPE html>
            <html>
            <body style="font-family:system-ui,sans-serif;max-width:480px;margin:0 auto;padding:24px">
              <h2 style="color:#be123c">Recuperar contraseña</h2>
              <p>Hola <strong>{toName}</strong>,</p>
              <p>Recibimos una solicitud para restablecer tu contraseña en PropertyMap.</p>
              <p>
                <a href="{link}" style="background:#be123c;color:white;padding:12px 24px;border-radius:8px;text-decoration:none;display:inline-block">
                  Restablecer contraseña
                </a>
              </p>
              <p style="color:#666">El link expira en 1 hora. Si no solicitaste esto, ignorá este email.</p>
            </body>
            </html>
            """;
        await SendAsync(toEmail, toName, "Restablecé tu contraseña en PropertyMap", html);
    }

    public async Task SendWelcomeAsync(string toEmail, string toName)
    {
        var html = $"""
            <!DOCTYPE html>
            <html>
            <body style="font-family:system-ui,sans-serif;max-width:480px;margin:0 auto;padding:24px">
              <h2 style="color:#be123c">¡Bienvenido a PropertyMap!</h2>
              <p>Hola <strong>{toName}</strong>,</p>
              <p>Tu cuenta está activa. Ya podés buscar propiedades, guardar favoritos y configurar alertas.</p>
              <p>
                <a href="https://propertymap.com.ar" style="background:#be123c;color:white;padding:12px 24px;border-radius:8px;text-decoration:none;display:inline-block">
                  Ir a PropertyMap
                </a>
              </p>
            </body>
            </html>
            """;
        await SendAsync(toEmail, toName, "¡Bienvenido a PropertyMap!", html);
    }
}
```

- [ ] **Step 2: Build**

```bash
dotnet build src/PropertyMap.Infrastructure/PropertyMap.Infrastructure.csproj
```
Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```bash
git add src/PropertyMap.Infrastructure/Services/EmailService.cs
git commit -m "feat(infra): implement EmailService with MailKit (verify, reset, welcome)"
```

---

## Task 14: ListingsController

**Files:**
- Create: `src/PropertyMap.Api/Controllers/ListingsController.cs`
- Modify: `src/PropertyMap.Infrastructure/Repositories/ListingRepository.cs`

- [ ] **Step 1: Actualizar ListingRepository para retornar ListingDetailDto**

Agregar método `GetByIdAsDetailAsync` a `IListingRepository` en Core:

```csharp
// Agregar a src/PropertyMap.Core/Interfaces/IListingRepository.cs
Task<ListingDetailDto?> GetByIdAsDetailAsync(int id);
```

Implementar en `src/PropertyMap.Infrastructure/Repositories/ListingRepository.cs`:

```csharp
public async Task<ListingDetailDto?> GetByIdAsDetailAsync(int id)
{
    var listing = await _context.PropertyListings
        .Include(l => l.Location)
        .Include(l => l.Publisher)
        .Include(l => l.Images.OrderBy(i => i.Orden))
        .FirstOrDefaultAsync(l => l.Id == id && l.Estado == EstadoPublicacion.Publicada);

    if (listing == null) return null;

    return new ListingDetailDto(
        Id: listing.Id,
        Titulo: listing.Titulo,
        Descripcion: listing.Descripcion,
        Precio: listing.Precio,
        Moneda: listing.Moneda,
        TipoPropiedad: listing.TipoPropiedad.ToString(),
        Operacion: listing.Operacion.ToString(),
        DireccionTexto: listing.Location.DireccionTexto,
        Ciudad: listing.Location.Ciudad,
        Provincia: listing.Location.Provincia,
        Lat: listing.Location.Latitud,
        Lng: listing.Location.Longitud,
        Superficie: listing.Superficie,
        SuperficieCubierta: listing.SuperficieCubierta,
        Ambientes: listing.Ambientes,
        Dormitorios: listing.Dormitorios,
        Banos: listing.Banos,
        Antiguedad: listing.Antiguedad,
        Cochera: listing.Cochera,
        Amenities: listing.Amenities,
        FotoUrls: listing.Images.Select(i => i.Url).ToList(),
        PublisherNombre: listing.Publisher.Nombre,
        PublisherTelefono: listing.Publisher.Telefono,
        PublisherLogoUrl: listing.Publisher.LogoUrl,
        FechaPublicacion: listing.FechaPublicacion
    );
}
```

También actualizar `GetActiveListingsAsync` y `GetActiveListingsForMapAsync` para filtrar por `EstadoPublicacion.Publicada` en lugar de `EstadoPropiedad.Activa`.

- [ ] **Step 2: Actualizar GetActiveListingsAsync en ListingRepository**

```csharp
public async Task<IEnumerable<PropertyListing>> GetActiveListingsAsync()
{
    return await _context.PropertyListings
        .Include(l => l.Location)
        .Include(l => l.Publisher)
        .Include(l => l.Images.OrderBy(i => i.Orden))
        .Where(l => l.Estado == EstadoPublicacion.Publicada)
        .ToListAsync();
}
```

- [ ] **Step 3: Actualizar GetActiveListingsForMapAsync**

```csharp
public async Task<IEnumerable<ListingMapDto>> GetActiveListingsForMapAsync()
{
    return await _context.PropertyListings
        .Include(l => l.Location)
        .Include(l => l.Images.Where(i => i.EsPrincipal))
        .Where(l => l.Estado == EstadoPublicacion.Publicada)
        .Select(l => new ListingMapDto(
            l.Id,
            l.Location.Latitud,
            l.Location.Longitud,
            l.Titulo,
            l.Precio,
            l.Moneda,
            l.TipoPropiedad.ToString(),
            l.Operacion.ToString(),
            l.Images.FirstOrDefault(i => i.EsPrincipal) != null
                ? l.Images.First(i => i.EsPrincipal).Url
                : null))
        .ToListAsync();
}
```

- [ ] **Step 4: Crear ListingsController**

`src/PropertyMap.Api/Controllers/ListingsController.cs`
```csharp
using Microsoft.AspNetCore.Mvc;
using PropertyMap.Core.Interfaces;

namespace PropertyMap.Api.Controllers;

[ApiController]
[Route("api/listings")]
public class ListingsController : ControllerBase
{
    private readonly IListingRepository _listings;

    public ListingsController(IListingRepository listings)
    {
        _listings = listings;
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

- [ ] **Step 5: Build**

```bash
dotnet build src/PropertyMap.Api/PropertyMap.Api.csproj
```
Expected: `Build succeeded.`

- [ ] **Step 6: Commit**

```bash
git add src/PropertyMap.Core/Interfaces/ src/PropertyMap.Infrastructure/Repositories/ src/PropertyMap.Api/Controllers/ListingsController.cs
git commit -m "feat(api): add ListingsController with GET all, GET by id, GET map endpoints"
```

---

## Task 15: AuthController — Register + VerifyEmail

**Files:**
- Create: `src/PropertyMap.Api/Controllers/AuthController.cs`

- [ ] **Step 1: Crear AuthController con Register y VerifyEmail**

`src/PropertyMap.Api/Controllers/AuthController.cs`
```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PropertyMap.Core.DTOs.Auth;
using PropertyMap.Core.Entities;
using PropertyMap.Core.Enums;
using PropertyMap.Core.Interfaces;

namespace PropertyMap.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITokenService _tokenService;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _config;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        ITokenService tokenService,
        IEmailService emailService,
        IConfiguration config)
    {
        _userManager = userManager;
        _tokenService = tokenService;
        _emailService = emailService;
        _config = config;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        if (request.Password != request.ConfirmPassword)
            return BadRequest(new { message = "Las contraseñas no coinciden." });

        if (await _userManager.FindByEmailAsync(request.Email) != null)
            return Conflict(new { message = "El email ya está registrado." });

        var token = GenerateVerificationCode();
        var user = new ApplicationUser
        {
            Nombre = request.Nombre,
            Apellido = request.Apellido,
            Email = request.Email,
            UserName = request.Email,
            Estado = EstadoUsuario.PendienteVerificacion,
            EmailVerificationToken = token,
            EmailVerificationExpiry = DateTime.UtcNow.AddHours(24),
            FechaRegistro = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
            return BadRequest(new { errors = result.Errors.Select(e => e.Description) });

        await _userManager.AddToRoleAsync(user, "User");
        await _emailService.SendEmailVerificationAsync(request.Email, request.Nombre, token);

        return Ok(new { message = "Registro exitoso. Revisá tu email para verificar tu cuenta." });
    }

    [HttpPost("verify-email")]
    public async Task<IActionResult> VerifyEmail(VerifyEmailRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null)
            return NotFound(new { message = "Usuario no encontrado." });

        if (user.EmailVerificationToken != request.Token ||
            user.EmailVerificationExpiry < DateTime.UtcNow)
            return BadRequest(new { message = "Token inválido o expirado." });

        user.EmailConfirmed = true;
        user.Estado = EstadoUsuario.Activo;
        user.EmailVerificationToken = null;
        user.EmailVerificationExpiry = null;
        await _userManager.UpdateAsync(user);
        await _emailService.SendWelcomeAsync(user.Email!, user.Nombre);

        return Ok(new { message = "Email verificado correctamente." });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null || !await _userManager.CheckPasswordAsync(user, request.Password))
            return Unauthorized(new { message = "Credenciales incorrectas." });

        if (!user.EmailConfirmed)
            return Unauthorized(new { message = "Debés verificar tu email antes de iniciar sesión." });

        if (user.Estado == EstadoUsuario.Suspendido)
            return Unauthorized(new { message = "Tu cuenta está suspendida." });

        var roles = await _userManager.GetRolesAsync(user);
        var jwtSettings = _config.GetSection("JwtSettings");

        user.RefreshToken = _tokenService.GenerateRefreshToken();
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(
            int.Parse(jwtSettings["RefreshTokenExpiryDays"] ?? "7"));
        await _userManager.UpdateAsync(user);

        return Ok(new AuthResponse(
            AccessToken: _tokenService.GenerateAccessToken(user, roles),
            RefreshToken: user.RefreshToken,
            AccessTokenExpiry: DateTime.UtcNow.AddMinutes(
                int.Parse(jwtSettings["AccessTokenExpiryMinutes"] ?? "15")),
            UserId: user.Id,
            Email: user.Email!,
            NombreCompleto: $"{user.Nombre} {user.Apellido}",
            Roles: roles
        ));
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(RefreshTokenRequest request)
    {
        var principal = _tokenService.ValidateExpiredToken(request.AccessToken);
        if (principal == null)
            return Unauthorized(new { message = "Token inválido." });

        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = await _userManager.FindByIdAsync(userId!);
        if (user == null ||
            user.RefreshToken != request.RefreshToken ||
            user.RefreshTokenExpiry < DateTime.UtcNow)
            return Unauthorized(new { message = "Refresh token inválido o expirado." });

        var roles = await _userManager.GetRolesAsync(user);
        var jwtSettings = _config.GetSection("JwtSettings");

        user.RefreshToken = _tokenService.GenerateRefreshToken();
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(
            int.Parse(jwtSettings["RefreshTokenExpiryDays"] ?? "7"));
        await _userManager.UpdateAsync(user);

        return Ok(new AuthResponse(
            AccessToken: _tokenService.GenerateAccessToken(user, roles),
            RefreshToken: user.RefreshToken,
            AccessTokenExpiry: DateTime.UtcNow.AddMinutes(
                int.Parse(jwtSettings["AccessTokenExpiryMinutes"] ?? "15")),
            UserId: user.Id,
            Email: user.Email!,
            NombreCompleto: $"{user.Nombre} {user.Apellido}",
            Roles: roles
        ));
    }

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request)
    {
        const string safeMessage = "Si el email existe, recibirás instrucciones en breve.";
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null || !user.EmailConfirmed)
            return Ok(new { message = safeMessage });

        user.PasswordResetToken = _tokenService.GenerateRefreshToken();
        user.PasswordResetExpiry = DateTime.UtcNow.AddHours(1);
        await _userManager.UpdateAsync(user);

        var resetUrl = _config["AppSettings:FrontendUrl"] ?? "https://propertymap.com.ar";
        await _emailService.SendPasswordResetAsync(
            user.Email!, user.Nombre, user.PasswordResetToken, $"{resetUrl}/reset-password");

        return Ok(new { message = safeMessage });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request)
    {
        if (request.NewPassword != request.ConfirmNewPassword)
            return BadRequest(new { message = "Las contraseñas no coinciden." });

        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null ||
            user.PasswordResetToken != request.Token ||
            user.PasswordResetExpiry < DateTime.UtcNow)
            return BadRequest(new { message = "Token inválido o expirado." });

        var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, resetToken, request.NewPassword);
        if (!result.Succeeded)
            return BadRequest(new { errors = result.Errors.Select(e => e.Description) });

        user.PasswordResetToken = null;
        user.PasswordResetExpiry = null;
        await _userManager.UpdateAsync(user);

        return Ok(new { message = "Contraseña restablecida correctamente." });
    }

    private static string GenerateVerificationCode() =>
        Random.Shared.Next(100000, 999999).ToString();
}
```

- [ ] **Step 2: Build**

```bash
dotnet build src/PropertyMap.Api/PropertyMap.Api.csproj
```
Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```bash
git add src/PropertyMap.Api/Controllers/AuthController.cs
git commit -m "feat(api): add AuthController with register, verify-email, login, refresh, forgot/reset-password"
```

---

## Task 16: Tests de integración para AuthController

**Files:**
- Modify: `src/PropertyMap.Tests/PropertyMap.Tests.csproj`
- Create: `src/PropertyMap.Tests/Api/TestWebApplicationFactory.cs`
- Create: `src/PropertyMap.Tests/Api/AuthControllerTests.cs`

- [ ] **Step 1: Agregar paquetes al proyecto de tests**

```bash
cd C:\Agentes\PropertyMap\src\PropertyMap.Tests
dotnet add package Microsoft.AspNetCore.Mvc.Testing --version 9.*
dotnet add package Microsoft.EntityFrameworkCore.InMemory --version 9.*
dotnet add reference ../PropertyMap.Api/PropertyMap.Api.csproj
```

- [ ] **Step 2: Crear TestWebApplicationFactory**

`src/PropertyMap.Tests/Api/TestWebApplicationFactory.cs`
```csharp
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PropertyMap.Core.Interfaces;
using PropertyMap.Infrastructure.Data;

namespace PropertyMap.Tests.Api;

public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Replace SQL Server DbContext with InMemory
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
            if (descriptor != null) services.Remove(descriptor);

            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}"));

            // Replace email service with no-op mock
            var emailDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IEmailService));
            if (emailDescriptor != null) services.Remove(emailDescriptor);
            services.AddScoped<IEmailService, NoOpEmailService>();
        });

        builder.UseEnvironment("Testing");
    }
}

public class NoOpEmailService : IEmailService
{
    public Task SendEmailVerificationAsync(string toEmail, string toName, string token) => Task.CompletedTask;
    public Task SendPasswordResetAsync(string toEmail, string toName, string token, string resetUrl) => Task.CompletedTask;
    public Task SendWelcomeAsync(string toEmail, string toName) => Task.CompletedTask;
}
```

- [ ] **Step 3: Escribir tests**

`src/PropertyMap.Tests/Api/AuthControllerTests.cs`
```csharp
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using PropertyMap.Core.DTOs.Auth;
using PropertyMap.Core.Entities;
using PropertyMap.Core.Enums;
using Xunit;

namespace PropertyMap.Tests.Api;

public class AuthControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly TestWebApplicationFactory _factory;

    public AuthControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Register_ValidData_Returns200WithMessage()
    {
        var request = new RegisterRequest("Juan", "Pérez", $"test_{Guid.NewGuid()}@example.com", "Test123!", "Test123!");
        var response = await _client.PostAsJsonAsync("/api/auth/register", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<MessageResponse>();
        Assert.Contains("Revisá tu email", body!.Message);
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns409()
    {
        var email = $"dup_{Guid.NewGuid()}@example.com";
        var request = new RegisterRequest("A", "B", email, "Test123!", "Test123!");
        await _client.PostAsJsonAsync("/api/auth/register", request);
        var response = await _client.PostAsJsonAsync("/api/auth/register", request);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Register_PasswordMismatch_Returns400()
    {
        var request = new RegisterRequest("A", "B", $"mismatch_{Guid.NewGuid()}@example.com", "Test123!", "Different123!");
        var response = await _client.PostAsJsonAsync("/api/auth/register", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_UnverifiedEmail_Returns401()
    {
        var email = $"unverified_{Guid.NewGuid()}@example.com";
        await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest("A", "B", email, "Test123!", "Test123!"));
        var response = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "Test123!"));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_VerifiedUser_ReturnsTokens()
    {
        var email = $"verified_{Guid.NewGuid()}@example.com";
        await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest("Ana", "García", email, "Test123!", "Test123!"));

        // Verify email manually via UserManager
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        user!.EmailConfirmed = true;
        user.Estado = EstadoUsuario.Activo;
        user.EmailVerificationToken = null;
        await userManager.UpdateAsync(user);

        var response = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "Test123!"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotEmpty(auth!.AccessToken);
        Assert.NotEmpty(auth.RefreshToken);
        Assert.Contains("User", auth.Roles);
    }

    [Fact]
    public async Task VerifyEmail_InvalidToken_Returns400()
    {
        var email = $"verify_{Guid.NewGuid()}@example.com";
        await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest("A", "B", email, "Test123!", "Test123!"));
        var response = await _client.PostAsJsonAsync("/api/auth/verify-email", new VerifyEmailRequest(email, "000000"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ForgotPassword_UnknownEmail_Returns200WithSafeMessage()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/forgot-password",
            new ForgotPasswordRequest("nobody@example.com"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<MessageResponse>();
        Assert.Contains("Si el email existe", body!.Message);
    }

    [Fact]
    public async Task GetListings_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/listings");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

public record MessageResponse(string Message);
```

- [ ] **Step 4: Correr los tests**

```bash
cd C:\Agentes\PropertyMap
dotnet test src/PropertyMap.Tests/PropertyMap.Tests.csproj --logger "console;verbosity=normal"
```

Expected: todos los tests anteriores + los nuevos de Auth pasan. Si alguno falla, revisar el mensaje de error y corregir.

- [ ] **Step 5: Commit**

```bash
git add src/PropertyMap.Tests/
git commit -m "test(api): add AuthController integration tests with TestWebApplicationFactory"
```

---

## Task 17: Refactor PropertyMap.Web → IListingApiService

**Files:**
- Create: `src/PropertyMap.Web/Services/IListingApiService.cs`
- Create: `src/PropertyMap.Web/Services/ListingApiService.cs`
- Modify: `src/PropertyMap.Web/Program.cs`
- Modify: `src/PropertyMap.Web/Components/Pages/Home.razor`
- Modify: `src/PropertyMap.Web/Components/Pages/PropertyDetail.razor`
- Modify: `src/PropertyMap.Web/Components/Listings/PropertyCard.razor`

- [ ] **Step 1: Crear IListingApiService**

`src/PropertyMap.Web/Services/IListingApiService.cs`
```csharp
using PropertyMap.Core.DTOs;
using PropertyMap.Core.Entities;

namespace PropertyMap.Web.Services;

public interface IListingApiService
{
    Task<IEnumerable<PropertyListing>> GetActiveListingsAsync();
    Task<IEnumerable<ListingMapDto>> GetActiveListingsForMapAsync();
    Task<ListingDetailDto?> GetByIdAsync(int id);
}
```

- [ ] **Step 2: Crear ListingApiService**

`src/PropertyMap.Web/Services/ListingApiService.cs`
```csharp
using System.Net.Http.Json;
using PropertyMap.Core.DTOs;
using PropertyMap.Core.Entities;

namespace PropertyMap.Web.Services;

public class ListingApiService : IListingApiService
{
    private readonly HttpClient _http;

    public ListingApiService(HttpClient http)
    {
        _http = http;
    }

    public async Task<IEnumerable<PropertyListing>> GetActiveListingsAsync()
    {
        return await _http.GetFromJsonAsync<IEnumerable<PropertyListing>>("/api/listings")
               ?? Enumerable.Empty<PropertyListing>();
    }

    public async Task<IEnumerable<ListingMapDto>> GetActiveListingsForMapAsync()
    {
        return await _http.GetFromJsonAsync<IEnumerable<ListingMapDto>>("/api/listings/map")
               ?? Enumerable.Empty<ListingMapDto>();
    }

    public async Task<ListingDetailDto?> GetByIdAsync(int id)
    {
        try
        {
            return await _http.GetFromJsonAsync<ListingDetailDto>($"/api/listings/{id}");
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }
}
```

- [ ] **Step 3: Registrar en Program.cs de PropertyMap.Web**

En `src/PropertyMap.Web/Program.cs`, agregar antes de `builder.Build()`:

```csharp
// Registrar HttpClient para API
builder.Services.AddHttpClient<IListingApiService, ListingApiService>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ApiSettings:BaseUrl"]
                                 ?? "https://localhost:7002/");
});
```

Y agregar en `appsettings.json` de `PropertyMap.Web`:
```json
"ApiSettings": {
  "BaseUrl": "https://localhost:7002/"
}
```

- [ ] **Step 4: Actualizar Home.razor**

En `src/PropertyMap.Web/Components/Pages/Home.razor`, reemplazar la inyección de repositorios:

Cambiar:
```razor
@inject IListingRepository ListingRepo
```
Por:
```razor
@inject IListingApiService ListingApi
```

Y en `OnInitializedAsync`, cambiar:
```csharp
allListings = (await ListingRepo.GetActiveListingsAsync()).ToList();
mapListings = (await ListingRepo.GetActiveListingsForMapAsync()).ToList();
```
Por:
```csharp
allListings = (await ListingApi.GetActiveListingsAsync()).ToList();
mapListings = (await ListingApi.GetActiveListingsForMapAsync()).ToList();
```

- [ ] **Step 5: Actualizar PropertyDetail.razor**

Cambiar:
```razor
@inject IListingRepository ListingRepo
```
Por:
```razor
@inject IListingApiService ListingApi
```

Y el método de carga de propiedad, reemplazando el repo call por:
```csharp
var detail = await ListingApi.GetByIdAsync(Id);
```

Para adaptar `PropertyDetail.razor` al `ListingDetailDto` (que tiene `FotoUrls` en lugar de `Images`), actualizar la galería de fotos para usar `detail.FotoUrls` en lugar de `listing.Images.Select(...)`.

- [ ] **Step 6: Actualizar PropertyCard.razor**

`PropertyCard` actualmente usa `Listing.Fotos`. Cambiar a `Listing.Images`. La prop del card sigue siendo `PropertyListing` (que ahora tiene nav `Images`). Cambiar todas las referencias de `Listing.Fotos[index]` a:

```csharp
private string GetFotoUrl(int index)
{
    var images = Listing.Images.OrderBy(i => i.Orden).ToList();
    return index < images.Count ? images[index].Url : "/images/placeholder.jpg";
}

private int FotoCount => Listing.Images.Count;
```

- [ ] **Step 7: Agregar _Imports.razor la referencia al namespace**

En `src/PropertyMap.Web/Components/_Imports.razor` agregar:
```razor
@using PropertyMap.Web.Services
```

- [ ] **Step 8: Build completo**

```bash
cd C:\Agentes\PropertyMap
dotnet build PropertyMap.sln
```
Expected: `Build succeeded. 0 Error(s).`

- [ ] **Step 9: Levantar API y Web y verificar que funciona**

Terminal 1:
```bash
dotnet run --project src/PropertyMap.Api
```
Esperar: `Now listening on: https://localhost:7002`

Terminal 2:
```bash
dotnet run --project src/PropertyMap.Web
```
Esperar: `Now listening on: https://localhost:7001`

Abrir `https://localhost:7001` — el mapa debe cargar propiedades.
Abrir `https://localhost:7002/swagger` — debe mostrar los endpoints `/api/auth/*` y `/api/listings`.

- [ ] **Step 10: Commit final**

```bash
git add src/PropertyMap.Web/ src/PropertyMap.Api/
git commit -m "feat(web): refactor Blazor to consume listings API via HttpClient, replace direct repo injection"
```

---

## Self-Review

### Spec coverage
- ✅ Domain model completo (15 entidades nuevas + 6 enums nuevos + 2 expandidos)
- ✅ `PropertyMap.Api` project con JWT, CORS, Swagger
- ✅ Auth endpoints: register, verify-email, login, refresh, forgot-password, reset-password
- ✅ Email service con MailKit (verify, reset, welcome)
- ✅ TokenService (access token JWT 15 min + refresh token 7 días rotante)
- ✅ Listings endpoints (GET /listings, GET /listings/{id}, GET /listings/map)
- ✅ Blazor refactorizado a HttpClient
- ✅ Migration con data migration para EstadoPublicacion
- ✅ DbSeeder actualizado con ApplicationUser real y PropertyImage rows
- ✅ Tests de integración para AuthController

### Dependencias de tasks
```
Task 1 → Task 2 → Task 3 → Task 4 → Task 5 → Task 6 → Task 7 → Task 8 (secuencial)
Task 9 → Task 10 (solo depende de Task 1)
Task 11 → Task 12 → Task 13 → Task 14 → Task 15 → Task 16 (secuencial)
Task 17 depende de Task 14 y Task 15
```

Tasks 1-8 son Core; Tasks 9-13 son Infrastructure; Tasks 14-17 son API + Web.

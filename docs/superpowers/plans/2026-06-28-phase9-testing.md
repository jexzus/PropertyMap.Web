# Phase 9.2 — Testing Exhaustivo Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Cerrar los gaps de cobertura de integration tests para `AdminController`, `ListingsController` y `NotificationsController`, los 3 controllers sin tests dedicados.

**Architecture:** 3 archivos nuevos de test xUnit, mismo patrón ya usado en todo `PropertyMap.Tests/Api/`: `IClassFixture<TestWebApplicationFactory>` + `TestAuthHelper` para autenticación de Publisher/User, creación manual de un usuario Admin vía `UserManager` (no hay helper compartido para Admin — se replica el patrón ya usado en `ReportsControllerTests`/`ViewTrackingTests`), y acceso directo a `AppDbContext` vía `_factory.Services.CreateScope()` para asserts fuertes y para insertar datos que no tienen endpoint público de creación (notificaciones).

**Tech Stack:** xUnit, EF Core InMemory, `Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory` (ya configurado en `TestWebApplicationFactory.cs`). Sin librerías nuevas.

**Spec de referencia:** `docs/superpowers/specs/2026-06-28-phase9-testing-design.md`

---

### Task 1: AdminControllerTests.cs

**Files:**
- Create: `PropertyMap.Tests/Api/AdminControllerTests.cs`

- [ ] **Step 1: Crear el archivo con el helper de admin y las primeras 2 pruebas (pending + aprobar)**

```csharp
using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using PropertyMap.Core.DTOs.Admin;
using PropertyMap.Core.DTOs.Properties;
using PropertyMap.Core.Enums;
using PropertyMap.Infrastructure.Data;
using Xunit;

namespace PropertyMap.Tests.Api;

public class AdminControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public AdminControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private record CreatedIdDto(int Id);

    private static CreateListingRequest BuildListingRequest(string titulo) => new(
        Operacion: TipoOperacion.Venta, TipoPropiedad: TipoPropiedad.Casa,
        Titulo: titulo, Descripcion: "Test",
        Precio: 80000, Moneda: "USD",
        DireccionTexto: "Av. Admin 100", Ciudad: "Neuquén", Provincia: "Neuquén",
        Lat: -38.95, Lng: -68.06,
        Superficie: null, SuperficieCubierta: null, Ambientes: null,
        Dormitorios: null, Banos: null, Antiguedad: null,
        Cochera: false, Amenities: []);

    private async Task<HttpClient> CreateAdminClientAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider
            .GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<PropertyMap.Core.Entities.ApplicationUser>>();
        var adminEmail = $"admin_{Guid.NewGuid()}@test.com";
        var adminUser = new PropertyMap.Core.Entities.ApplicationUser
        {
            UserName = adminEmail, Email = adminEmail,
            Nombre = "Admin", Apellido = "Test", EmailConfirmed = true,
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
        return adminClient;
    }

    private async Task<(HttpClient pubClient, int listingId)> CreatePendingListingAsync(string titulo = "Casa pendiente")
    {
        var (pubClient, _) = await TestAuthHelper.CreateAuthenticatedPublisherAsync(_factory);
        await TestAuthHelper.CreatePublisherProfileAsync(pubClient);
        var createResp = await pubClient.PostAsJsonAsync("/api/properties", BuildListingRequest(titulo));
        var created = await createResp.Content.ReadFromJsonAsync<CreatedIdDto>();
        return (pubClient, created!.Id);
    }

    [Fact]
    public async Task GetPending_ReturnsOnlyPendingListings()
    {
        var adminClient = await CreateAdminClientAsync();
        var (_, pendingId) = await CreatePendingListingAsync("Casa pendiente A");
        var (_, otherPendingId) = await CreatePendingListingAsync("Casa pendiente B");

        // Publicar otherPendingId para que NO aparezca como pendiente
        await adminClient.PatchAsJsonAsync($"/api/admin/listings/{otherPendingId}/review",
            new ReviewListingRequest(true, null));

        var pending = await adminClient.GetFromJsonAsync<List<PropertyMap.Core.Entities.PropertyListing>>(
            "/api/admin/listings/pending");

        Assert.Contains(pending!, l => l.Id == pendingId);
        Assert.DoesNotContain(pending!, l => l.Id == otherPendingId);
    }

    // Nota: el trigger de NotifyMatchingAlertsAsync al aprobar ya está cubierto en detalle
    // por AlertMatchingTests.cs (ApprovingListing_NotifiesMatchingAlert /
    // ApprovingListing_DoesNotNotify_WhenPriceExceedsMax). Esta prueba se limita al
    // comportamiento propio de AdminController: que Review cambia el Estado a Publicada.
    [Fact]
    public async Task Review_Aprobar_PublishesListing()
    {
        var adminClient = await CreateAdminClientAsync();
        var (_, listingId) = await CreatePendingListingAsync();

        var reviewResp = await adminClient.PatchAsJsonAsync($"/api/admin/listings/{listingId}/review",
            new ReviewListingRequest(true, null));
        Assert.Equal(HttpStatusCode.OK, reviewResp.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var listing = db.PropertyListings.First(l => l.Id == listingId);
        Assert.Equal(EstadoPublicacion.Publicada, listing.Estado);
    }
}
```

- [ ] **Step 2: Correr la suite para verificar que las 2 pruebas pasan**

Run: `cd C:\Agentes\PropertyMap && dotnet test src/PropertyMap.Tests/PropertyMap.Tests.csproj --filter "FullyQualifiedName~AdminControllerTests"`
Expected: `Correctas! - Con error: 0, Superado: 2`

- [ ] **Step 3: Agregar las 5 pruebas restantes (rechazar, 400, 404, GetAll, rol requerido)**

Agregar estos métodos dentro de la misma clase `AdminControllerTests`, después de `Review_Aprobar_PublishesListing`:

```csharp
    [Fact]
    public async Task Review_Rechazar_SetsBorradorWithMotivo()
    {
        var adminClient = await CreateAdminClientAsync();
        var (_, listingId) = await CreatePendingListingAsync();

        var reviewResp = await adminClient.PatchAsJsonAsync($"/api/admin/listings/{listingId}/review",
            new ReviewListingRequest(false, "Fotos de mala calidad"));
        Assert.Equal(HttpStatusCode.OK, reviewResp.StatusCode);

        var body = await reviewResp.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.Contains("Fotos de mala calidad", body!["message"]);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var listing = db.PropertyListings.First(l => l.Id == listingId);
        Assert.Equal(EstadoPublicacion.Borrador, listing.Estado);
    }

    [Fact]
    public async Task Review_NotPending_ReturnsBadRequest()
    {
        var adminClient = await CreateAdminClientAsync();
        var (_, listingId) = await CreatePendingListingAsync();
        await adminClient.PatchAsJsonAsync($"/api/admin/listings/{listingId}/review",
            new ReviewListingRequest(true, null));

        // Ya está Publicada, reintentar revisión debe fallar
        var secondReview = await adminClient.PatchAsJsonAsync($"/api/admin/listings/{listingId}/review",
            new ReviewListingRequest(true, null));

        Assert.Equal(HttpStatusCode.BadRequest, secondReview.StatusCode);
    }

    [Fact]
    public async Task Review_NotFound_Returns404()
    {
        var adminClient = await CreateAdminClientAsync();

        var reviewResp = await adminClient.PatchAsJsonAsync("/api/admin/listings/999999/review",
            new ReviewListingRequest(true, null));

        Assert.Equal(HttpStatusCode.NotFound, reviewResp.StatusCode);
    }

    [Fact]
    public async Task GetAll_ReturnsActiveListings()
    {
        var adminClient = await CreateAdminClientAsync();
        var (_, listingId) = await CreatePendingListingAsync("Casa para GetAll");
        await adminClient.PatchAsJsonAsync($"/api/admin/listings/{listingId}/review",
            new ReviewListingRequest(true, null));

        var allResp = await adminClient.GetAsync("/api/admin/listings");
        Assert.Equal(HttpStatusCode.OK, allResp.StatusCode);

        var listings = await allResp.Content.ReadFromJsonAsync<List<PropertyMap.Core.Entities.PropertyListing>>();
        Assert.Contains(listings!, l => l.Id == listingId);
    }

    [Fact]
    public async Task Endpoints_RequireAdminRole_RejectsOtherRoles()
    {
        var (pubClient, _) = await TestAuthHelper.CreateAuthenticatedPublisherAsync(_factory);

        var pendingResp = await pubClient.GetAsync("/api/admin/listings/pending");
        Assert.Equal(HttpStatusCode.Forbidden, pendingResp.StatusCode);

        var reviewResp = await pubClient.PatchAsJsonAsync("/api/admin/listings/1/review",
            new ReviewListingRequest(true, null));
        Assert.Equal(HttpStatusCode.Forbidden, reviewResp.StatusCode);
    }
```

- [ ] **Step 4: Correr la suite completa de AdminControllerTests**

Run: `cd C:\Agentes\PropertyMap && dotnet test src/PropertyMap.Tests/PropertyMap.Tests.csproj --filter "FullyQualifiedName~AdminControllerTests"`
Expected: `Correctas! - Con error: 0, Superado: 7`

- [ ] **Step 5: Commit**

```bash
cd C:\Agentes\PropertyMap\src
git add PropertyMap.Tests/Api/AdminControllerTests.cs
git commit -m "test: add integration tests for AdminController"
```

---

### Task 2: ListingsControllerTests.cs

**Files:**
- Create: `PropertyMap.Tests/Api/ListingsControllerTests.cs`

- [ ] **Step 1: Crear el archivo con las 5 pruebas**

```csharp
using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using PropertyMap.Core.DTOs;
using PropertyMap.Core.DTOs.Admin;
using PropertyMap.Core.DTOs.Properties;
using PropertyMap.Core.Entities;
using PropertyMap.Core.Enums;
using Xunit;

namespace PropertyMap.Tests.Api;

public class ListingsControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public ListingsControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private record CreatedIdDto(int Id);

    private static CreateListingRequest BuildListingRequest(string titulo) => new(
        Operacion: TipoOperacion.Venta, TipoPropiedad: TipoPropiedad.Departamento,
        Titulo: titulo, Descripcion: "Test",
        Precio: 60000, Moneda: "USD",
        DireccionTexto: "Calle Listings 50", Ciudad: "Mendoza", Provincia: "Mendoza",
        Lat: -32.89, Lng: -68.84,
        Superficie: null, SuperficieCubierta: null, Ambientes: null,
        Dormitorios: 2, Banos: 1, Antiguedad: null,
        Cochera: false, Amenities: []);

    private async Task<HttpClient> CreateAdminClientAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider
            .GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<ApplicationUser>>();
        var adminEmail = $"admin_listings_{Guid.NewGuid()}@test.com";
        var adminUser = new ApplicationUser
        {
            UserName = adminEmail, Email = adminEmail,
            Nombre = "Admin", Apellido = "Listings", EmailConfirmed = true,
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
        return adminClient;
    }

    private async Task<int> CreateAndPublishListingAsync(string titulo)
    {
        var (pubClient, _) = await TestAuthHelper.CreateAuthenticatedPublisherAsync(_factory);
        await TestAuthHelper.CreatePublisherProfileAsync(pubClient);
        var createResp = await pubClient.PostAsJsonAsync("/api/properties", BuildListingRequest(titulo));
        var created = await createResp.Content.ReadFromJsonAsync<CreatedIdDto>();

        var adminClient = await CreateAdminClientAsync();
        await adminClient.PatchAsJsonAsync($"/api/admin/listings/{created!.Id}/review",
            new ReviewListingRequest(true, null));

        return created.Id;
    }

    [Fact]
    public async Task GetAll_ReturnsOnlyPublishedListings()
    {
        var publishedId = await CreateAndPublishListingAsync("Depto publicado");

        var (pubClient, _) = await TestAuthHelper.CreateAuthenticatedPublisherAsync(_factory);
        await TestAuthHelper.CreatePublisherProfileAsync(pubClient);
        var pendingResp = await pubClient.PostAsJsonAsync("/api/properties", BuildListingRequest("Depto pendiente"));
        var pending = await pendingResp.Content.ReadFromJsonAsync<CreatedIdDto>();

        var anonClient = _factory.CreateClient();
        var allResp = await anonClient.GetAsync("/api/listings");
        Assert.Equal(HttpStatusCode.OK, allResp.StatusCode);

        var listings = await allResp.Content.ReadFromJsonAsync<List<PropertyListing>>();
        Assert.Contains(listings!, l => l.Id == publishedId);
        Assert.DoesNotContain(listings!, l => l.Id == pending!.Id);
    }

    [Fact]
    public async Task GetAll_NoCrashOnPublisherNavigation()
    {
        await CreateAndPublishListingAsync("Depto con publisher");

        var anonClient = _factory.CreateClient();
        var resp = await anonClient.GetAsync("/api/listings");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var listings = await resp.Content.ReadFromJsonAsync<List<PropertyListing>>();
        Assert.NotEmpty(listings!);
        Assert.Contains(listings!, l => l.Publisher != null);
    }

    [Fact]
    public async Task GetById_ReturnsDetailAndTracksView()
    {
        var listingId = await CreateAndPublishListingAsync("Depto detalle");

        var anonClient = _factory.CreateClient();
        var resp = await anonClient.GetAsync($"/api/listings/{listingId}");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var detail = await resp.Content.ReadFromJsonAsync<PropertyMap.Core.DTOs.ListingDetailDto>();
        Assert.Equal("Depto detalle", detail!.Titulo);
        Assert.Equal(TipoOperacion.Venta.ToString(), detail.Operacion);
    }

    [Fact]
    public async Task GetById_NotFound_Returns404()
    {
        var anonClient = _factory.CreateClient();
        var resp = await anonClient.GetAsync("/api/listings/999999");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task GetForMap_ReturnsMapDtos()
    {
        var listingId = await CreateAndPublishListingAsync("Depto mapa");

        var anonClient = _factory.CreateClient();
        var resp = await anonClient.GetAsync("/api/listings/map");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var mapListings = await resp.Content.ReadFromJsonAsync<List<ListingMapDto>>();
        var found = mapListings!.First(l => l.Id == listingId);
        Assert.Equal(TipoOperacion.Venta.ToString(), found.Operacion);
        Assert.NotEqual(0, found.Lat);
        Assert.NotEqual(0, found.Lng);
    }
}
```

- [ ] **Step 2: Correr la suite para verificar que las 5 pruebas pasan**

Run: `cd C:\Agentes\PropertyMap && dotnet test src/PropertyMap.Tests/PropertyMap.Tests.csproj --filter "FullyQualifiedName~ListingsControllerTests"`
Expected: `Correctas! - Con error: 0, Superado: 5`

- [ ] **Step 3: Commit**

```bash
cd C:\Agentes\PropertyMap\src
git add PropertyMap.Tests/Api/ListingsControllerTests.cs
git commit -m "test: add integration tests for ListingsController"
```

---

### Task 3: NotificationsControllerTests.cs

**Files:**
- Create: `PropertyMap.Tests/Api/NotificationsControllerTests.cs`

- [ ] **Step 1: Crear el archivo con el helper de inserción directa y las 6 pruebas**

```csharp
using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using PropertyMap.Core.DTOs.Notifications;
using PropertyMap.Core.Entities;
using PropertyMap.Core.Enums;
using PropertyMap.Infrastructure.Data;
using Xunit;

namespace PropertyMap.Tests.Api;

public class NotificationsControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public NotificationsControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task SeedNotificationAsync(string userId, bool leida = false, string titulo = "Notif")
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Notifications.Add(new Notification
        {
            UserId = userId,
            Tipo = TipoNotificacion.AlertaCoincidencia,
            Titulo = titulo,
            Mensaje = "Mensaje de prueba",
            Leida = leida,
            FechaCreacion = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task GetMine_ReturnsOnlyOwnNotifications()
    {
        var (userAClient, userAId) = await TestAuthHelper.CreateAuthenticatedUserAsync(_factory);
        var (_, userBId) = await TestAuthHelper.CreateAuthenticatedUserAsync(_factory);

        await SeedNotificationAsync(userAId, titulo: "Para A");
        await SeedNotificationAsync(userBId, titulo: "Para B");

        var mine = await userAClient.GetFromJsonAsync<List<NotificationDto>>("/api/notifications");

        Assert.Contains(mine!, n => n.Titulo == "Para A");
        Assert.DoesNotContain(mine!, n => n.Titulo == "Para B");
    }

    [Fact]
    public async Task GetMine_RespectsTakeParameter()
    {
        var (userClient, userId) = await TestAuthHelper.CreateAuthenticatedUserAsync(_factory);
        for (var i = 0; i < 5; i++)
            await SeedNotificationAsync(userId, titulo: $"Notif {i}");

        var limited = await userClient.GetFromJsonAsync<List<NotificationDto>>("/api/notifications?take=3");

        Assert.Equal(3, limited!.Count);
    }

    [Fact]
    public async Task GetUnreadCount_CountsOnlyUnread()
    {
        var (userClient, userId) = await TestAuthHelper.CreateAuthenticatedUserAsync(_factory);
        await SeedNotificationAsync(userId, leida: false);
        await SeedNotificationAsync(userId, leida: false);
        await SeedNotificationAsync(userId, leida: true);

        var countResp = await userClient.GetAsync("/api/notifications/unread-count");
        var count = await countResp.Content.ReadFromJsonAsync<int>();

        Assert.Equal(2, count);
    }

    [Fact]
    public async Task MarkAsRead_SetsLeidaTrue_DoesNotAffectOthers()
    {
        var (userClient, userId) = await TestAuthHelper.CreateAuthenticatedUserAsync(_factory);
        await SeedNotificationAsync(userId, titulo: "A marcar");
        await SeedNotificationAsync(userId, titulo: "Sin marcar");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var toMark = db.Notifications.First(n => n.UserId == userId && n.Titulo == "A marcar");
        var other = db.Notifications.First(n => n.UserId == userId && n.Titulo == "Sin marcar");

        var resp = await userClient.PatchAsync($"/api/notifications/{toMark.Id}/read", null);
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(verifyDb.Notifications.First(n => n.Id == toMark.Id).Leida);
        Assert.False(verifyDb.Notifications.First(n => n.Id == other.Id).Leida);
    }

    [Fact]
    public async Task MarkAsRead_OtherUsersNotification_DoesNotAffectIt()
    {
        var (userAClient, userAId) = await TestAuthHelper.CreateAuthenticatedUserAsync(_factory);
        var (_, userBId) = await TestAuthHelper.CreateAuthenticatedUserAsync(_factory);
        await SeedNotificationAsync(userBId, titulo: "De B");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var notifB = db.Notifications.First(n => n.UserId == userBId && n.Titulo == "De B");

        await userAClient.PatchAsync($"/api/notifications/{notifB.Id}/read", null);

        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(verifyDb.Notifications.First(n => n.Id == notifB.Id).Leida);
    }

    [Fact]
    public async Task MarkAllAsRead_MarksOnlyCurrentUsersNotifications()
    {
        var (userAClient, userAId) = await TestAuthHelper.CreateAuthenticatedUserAsync(_factory);
        var (_, userBId) = await TestAuthHelper.CreateAuthenticatedUserAsync(_factory);
        await SeedNotificationAsync(userAId, titulo: "A1");
        await SeedNotificationAsync(userAId, titulo: "A2");
        await SeedNotificationAsync(userBId, titulo: "B1");

        var resp = await userAClient.PatchAsync("/api/notifications/read-all", null);
        Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(db.Notifications.Where(n => n.UserId == userAId).All(n => n.Leida));
        Assert.False(db.Notifications.First(n => n.UserId == userBId).Leida);
    }
}
```

- [ ] **Step 2: Correr la suite para verificar que las 6 pruebas pasan**

Run: `cd C:\Agentes\PropertyMap && dotnet test src/PropertyMap.Tests/PropertyMap.Tests.csproj --filter "FullyQualifiedName~NotificationsControllerTests"`
Expected: `Correctas! - Con error: 0, Superado: 6`

- [ ] **Step 3: Commit**

```bash
cd C:\Agentes\PropertyMap\src
git add PropertyMap.Tests/Api/NotificationsControllerTests.cs
git commit -m "test: add integration tests for NotificationsController"
```

---

### Task 4: Verificación final de la suite completa

**Files:** ninguno (solo verificación)

- [ ] **Step 1: Correr toda la suite de tests**

Run: `cd C:\Agentes\PropertyMap && dotnet test src/PropertyMap.Tests/PropertyMap.Tests.csproj`
Expected: `Correctas! - Con error: 0, Superado: 101` (83 existentes + 7 de AdminControllerTests + 5 de ListingsControllerTests + 6 de NotificationsControllerTests)

- [ ] **Step 2: Correr el build completo de la solución**

Run: `cd C:\Agentes\PropertyMap && dotnet build src/PropertyMap.Web/PropertyMap.Web.sln`
Expected: `Compilación correcta. 0 Errores`

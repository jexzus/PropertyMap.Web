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

    private record CreatedIdDto(int id);

    [Fact]
    public async Task ApprovingListing_NotifiesMatchingAlert()
    {
        var (userClient, _) = await TestAuthHelper.CreateAuthenticatedUserAsync(_factory);
        await userClient.PostAsJsonAsync("/api/alerts",
            new CreateAlertRequest("Casa en Mendoza", TipoOperacion.Venta, TipoPropiedad.Casa,
                "Mendoza", 80000, "USD", null));

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
        await adminClient.PatchAsJsonAsync($"/api/admin/listings/{created!.id}/review",
            new { Aprobar = true, MotivoRechazo = (string?)null });

        var notifications = await userClient.GetFromJsonAsync<List<NotificationDto>>("/api/notifications");
        Assert.NotNull(notifications);
        Assert.Contains(notifications!, n =>
            n.Tipo == TipoNotificacion.AlertaCoincidencia &&
            n.UrlAccion == $"/propiedad/{created.id}");
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
        await adminClient.PatchAsJsonAsync($"/api/admin/listings/{created!.id}/review",
            new { Aprobar = true, MotivoRechazo = (string?)null });

        var notifications = await userClient.GetFromJsonAsync<List<NotificationDto>>("/api/notifications");
        Assert.NotNull(notifications);
        Assert.DoesNotContain(notifications!, n => n.Tipo == TipoNotificacion.AlertaCoincidencia);
    }
}

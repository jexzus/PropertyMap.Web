using System.Net.Http.Json;
using PropertyMap.Core.DTOs.Properties;
using PropertyMap.Core.Enums;
using PropertyMap.Infrastructure.Data;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace PropertyMap.Tests.Api;

public class ViewTrackingTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public ViewTrackingTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static CreateListingRequest SampleListing() => new(
        Operacion: TipoOperacion.Venta,
        TipoPropiedad: TipoPropiedad.Casa,
        Titulo: "Casa tracking test",
        Descripcion: "Test",
        Precio: 100000,
        Moneda: "USD",
        DireccionTexto: "Calle 123",
        Ciudad: "Santa Rosa",
        Provincia: "La Pampa",
        Lat: -36.62,
        Lng: -64.29,
        Superficie: null,
        SuperficieCubierta: null,
        Ambientes: null,
        Dormitorios: null,
        Banos: null,
        Antiguedad: null,
        Cochera: false,
        Amenities: []
    );

    [Fact]
    public async Task GetListing_SameIpSameDay_CountsOnce()
    {
        // Arrange: create a published listing
        var (pubClient, _) = await TestAuthHelper.CreateAuthenticatedPublisherAsync(_factory);
        await TestAuthHelper.CreatePublisherProfileAsync(pubClient);
        var createResp = await pubClient.PostAsJsonAsync("/api/properties", SampleListing());
        var created = await createResp.Content.ReadFromJsonAsync<CreatedIdResponse>();

        // Publish via admin review
        var adminClient = _factory.CreateClient();
        var adminScope = _factory.Services.CreateScope();
        var adminUserManager = adminScope.ServiceProvider
            .GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<PropertyMap.Core.Entities.ApplicationUser>>();
        var adminUser = new PropertyMap.Core.Entities.ApplicationUser
        {
            UserName = "admin@test.com", Email = "admin@test.com",
            Nombre = "Admin", Apellido = "Test",
            EmailConfirmed = true,
            Estado = PropertyMap.Core.Enums.EstadoUsuario.Activo
        };
        await adminUserManager.CreateAsync(adminUser, "Admin123!");
        await adminUserManager.AddToRoleAsync(adminUser, "Admin");
        var adminLogin = await adminClient.PostAsJsonAsync("/api/auth/login",
            new PropertyMap.Core.DTOs.Auth.LoginRequest("admin@test.com", "Admin123!"));
        var adminAuth = await adminLogin.Content.ReadFromJsonAsync<PropertyMap.Core.DTOs.Auth.AuthResponse>();
        adminClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", adminAuth!.AccessToken);
        await adminClient.PatchAsJsonAsync($"/api/admin/listings/{created!.Id}/review",
            new { Aprobar = true, MotivoRechazo = (string?)null });

        // Act: anonymous client hits GET /api/listings/{id} twice
        var anonClient = _factory.CreateClient();
        await anonClient.GetAsync($"/api/listings/{created.Id}");
        await anonClient.GetAsync($"/api/listings/{created.Id}");

        // Assert: only 1 view recorded
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var viewCount = db.PropertyViews.Count(v => v.PropertyListingId == created.Id);
        Assert.Equal(1, viewCount);
    }

    private record CreatedIdResponse(int Id);
}

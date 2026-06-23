using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using PropertyMap.Core.DTOs.Properties;
using PropertyMap.Core.DTOs.User;
using PropertyMap.Core.Enums;
using Xunit;

namespace PropertyMap.Tests.Api;

public class FavoritesControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public FavoritesControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static CreateListingRequest SampleListing() => new(
        Operacion: TipoOperacion.Venta,
        TipoPropiedad: TipoPropiedad.Departamento,
        Titulo: "Depto favorito test",
        Descripcion: "Test",
        Precio: 80000,
        Moneda: "USD",
        DireccionTexto: "Av. Test 456",
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
        Amenities: []
    );

    private async Task<int> CreatePublishedListingAsync()
    {
        var (pubClient, _) = await TestAuthHelper.CreateAuthenticatedPublisherAsync(_factory);
        await TestAuthHelper.CreatePublisherProfileAsync(pubClient);
        var createResp = await pubClient.PostAsJsonAsync("/api/properties", SampleListing());
        var created = await createResp.Content.ReadFromJsonAsync<CreatedIdDto>();

        // approve via admin
        var adminScope = _factory.Services.CreateScope();
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

        return created.Id;
    }

    private record CreatedIdDto(int Id);

    [Fact]
    public async Task AddFavorite_WithoutAuth_Returns401()
    {
        var client = _factory.CreateClient();
        var resp = await client.PostAsync("/api/favorites/1", null);
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task AddAndRemoveFavorite_WorksCorrectly()
    {
        var listingId = await CreatePublishedListingAsync();
        var (client, _) = await TestAuthHelper.CreateAuthenticatedPublisherAsync(_factory);

        // Add
        var addResp = await client.PostAsync($"/api/favorites/{listingId}", null);
        Assert.Equal(HttpStatusCode.OK, addResp.StatusCode);

        // List
        var listResp = await client.GetAsync("/api/favorites");
        var favs = await listResp.Content.ReadFromJsonAsync<List<MyListingDto>>();
        Assert.Contains(favs!, f => f.Id == listingId);

        // Remove
        var removeResp = await client.DeleteAsync($"/api/favorites/{listingId}");
        Assert.Equal(HttpStatusCode.OK, removeResp.StatusCode);

        // List again — empty
        var listResp2 = await client.GetAsync("/api/favorites");
        var favs2 = await listResp2.Content.ReadFromJsonAsync<List<MyListingDto>>();
        Assert.DoesNotContain(favs2!, f => f.Id == listingId);
    }

    [Fact]
    public async Task AddFavorite_Idempotent_DoesNotDuplicate()
    {
        var listingId = await CreatePublishedListingAsync();
        var (client, _) = await TestAuthHelper.CreateAuthenticatedPublisherAsync(_factory);

        await client.PostAsync($"/api/favorites/{listingId}", null);
        await client.PostAsync($"/api/favorites/{listingId}", null);

        var listResp = await client.GetAsync("/api/favorites");
        var favs = await listResp.Content.ReadFromJsonAsync<List<MyListingDto>>();
        Assert.Equal(1, favs!.Count(f => f.Id == listingId));
    }

    [Fact]
    public async Task GetStatus_AnonymousUser_ReturnsFalseWithCount()
    {
        var listingId = await CreatePublishedListingAsync();
        var (authClient, _) = await TestAuthHelper.CreateAuthenticatedPublisherAsync(_factory);
        await authClient.PostAsync($"/api/favorites/{listingId}", null);

        var anonClient = _factory.CreateClient();
        var resp = await anonClient.GetAsync($"/api/favorites/{listingId}/status");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var status = await resp.Content.ReadFromJsonAsync<FavoriteStatusResponse>();
        Assert.False(status!.IsFavorited);
        Assert.Equal(1, status.Count);
    }
}

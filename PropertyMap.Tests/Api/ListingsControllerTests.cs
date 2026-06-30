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
        Assert.Equal(-32.89, found.Lat, precision: 2);
        Assert.Equal(-68.84, found.Lng, precision: 2);
    }

    [Fact]
    public async Task GetAll_DoesNotExposePublisherSensitiveFields()
    {
        await CreateAndPublishListingAsync("Depto sin datos sensibles");

        var anonClient = _factory.CreateClient();
        var resp = await anonClient.GetAsync("/api/listings");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var json = await resp.Content.ReadFromJsonAsync<System.Text.Json.JsonDocument>();
        var publisherElement = json!.RootElement[0].GetProperty("publisher");

        Assert.False(publisherElement.TryGetProperty("email", out _));
        Assert.False(publisherElement.TryGetProperty("telefono", out _));
        Assert.False(publisherElement.TryGetProperty("userId", out _));
    }
}

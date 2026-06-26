using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using PropertyMap.Core.DTOs.Plans;
using PropertyMap.Core.DTOs.Properties;
using PropertyMap.Core.Enums;
using Xunit;

namespace PropertyMap.Tests.Api;

public class DestacadoTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public DestacadoTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private record CreatedIdDto(int Id);

    private async Task<(HttpClient pubClient, int listingId, string userId)> CreateApprovedListingAsync()
    {
        var (pubClient, userId) = await TestAuthHelper.CreateAuthenticatedPublisherAsync(_factory);
        await TestAuthHelper.CreatePublisherProfileAsync(pubClient);

        var listing = new CreateListingRequest(
            Operacion: TipoOperacion.Venta, TipoPropiedad: TipoPropiedad.Casa,
            Titulo: "Casa destacable", Descripcion: "Test",
            Precio: 70000, Moneda: "USD",
            DireccionTexto: "Av. Destacado 1", Ciudad: "Bariloche", Provincia: "Río Negro",
            Lat: -41.13, Lng: -71.30,
            Superficie: null, SuperficieCubierta: null, Ambientes: null,
            Dormitorios: null, Banos: null, Antiguedad: null,
            Cochera: false, Amenities: []);
        var createResp = await pubClient.PostAsJsonAsync("/api/properties", listing);
        var created = await createResp.Content.ReadFromJsonAsync<CreatedIdDto>();

        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider
            .GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<PropertyMap.Core.Entities.ApplicationUser>>();
        var adminEmail = $"admin_destacado_{Guid.NewGuid()}@test.com";
        var adminUser = new PropertyMap.Core.Entities.ApplicationUser
        {
            UserName = adminEmail, Email = adminEmail,
            Nombre = "Admin", Apellido = "Destacado", EmailConfirmed = true,
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
        await adminClient.PatchAsJsonAsync($"/api/admin/listings/{created!.Id}/review",
            new { Aprobar = true, MotivoRechazo = (string?)null });

        return (pubClient, created.Id, userId);
    }

    [Fact]
    public async Task ToggleDestacado_WithoutSubscription_Returns400()
    {
        var (pubClient, listingId, _) = await CreateApprovedListingAsync();

        var resp = await pubClient.PatchAsync($"/api/properties/{listingId}/destacar", null);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task ToggleDestacado_WithSubscriptionUnderLimit_Succeeds()
    {
        var (pubClient, listingId, _) = await CreateApprovedListingAsync();
        var plans = await (await pubClient.GetAsync("/api/plans")).Content.ReadFromJsonAsync<List<PlanDto>>();
        var profesional = plans!.First(p => p.Slug == "profesional");
        await pubClient.PostAsJsonAsync("/api/subscriptions", new SubscribeRequest(profesional.Id));

        var resp = await pubClient.PatchAsync($"/api/properties/{listingId}/destacar", null);

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<Dictionary<string, bool>>();
        Assert.True(body!["destacado"]);
    }

    [Fact]
    public async Task ToggleDestacado_Twice_TogglesBackOff()
    {
        var (pubClient, listingId, _) = await CreateApprovedListingAsync();
        var plans = await (await pubClient.GetAsync("/api/plans")).Content.ReadFromJsonAsync<List<PlanDto>>();
        var profesional = plans!.First(p => p.Slug == "profesional");
        await pubClient.PostAsJsonAsync("/api/subscriptions", new SubscribeRequest(profesional.Id));

        await pubClient.PatchAsync($"/api/properties/{listingId}/destacar", null);
        var secondResp = await pubClient.PatchAsync($"/api/properties/{listingId}/destacar", null);

        var body = await secondResp.Content.ReadFromJsonAsync<Dictionary<string, bool>>();
        Assert.False(body!["destacado"]);
    }

    [Fact]
    public async Task GetActiveListingsForMap_ListsDestacadosFirst()
    {
        var (pubClient, listingId, _) = await CreateApprovedListingAsync();
        var plans = await (await pubClient.GetAsync("/api/plans")).Content.ReadFromJsonAsync<List<PlanDto>>();
        var profesional = plans!.First(p => p.Slug == "profesional");
        await pubClient.PostAsJsonAsync("/api/subscriptions", new SubscribeRequest(profesional.Id));
        await pubClient.PatchAsync($"/api/properties/{listingId}/destacar", null);

        var mapResp = await _factory.CreateClient().GetAsync("/api/listings/map");
        var mapListings = await mapResp.Content.ReadFromJsonAsync<List<PropertyMap.Core.DTOs.ListingMapDto>>();

        Assert.NotNull(mapListings);
        Assert.Contains(mapListings!, l => l.Id == listingId);
    }
}

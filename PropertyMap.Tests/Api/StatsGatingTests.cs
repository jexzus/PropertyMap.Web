using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using PropertyMap.Core.DTOs.Plans;
using PropertyMap.Core.DTOs.Properties;
using PropertyMap.Core.DTOs.Stats;
using PropertyMap.Core.Enums;
using Xunit;

namespace PropertyMap.Tests.Api;

public class StatsGatingTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public StatsGatingTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private record CreatedIdDto(int Id);

    private async Task<(HttpClient pubClient, int listingId)> CreateApprovedListingWithFavoriteAsync()
    {
        var (pubClient, _) = await TestAuthHelper.CreateAuthenticatedPublisherAsync(_factory);
        await TestAuthHelper.CreatePublisherProfileAsync(pubClient);

        var listing = new CreateListingRequest(
            Operacion: TipoOperacion.Venta, TipoPropiedad: TipoPropiedad.Casa,
            Titulo: "Casa con stats", Descripcion: "Test",
            Precio: 70000, Moneda: "USD",
            DireccionTexto: "Av. Stats 1", Ciudad: "Bariloche", Provincia: "Río Negro",
            Lat: -41.13, Lng: -71.30,
            Superficie: null, SuperficieCubierta: null, Ambientes: null,
            Dormitorios: null, Banos: null, Antiguedad: null,
            Cochera: false, Amenities: []);
        var createResp = await pubClient.PostAsJsonAsync("/api/properties", listing);
        var created = await createResp.Content.ReadFromJsonAsync<CreatedIdDto>();

        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider
            .GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<PropertyMap.Core.Entities.ApplicationUser>>();
        var adminEmail = $"admin_stats_{Guid.NewGuid()}@test.com";
        var adminUser = new PropertyMap.Core.Entities.ApplicationUser
        {
            UserName = adminEmail, Email = adminEmail,
            Nombre = "Admin", Apellido = "Stats", EmailConfirmed = true,
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

        var (favClient, _) = await TestAuthHelper.CreateAuthenticatedUserAsync(_factory);
        await favClient.PostAsync($"/api/favorites/{created.Id}", null);

        return (pubClient, created.Id);
    }

    [Fact]
    public async Task GetForListing_WithoutSubscription_HidesAdvancedMetrics()
    {
        var (pubClient, listingId) = await CreateApprovedListingWithFavoriteAsync();

        var resp = await pubClient.GetAsync($"/api/stats/listings/{listingId}");
        var stats = await resp.Content.ReadFromJsonAsync<ListingStatsDto>();

        Assert.NotNull(stats);
        Assert.Equal(0, stats!.Favoritos);
    }

    [Fact]
    public async Task GetForListing_WithAdvancedStatsPlan_ShowsAdvancedMetrics()
    {
        var (pubClient, listingId) = await CreateApprovedListingWithFavoriteAsync();

        var plans = await (await pubClient.GetAsync("/api/plans")).Content.ReadFromJsonAsync<List<PlanDto>>();
        var profesional = plans!.First(p => p.Slug == "profesional");
        await pubClient.PostAsJsonAsync("/api/subscriptions", new SubscribeRequest(profesional.Id));

        var resp = await pubClient.GetAsync($"/api/stats/listings/{listingId}");
        var stats = await resp.Content.ReadFromJsonAsync<ListingStatsDto>();

        Assert.NotNull(stats);
        Assert.Equal(1, stats!.Favoritos);
    }

    [Fact]
    public async Task GetMine_WithoutSubscription_HidesAdvancedMetrics()
    {
        var (pubClient, listingId) = await CreateApprovedListingWithFavoriteAsync();

        var resp = await pubClient.GetAsync("/api/stats/mine");
        var stats = await resp.Content.ReadFromJsonAsync<List<ListingStatsDto>>();

        var listingStats = stats!.Single(s => s.ListingId == listingId);
        Assert.Equal(0, listingStats.Favoritos);
    }

    [Fact]
    public async Task GetMine_WithAdvancedStatsPlan_ShowsAdvancedMetrics()
    {
        var (pubClient, listingId) = await CreateApprovedListingWithFavoriteAsync();

        var plans = await (await pubClient.GetAsync("/api/plans")).Content.ReadFromJsonAsync<List<PlanDto>>();
        var profesional = plans!.First(p => p.Slug == "profesional");
        await pubClient.PostAsJsonAsync("/api/subscriptions", new SubscribeRequest(profesional.Id));

        var resp = await pubClient.GetAsync("/api/stats/mine");
        var stats = await resp.Content.ReadFromJsonAsync<List<ListingStatsDto>>();

        var listingStats = stats!.Single(s => s.ListingId == listingId);
        Assert.Equal(1, listingStats.Favoritos);
    }
}

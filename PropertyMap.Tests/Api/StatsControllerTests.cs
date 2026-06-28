using System.Net;
using System.Net.Http.Json;
using PropertyMap.Core.DTOs.Plans;
using PropertyMap.Core.DTOs.Properties;
using PropertyMap.Core.DTOs.Stats;
using PropertyMap.Core.Enums;
using Xunit;

namespace PropertyMap.Tests.Api;

public class StatsControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public StatsControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private record CreatedIdDto(int Id);

    [Fact]
    public async Task GetMine_NoListings_ReturnsEmptyList()
    {
        var (client, _) = await TestAuthHelper.CreateAuthenticatedPublisherAsync(_factory);
        await TestAuthHelper.CreatePublisherProfileAsync(client);

        var resp = await client.GetAsync("/api/stats/mine");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var stats = await resp.Content.ReadFromJsonAsync<List<ListingStatsDto>>();
        Assert.Empty(stats!);
    }

    [Fact]
    public async Task GetForListing_AfterFavoriteAndConsulta_CountsCorrectly()
    {
        var (pubClient, _) = await TestAuthHelper.CreateAuthenticatedPublisherAsync(_factory);
        await TestAuthHelper.CreatePublisherProfileAsync(pubClient);

        var listing = new CreateListingRequest(
            Operacion: TipoOperacion.Venta, TipoPropiedad: TipoPropiedad.Casa,
            Titulo: "Casa con stats", Descripcion: "Test",
            Precio: 60000, Moneda: "USD",
            DireccionTexto: "Av. Stats 1", Ciudad: "Neuquén", Provincia: "Neuquén",
            Lat: -38.95, Lng: -68.06,
            Superficie: null, SuperficieCubierta: null, Ambientes: null,
            Dormitorios: null, Banos: null, Antiguedad: null,
            Cochera: false, Amenities: []);
        var createResp = await pubClient.PostAsJsonAsync("/api/properties", listing);
        var created = await createResp.Content.ReadFromJsonAsync<CreatedIdDto>();

        var (userClient, _) = await TestAuthHelper.CreateAuthenticatedUserAsync(_factory);
        await userClient.PostAsJsonAsync($"/api/favorites/{created!.Id}", new { });

        var plans = await (await pubClient.GetAsync("/api/plans")).Content.ReadFromJsonAsync<List<PlanDto>>();
        var profesional = plans!.First(p => p.Slug == "profesional");
        await pubClient.PostAsJsonAsync("/api/subscriptions", new SubscribeRequest(profesional.Id));

        var statsResp = await pubClient.GetAsync($"/api/stats/listings/{created.Id}");
        Assert.Equal(HttpStatusCode.OK, statsResp.StatusCode);
        var stats = await statsResp.Content.ReadFromJsonAsync<ListingStatsDto>();
        Assert.NotNull(stats);
        Assert.Equal(1, stats!.Favoritos);
        Assert.Equal(0, stats.Consultas);
        Assert.Equal(0, stats.Conversiones);
    }

    [Fact]
    public async Task GetForListing_NotOwner_Returns404()
    {
        var (pubClientA, _) = await TestAuthHelper.CreateAuthenticatedPublisherAsync(_factory);
        await TestAuthHelper.CreatePublisherProfileAsync(pubClientA);
        var listing = new CreateListingRequest(
            Operacion: TipoOperacion.Venta, TipoPropiedad: TipoPropiedad.Casa,
            Titulo: "Casa de otro publisher", Descripcion: "Test",
            Precio: 40000, Moneda: "USD",
            DireccionTexto: "Av. Otro 2", Ciudad: "Neuquén", Provincia: "Neuquén",
            Lat: -38.95, Lng: -68.06,
            Superficie: null, SuperficieCubierta: null, Ambientes: null,
            Dormitorios: null, Banos: null, Antiguedad: null,
            Cochera: false, Amenities: []);
        var createResp = await pubClientA.PostAsJsonAsync("/api/properties", listing);
        var created = await createResp.Content.ReadFromJsonAsync<CreatedIdDto>();

        var (pubClientB, _) = await TestAuthHelper.CreateAuthenticatedPublisherAsync(_factory);
        await TestAuthHelper.CreatePublisherProfileAsync(pubClientB);

        var resp = await pubClientB.GetAsync($"/api/stats/listings/{created!.Id}");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }
}

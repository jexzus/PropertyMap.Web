using System.Net;
using System.Net.Http.Json;
using PropertyMap.Core.DTOs.Plans;
using PropertyMap.Core.DTOs.Properties;
using PropertyMap.Core.Enums;
using Xunit;

namespace PropertyMap.Tests.Api;

public class PlanLimitsTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public PlanLimitsTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private record CreatedIdDto(int Id);

    private static CreateListingRequest BuildListingRequest(string titulo) => new(
        Operacion: TipoOperacion.Venta, TipoPropiedad: TipoPropiedad.Casa,
        Titulo: titulo, Descripcion: "Test",
        Precio: 70000, Moneda: "USD",
        DireccionTexto: "Av. Limite 1", Ciudad: "Bariloche", Provincia: "Río Negro",
        Lat: -41.13, Lng: -71.30,
        Superficie: null, SuperficieCubierta: null, Ambientes: null,
        Dormitorios: null, Banos: null, Antiguedad: null,
        Cochera: false, Amenities: []);

    [Fact]
    public async Task Create_WithoutSubscription_AllowsUpToFreeLimitThenBlocks()
    {
        var (pubClient, _) = await TestAuthHelper.CreateAuthenticatedPublisherAsync(_factory);
        await TestAuthHelper.CreatePublisherProfileAsync(pubClient);

        for (var i = 0; i < 3; i++)
        {
            var resp = await pubClient.PostAsJsonAsync("/api/properties", BuildListingRequest($"Casa {i}"));
            Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        }

        var blockedResp = await pubClient.PostAsJsonAsync("/api/properties", BuildListingRequest("Casa 4"));

        Assert.Equal(HttpStatusCode.BadRequest, blockedResp.StatusCode);
    }

    [Fact]
    public async Task Create_WithPremiumSubscription_HasNoLimit()
    {
        var (pubClient, _) = await TestAuthHelper.CreateAuthenticatedPublisherAsync(_factory);
        await TestAuthHelper.CreatePublisherProfileAsync(pubClient);

        var plans = await (await pubClient.GetAsync("/api/plans")).Content.ReadFromJsonAsync<List<PlanDto>>();
        var premium = plans!.First(p => p.Slug == "premium");
        await pubClient.PostAsJsonAsync("/api/subscriptions", new SubscribeRequest(premium.Id));

        for (var i = 0; i < 4; i++)
        {
            var resp = await pubClient.PostAsJsonAsync("/api/properties", BuildListingRequest($"Casa Premium {i}"));
            Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        }
    }
}

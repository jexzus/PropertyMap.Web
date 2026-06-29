using System.Net;
using System.Net.Http.Json;
using PropertyMap.Core.DTOs.Properties;
using PropertyMap.Core.Enums;
using Xunit;

namespace PropertyMap.Tests.Api;

public class PropertiesControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public PropertiesControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static CreateListingRequest SampleListing() => new(
        Operacion: TipoOperacion.Venta,
        TipoPropiedad: TipoPropiedad.Casa,
        Titulo: "Casa test",
        Descripcion: "Descripción de prueba",
        Precio: 100000,
        Moneda: "USD",
        DireccionTexto: "Calle Test 123",
        Ciudad: "Santa Rosa",
        Provincia: "La Pampa",
        Lat: -36.6200,
        Lng: -64.2895,
        Superficie: 100,
        SuperficieCubierta: 80,
        Ambientes: 3,
        Dormitorios: 2,
        Banos: 1,
        Antiguedad: 5,
        Cochera: true,
        Amenities: ["Balcón", "Parrilla"]
    );

    [Fact]
    public async Task CreateListing_WithoutAuth_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/properties", SampleListing());
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateListing_WithPublisherProfile_Returns201()
    {
        var (client, _) = await TestAuthHelper.CreateAuthenticatedPublisherAsync(_factory);
        await TestAuthHelper.CreatePublisherProfileAsync(client);

        var response = await client.PostAsJsonAsync("/api/properties", SampleListing());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task GetMine_ReturnsOwnListings()
    {
        var (client, _) = await TestAuthHelper.CreateAuthenticatedPublisherAsync(_factory);
        await TestAuthHelper.CreatePublisherProfileAsync(client);
        await client.PostAsJsonAsync("/api/properties", SampleListing());

        var response = await client.GetAsync("/api/properties/mine");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var listings = await response.Content.ReadFromJsonAsync<List<MyListingDto>>();
        Assert.NotEmpty(listings!);
    }

    [Fact]
    public async Task GetMine_WithoutPublisherProfile_ReturnsEmptyList()
    {
        var (client, _) = await TestAuthHelper.CreateAuthenticatedPublisherAsync(_factory);
        var response = await client.GetAsync("/api/properties/mine");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var listings = await response.Content.ReadFromJsonAsync<List<MyListingDto>>();
        Assert.Empty(listings!);
    }

    [Fact]
    public async Task DeleteListing_ByOtherUser_ReturnsForbid()
    {
        var (client1, _) = await TestAuthHelper.CreateAuthenticatedPublisherAsync(_factory);
        await TestAuthHelper.CreatePublisherProfileAsync(client1, "Publisher Uno");
        var createResp = await client1.PostAsJsonAsync("/api/properties", SampleListing());
        var created = await createResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var listingId = created.GetProperty("id").GetInt32();

        var (client2, _) = await TestAuthHelper.CreateAuthenticatedPublisherAsync(_factory);
        await TestAuthHelper.CreatePublisherProfileAsync(client2, "Publisher Dos");
        var deleteResp = await client2.DeleteAsync($"/api/properties/{listingId}");

        Assert.Equal(HttpStatusCode.Forbidden, deleteResp.StatusCode);
    }

    [Fact]
    public async Task DeleteListing_ByOwner_Returns204()
    {
        var (client, _) = await TestAuthHelper.CreateAuthenticatedPublisherAsync(_factory);
        await TestAuthHelper.CreatePublisherProfileAsync(client);
        var createResp = await client.PostAsJsonAsync("/api/properties", SampleListing());
        var created = await createResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var listingId = created.GetProperty("id").GetInt32();

        var deleteResp = await client.DeleteAsync($"/api/properties/{listingId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResp.StatusCode);
    }

    [Fact]
    public async Task UpdateStatus_Publisher_CanOnlyPause()
    {
        var (client, _) = await TestAuthHelper.CreateAuthenticatedPublisherAsync(_factory);
        await TestAuthHelper.CreatePublisherProfileAsync(client);
        var createResp = await client.PostAsJsonAsync("/api/properties", SampleListing());
        var created = await createResp.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        var listingId = created.GetProperty("id").GetInt32();

        // PatchAsJsonAsync might not exist — use SendAsync with HttpMethod.Patch
        var patchReq = new HttpRequestMessage(HttpMethod.Patch, $"/api/properties/{listingId}/status");
        patchReq.Content = JsonContent.Create(new UpdateListingStatusRequest(EstadoPublicacion.Publicada));
        var resp = await client.SendAsync(patchReq);
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Create_TituloExceedsMaxLength_Returns400()
    {
        var (pubClient, _) = await TestAuthHelper.CreateAuthenticatedPublisherAsync(_factory);
        await TestAuthHelper.CreatePublisherProfileAsync(pubClient);

        var tituloLargo = new string('A', 200); // excede el límite de 150
        var request = new CreateListingRequest(
            Operacion: TipoOperacion.Venta, TipoPropiedad: TipoPropiedad.Casa,
            Titulo: tituloLargo, Descripcion: "Test",
            Precio: 70000, Moneda: "USD",
            DireccionTexto: "Calle Validacion 1", Ciudad: "Salta", Provincia: "Salta",
            Lat: -24.78, Lng: -65.41,
            Superficie: null, SuperficieCubierta: null, Ambientes: null,
            Dormitorios: null, Banos: null, Antiguedad: null,
            Cochera: false, Amenities: []);

        var resp = await pubClient.PostAsJsonAsync("/api/properties", request);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }
}

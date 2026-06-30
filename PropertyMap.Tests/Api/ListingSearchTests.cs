using System.Net.Http.Json;
using PropertyMap.Core.DTOs;
using PropertyMap.Core.DTOs.Admin;
using PropertyMap.Core.DTOs.Properties;
using PropertyMap.Core.Entities;
using PropertyMap.Core.Enums;
using Xunit;

namespace PropertyMap.Tests.Api;

public class ListingSearchTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public ListingSearchTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private record CreatedIdDto(int Id);

    private static CreateListingRequest BuildListingRequest(
        string titulo, string ciudad = "Bariloche", decimal precio = 80000,
        TipoOperacion operacion = TipoOperacion.Venta) => new(
        Operacion: operacion, TipoPropiedad: TipoPropiedad.Casa,
        Titulo: titulo, Descripcion: "Casa con vista al lago y jardín amplio",
        Precio: precio, Moneda: "USD",
        DireccionTexto: "Av. Búsqueda 1", Ciudad: ciudad, Provincia: "Río Negro",
        Lat: -41.13, Lng: -71.30,
        Superficie: null, SuperficieCubierta: null, Ambientes: null,
        Dormitorios: null, Banos: null, Antiguedad: null,
        Cochera: false, Amenities: []);

    private async Task<int> CreateAndPublishListingAsync(
        string titulo, string ciudad = "Bariloche", decimal precio = 80000,
        TipoOperacion operacion = TipoOperacion.Venta)
    {
        var (pubClient, _) = await TestAuthHelper.CreateAuthenticatedPublisherAsync(_factory);
        await TestAuthHelper.CreatePublisherProfileAsync(pubClient);
        var createResp = await pubClient.PostAsJsonAsync("/api/properties",
            BuildListingRequest(titulo, ciudad, precio, operacion));
        var created = await createResp.Content.ReadFromJsonAsync<CreatedIdDto>();

        var (adminClient, _) = await TestAuthHelper.CreateAuthenticatedAdminAsync(_factory);
        await adminClient.PatchAsJsonAsync($"/api/admin/listings/{created!.Id}/review",
            new ReviewListingRequest(true, null));

        return created.Id;
    }

    [Fact]
    public async Task Search_ByKeywordInTitulo_FindsMatch()
    {
        var listingId = await CreateAndPublishListingAsync("Casa exclusiva en Nahuel Huapi");

        var anonClient = _factory.CreateClient();
        var result = await anonClient.GetFromJsonAsync<PagedResultDto<PropertyListing>>(
            "/api/listings/search?q=Nahuel");

        Assert.Contains(result!.Items, l => l.Id == listingId);
    }

    [Fact]
    public async Task Search_ByKeywordInCiudad_FindsMatch()
    {
        var listingId = await CreateAndPublishListingAsync("Depto centrico", ciudad: "Villa La Angostura");

        var anonClient = _factory.CreateClient();
        var result = await anonClient.GetFromJsonAsync<PagedResultDto<PropertyListing>>(
            "/api/listings/search?q=Angostura");

        Assert.Contains(result!.Items, l => l.Id == listingId);
    }

    [Fact]
    public async Task Search_CombinedFilters_AppliesAll()
    {
        var matchId = await CreateAndPublishListingAsync(
            "Cabaña de montaña", precio: 60000, operacion: TipoOperacion.Venta);
        var noMatchPriceId = await CreateAndPublishListingAsync(
            "Cabaña cara de montaña", precio: 500000, operacion: TipoOperacion.Venta);
        var noMatchOperacionId = await CreateAndPublishListingAsync(
            "Cabaña de montaña en alquiler", precio: 60000, operacion: TipoOperacion.Alquiler);

        var anonClient = _factory.CreateClient();
        var result = await anonClient.GetFromJsonAsync<PagedResultDto<PropertyListing>>(
            "/api/listings/search?q=montaña&operacion=Venta&precioMax=100000");

        Assert.Contains(result!.Items, l => l.Id == matchId);
        Assert.DoesNotContain(result.Items, l => l.Id == noMatchPriceId);
        Assert.DoesNotContain(result.Items, l => l.Id == noMatchOperacionId);
    }

    [Fact]
    public async Task Search_Pagination_RespectsPageAndPageSizeAndReturnsCorrectTotalCount()
    {
        var marker = Guid.NewGuid().ToString("N")[..8];
        for (var i = 0; i < 5; i++)
            await CreateAndPublishListingAsync($"Propiedad paginacion {marker} {i}");

        var anonClient = _factory.CreateClient();
        var page1 = await anonClient.GetFromJsonAsync<PagedResultDto<PropertyListing>>(
            $"/api/listings/search?q={marker}&page=1&pageSize=2");
        var page2 = await anonClient.GetFromJsonAsync<PagedResultDto<PropertyListing>>(
            $"/api/listings/search?q={marker}&page=2&pageSize=2");

        Assert.Equal(5, page1!.TotalCount);
        Assert.Equal(2, page1.Items.Count);
        Assert.Equal(2, page2!.Items.Count);
        Assert.Empty(page1.Items.Select(l => l.Id).Intersect(page2.Items.Select(l => l.Id)));
    }

    [Fact]
    public async Task Search_NoResults_ReturnsEmptyItemsWithZeroTotalCount()
    {
        var anonClient = _factory.CreateClient();
        var result = await anonClient.GetFromJsonAsync<PagedResultDto<PropertyListing>>(
            "/api/listings/search?q=textoquenuncavaaexistirxyz123");

        Assert.Empty(result!.Items);
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task GetForMap_StillReturnsAllListings_UnaffectedBySearchChanges()
    {
        var listingId = await CreateAndPublishListingAsync("Propiedad para verificar mapa intacto");

        var anonClient = _factory.CreateClient();
        var mapResp = await anonClient.GetAsync("/api/listings/map");
        var mapListings = await mapResp.Content.ReadFromJsonAsync<List<ListingMapDto>>();

        Assert.Contains(mapListings!, l => l.Id == listingId);
    }
}

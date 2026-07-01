using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using PropertyMap.Core.DTOs.Admin;
using PropertyMap.Core.DTOs.Properties;
using PropertyMap.Core.Enums;
using PropertyMap.Infrastructure.Data;
using Xunit;

namespace PropertyMap.Tests.Api;

public class AdminControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public AdminControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private record CreatedIdDto(int Id);

    private static CreateListingRequest BuildListingRequest(string titulo) => new(
        Operacion: TipoOperacion.Venta, TipoPropiedad: TipoPropiedad.Casa,
        Titulo: titulo, Descripcion: "Test",
        Precio: 80000, Moneda: "USD",
        DireccionTexto: "Av. Admin 100", Ciudad: "Neuquén", Provincia: "Neuquén",
        Lat: -38.95, Lng: -68.06,
        Superficie: null, SuperficieCubierta: null, Ambientes: null,
        Dormitorios: null, Banos: null, Antiguedad: null,
        Cochera: false, Amenities: []);

    private async Task<HttpClient> CreateAdminClientAsync()
    {
        var (adminClient, _) = await TestAuthHelper.CreateAuthenticatedAdminAsync(_factory);
        return adminClient;
    }

    private async Task<(HttpClient pubClient, int listingId)> CreatePendingListingAsync(string titulo = "Casa pendiente")
    {
        var (pubClient, _) = await TestAuthHelper.CreateAuthenticatedPublisherAsync(_factory);
        await TestAuthHelper.CreatePublisherProfileAsync(pubClient);
        var createResp = await pubClient.PostAsJsonAsync("/api/properties", BuildListingRequest(titulo));
        var created = await createResp.Content.ReadFromJsonAsync<CreatedIdDto>();
        return (pubClient, created!.Id);
    }

    [Fact]
    public async Task GetPending_ReturnsOnlyPendingListings()
    {
        var adminClient = await CreateAdminClientAsync();
        var (_, pendingId) = await CreatePendingListingAsync("Casa pendiente A");
        var (_, otherPendingId) = await CreatePendingListingAsync("Casa pendiente B");

        // Publicar otherPendingId para que NO aparezca como pendiente
        await adminClient.PatchAsJsonAsync($"/api/admin/listings/{otherPendingId}/review",
            new ReviewListingRequest(true, null));

        var pending = await adminClient.GetFromJsonAsync<List<PendingListingDto>>(
            "/api/admin/listings/pending");

        Assert.Contains(pending!, l => l.Id == pendingId);
        Assert.DoesNotContain(pending!, l => l.Id == otherPendingId);
    }

    // Nota: el trigger de NotifyMatchingAlertsAsync al aprobar ya está cubierto en detalle
    // por AlertMatchingTests.cs (ApprovingListing_NotifiesMatchingAlert /
    // ApprovingListing_DoesNotNotify_WhenPriceExceedsMax). Esta prueba se limita al
    // comportamiento propio de AdminController: que Review cambia el Estado a Publicada.
    [Fact]
    public async Task Review_Aprobar_PublishesListing()
    {
        var adminClient = await CreateAdminClientAsync();
        var (_, listingId) = await CreatePendingListingAsync();

        var reviewResp = await adminClient.PatchAsJsonAsync($"/api/admin/listings/{listingId}/review",
            new ReviewListingRequest(true, null));
        Assert.Equal(HttpStatusCode.OK, reviewResp.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var listing = db.PropertyListings.First(l => l.Id == listingId);
        Assert.Equal(EstadoPublicacion.Publicada, listing.Estado);
    }

    [Fact]
    public async Task Review_Rechazar_SetsBorradorWithMotivo()
    {
        var adminClient = await CreateAdminClientAsync();
        var (_, listingId) = await CreatePendingListingAsync();

        var reviewResp = await adminClient.PatchAsJsonAsync($"/api/admin/listings/{listingId}/review",
            new ReviewListingRequest(false, "Fotos de mala calidad"));
        Assert.Equal(HttpStatusCode.OK, reviewResp.StatusCode);

        var body = await reviewResp.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.Contains("Fotos de mala calidad", body!["message"]);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var listing = db.PropertyListings.First(l => l.Id == listingId);
        Assert.Equal(EstadoPublicacion.Borrador, listing.Estado);
    }

    [Fact]
    public async Task Review_NotPending_ReturnsBadRequest()
    {
        var adminClient = await CreateAdminClientAsync();
        var (_, listingId) = await CreatePendingListingAsync();
        await adminClient.PatchAsJsonAsync($"/api/admin/listings/{listingId}/review",
            new ReviewListingRequest(true, null));

        // Ya está Publicada, reintentar revisión debe fallar
        var secondReview = await adminClient.PatchAsJsonAsync($"/api/admin/listings/{listingId}/review",
            new ReviewListingRequest(true, null));

        Assert.Equal(HttpStatusCode.BadRequest, secondReview.StatusCode);
    }

    [Fact]
    public async Task Review_NotFound_Returns404()
    {
        var adminClient = await CreateAdminClientAsync();

        var reviewResp = await adminClient.PatchAsJsonAsync("/api/admin/listings/999999/review",
            new ReviewListingRequest(true, null));

        Assert.Equal(HttpStatusCode.NotFound, reviewResp.StatusCode);
    }

    [Fact]
    public async Task GetAll_ReturnsActiveListings()
    {
        var adminClient = await CreateAdminClientAsync();
        var (_, listingId) = await CreatePendingListingAsync("Casa para GetAll");
        await adminClient.PatchAsJsonAsync($"/api/admin/listings/{listingId}/review",
            new ReviewListingRequest(true, null));

        var allResp = await adminClient.GetAsync("/api/admin/listings");
        Assert.Equal(HttpStatusCode.OK, allResp.StatusCode);

        var listings = await allResp.Content.ReadFromJsonAsync<List<PropertyMap.Core.Entities.PropertyListing>>();
        Assert.Contains(listings!, l => l.Id == listingId);
    }

    [Fact]
    public async Task Endpoints_RequireAdminRole_RejectsOtherRoles()
    {
        var (pubClient, _) = await TestAuthHelper.CreateAuthenticatedPublisherAsync(_factory);

        var pendingResp = await pubClient.GetAsync("/api/admin/listings/pending");
        Assert.Equal(HttpStatusCode.Forbidden, pendingResp.StatusCode);

        var reviewResp = await pubClient.PatchAsJsonAsync("/api/admin/listings/1/review",
            new ReviewListingRequest(true, null));
        Assert.Equal(HttpStatusCode.Forbidden, reviewResp.StatusCode);
    }
}

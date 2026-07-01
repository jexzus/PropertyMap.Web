using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using PropertyMap.Core.DTOs.Properties;
using PropertyMap.Core.DTOs.Reports;
using PropertyMap.Core.Enums;
using PropertyMap.Infrastructure.Data;
using Xunit;

namespace PropertyMap.Tests.Api;

public class ReportsControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public ReportsControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private record CreatedIdDto(int id);
    private record ListingIdDto(int Id);

    private async Task<(HttpClient adminClient, int listingId)> SetupApprovedListingAsync()
    {
        var (pubClient, _) = await TestAuthHelper.CreateAuthenticatedPublisherAsync(_factory);
        await TestAuthHelper.CreatePublisherProfileAsync(pubClient);
        var listing = new CreateListingRequest(
            Operacion: TipoOperacion.Venta, TipoPropiedad: TipoPropiedad.Casa,
            Titulo: "Casa para reportar", Descripcion: "Test",
            Precio: 50000, Moneda: "USD",
            DireccionTexto: "Calle Falsa 123", Ciudad: "Rosario", Provincia: "Santa Fe",
            Lat: -32.95, Lng: -60.64,
            Superficie: null, SuperficieCubierta: null, Ambientes: null,
            Dormitorios: null, Banos: null, Antiguedad: null,
            Cochera: false, Amenities: []);
        var createResp = await pubClient.PostAsJsonAsync("/api/properties", listing);
        var created = await createResp.Content.ReadFromJsonAsync<CreatedIdDto>();

        var (adminClient, _) = await TestAuthHelper.CreateAuthenticatedAdminAsync(_factory);
        await adminClient.PatchAsJsonAsync($"/api/admin/listings/{created!.id}/review",
            new { Aprobar = true, MotivoRechazo = (string?)null });

        return (adminClient, created.id);
    }

    [Fact]
    public async Task CreateReport_ThenAdminSeesIt_AsPending()
    {
        var (adminClient, listingId) = await SetupApprovedListingAsync();
        var (userClient, _) = await TestAuthHelper.CreateAuthenticatedUserAsync(_factory);

        var reportResp = await userClient.PostAsJsonAsync("/api/reports",
            new CreateReportRequest(listingId, MotivoReporte.Estafa, "Sospechoso"));
        Assert.Equal(HttpStatusCode.OK, reportResp.StatusCode);

        var pending = await adminClient.GetFromJsonAsync<List<ReportDto>>("/api/admin/reports");
        Assert.Contains(pending!, r => r.PropertyListingId == listingId && r.Motivo == MotivoReporte.Estafa);
    }

    [Fact]
    public async Task ReviewReport_Resuelto_PausesListing()
    {
        var (adminClient, listingId) = await SetupApprovedListingAsync();
        var (userClient, _) = await TestAuthHelper.CreateAuthenticatedUserAsync(_factory);
        await userClient.PostAsJsonAsync("/api/reports",
            new CreateReportRequest(listingId, MotivoReporte.Spam, null));

        var pending = await adminClient.GetFromJsonAsync<List<ReportDto>>("/api/admin/reports");
        var reportId = pending!.First(r => r.PropertyListingId == listingId).Id;

        var reviewResp = await adminClient.PatchAsJsonAsync($"/api/admin/reports/{reportId}/review",
            new ReviewReportRequest(EstadoReporte.Resuelto));
        Assert.Equal(HttpStatusCode.NoContent, reviewResp.StatusCode);

        // The listing should no longer be active/published (GET /api/admin/listings only
        // returns Estado == Publicada listings, per ListingRepository.GetActiveListingsAsync).
        var allListings = await adminClient.GetFromJsonAsync<List<ListingIdDto>>("/api/admin/listings");
        Assert.NotNull(allListings);
        Assert.DoesNotContain(allListings!, l => l.Id == listingId);

        // Definitive check: read the listing's Estado directly from the database.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var listing = await db.PropertyListings.FindAsync(listingId);
        Assert.NotNull(listing);
        Assert.Equal(EstadoPublicacion.Pausada, listing!.Estado);
    }
}

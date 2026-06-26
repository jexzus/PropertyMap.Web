using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using PropertyMap.Core.DTOs.Properties;
using PropertyMap.Core.DTOs.Reports;
using PropertyMap.Core.Enums;
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

        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider
            .GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<PropertyMap.Core.Entities.ApplicationUser>>();
        var adminEmail = $"admin_report_{Guid.NewGuid()}@test.com";
        var adminUser = new PropertyMap.Core.Entities.ApplicationUser
        {
            UserName = adminEmail, Email = adminEmail,
            Nombre = "Admin", Apellido = "Report", EmailConfirmed = true,
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

        var listingResp = await adminClient.GetAsync($"/api/properties/{listingId}");
        var detail = await listingResp.Content.ReadFromJsonAsync<PropertyMap.Core.DTOs.ListingDetailDto>();
        var allListings = await adminClient.GetFromJsonAsync<List<object>>("/api/admin/listings");
        Assert.NotNull(allListings);
    }
}

using System.Net;
using System.Net.Http.Json;
using PropertyMap.Core.DTOs.Admin;
using PropertyMap.Core.DTOs.Properties;
using PropertyMap.Core.DTOs.Reports;
using PropertyMap.Core.Enums;
using Xunit;

namespace PropertyMap.Tests.Api;

public class AuditLogTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public AuditLogTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private record CreatedIdDto(int Id);

    private static CreateListingRequest BuildListingRequest(string titulo) => new(
        Operacion: TipoOperacion.Venta, TipoPropiedad: TipoPropiedad.Casa,
        Titulo: titulo, Descripcion: "Test",
        Precio: 90000, Moneda: "USD",
        DireccionTexto: "Av. Auditoria 1", Ciudad: "Cordoba", Provincia: "Cordoba",
        Lat: -31.42, Lng: -64.18,
        Superficie: null, SuperficieCubierta: null, Ambientes: null,
        Dormitorios: null, Banos: null, Antiguedad: null,
        Cochera: false, Amenities: []);

    private async Task<(HttpClient pubClient, int listingId)> CreatePendingListingAsync(string titulo)
    {
        var (pubClient, _) = await TestAuthHelper.CreateAuthenticatedPublisherAsync(_factory);
        await TestAuthHelper.CreatePublisherProfileAsync(pubClient);
        var createResp = await pubClient.PostAsJsonAsync("/api/properties", BuildListingRequest(titulo));
        var created = await createResp.Content.ReadFromJsonAsync<CreatedIdDto>();
        return (pubClient, created!.Id);
    }

    [Fact]
    public async Task Review_Aprobar_LogsAuditEntry()
    {
        var (adminClient, _) = await TestAuthHelper.CreateAuthenticatedAdminAsync(_factory);
        var (_, listingId) = await CreatePendingListingAsync("Casa auditoria aprobar");

        await adminClient.PatchAsJsonAsync($"/api/admin/listings/{listingId}/review",
            new ReviewListingRequest(true, null));

        var logs = await adminClient.GetFromJsonAsync<List<AuditLogDto>>("/api/admin/audit-logs");

        Assert.Contains(logs!, l =>
            l.Accion == "AprobarListing" && l.Entidad == "PropertyListing" && l.EntidadId == listingId.ToString());
    }

    [Fact]
    public async Task Review_Rechazar_LogsAuditEntryWithMotivo()
    {
        var (adminClient, _) = await TestAuthHelper.CreateAuthenticatedAdminAsync(_factory);
        var (_, listingId) = await CreatePendingListingAsync("Casa auditoria rechazar");

        await adminClient.PatchAsJsonAsync($"/api/admin/listings/{listingId}/review",
            new ReviewListingRequest(false, "Fotos borrosas"));

        var logs = await adminClient.GetFromJsonAsync<List<AuditLogDto>>("/api/admin/audit-logs");

        Assert.Contains(logs!, l =>
            l.Accion == "RechazarListing" && l.EntidadId == listingId.ToString() && l.Detalles == "Fotos borrosas");
    }

    [Fact]
    public async Task ReviewReport_Resuelto_LogsAuditEntry()
    {
        var (adminClient, _) = await TestAuthHelper.CreateAuthenticatedAdminAsync(_factory);
        var (_, listingId) = await CreatePendingListingAsync("Casa auditoria reporte");
        await adminClient.PatchAsJsonAsync($"/api/admin/listings/{listingId}/review",
            new ReviewListingRequest(true, null));

        var (userClient, _) = await TestAuthHelper.CreateAuthenticatedUserAsync(_factory);
        await userClient.PostAsJsonAsync("/api/reports",
            new CreateReportRequest(listingId, MotivoReporte.Spam, null));

        var pending = await adminClient.GetFromJsonAsync<List<ReportDto>>("/api/admin/reports");
        var reportId = pending!.First(r => r.PropertyListingId == listingId).Id;

        await adminClient.PatchAsJsonAsync($"/api/admin/reports/{reportId}/review",
            new ReviewReportRequest(EstadoReporte.Resuelto));

        var logs = await adminClient.GetFromJsonAsync<List<AuditLogDto>>("/api/admin/audit-logs");

        Assert.Contains(logs!, l =>
            l.Accion == "ResolverReporte" && l.Entidad == "Report" && l.EntidadId == reportId.ToString());
    }

    [Fact]
    public async Task GetAuditLogs_RequiresAdminRole()
    {
        var (pubClient, _) = await TestAuthHelper.CreateAuthenticatedPublisherAsync(_factory);

        var resp = await pubClient.GetAsync("/api/admin/audit-logs");

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }
}

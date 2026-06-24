using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using PropertyMap.Core.DTOs.Ratings;
using PropertyMap.Core.DTOs.Properties;
using PropertyMap.Core.DTOs.Consultas;
using PropertyMap.Core.Enums;
using Xunit;

namespace PropertyMap.Tests.Api;

public class RatingsControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public RatingsControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private record CreatedIdDto(int Id);

    private static CreateListingRequest AlquilerTemporarioListing() => new(
        Operacion: TipoOperacion.AlquilerTemporario,
        TipoPropiedad: TipoPropiedad.Departamento,
        Titulo: "Alquiler Temporario Test",
        Descripcion: "Test",
        Precio: 5000,
        Moneda: "ARS",
        DireccionTexto: "Av. Rating 123",
        Ciudad: "Córdoba",
        Provincia: "Córdoba",
        Lat: -31.42,
        Lng: -64.18,
        Superficie: null, SuperficieCubierta: null, Ambientes: null,
        Dormitorios: null, Banos: null, Antiguedad: null,
        Cochera: false, Amenities: []);

    private static CreateListingRequest VentaListing() => new(
        Operacion: TipoOperacion.Venta,
        TipoPropiedad: TipoPropiedad.Departamento,
        Titulo: "Venta Test",
        Descripcion: "Test",
        Precio: 90000,
        Moneda: "USD",
        DireccionTexto: "Av. Venta 456",
        Ciudad: "Buenos Aires",
        Provincia: "Buenos Aires",
        Lat: -34.60, Lng: -58.38,
        Superficie: null, SuperficieCubierta: null, Ambientes: null,
        Dormitorios: null, Banos: null, Antiguedad: null,
        Cochera: false, Amenities: []);

    private async Task<(HttpClient pubClient, int listingId, int publisherId)> SetupApprovedListingAsync(CreateListingRequest listing)
    {
        var (pubClient, _) = await TestAuthHelper.CreateAuthenticatedPublisherAsync(_factory);
        var publisherId = await TestAuthHelper.CreatePublisherProfileAsync(pubClient);
        var createResp = await pubClient.PostAsJsonAsync("/api/properties", listing);
        var created = await createResp.Content.ReadFromJsonAsync<CreatedIdDto>();

        using var adminScope = _factory.Services.CreateScope();
        var adminMgr = adminScope.ServiceProvider
            .GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<PropertyMap.Core.Entities.ApplicationUser>>();
        var adminEmail = $"admin_rating_{Guid.NewGuid()}@test.com";
        var adminUser = new PropertyMap.Core.Entities.ApplicationUser
        {
            UserName = adminEmail, Email = adminEmail,
            Nombre = "Admin", Apellido = "Rating",
            EmailConfirmed = true,
            Estado = PropertyMap.Core.Enums.EstadoUsuario.Activo
        };
        await adminMgr.CreateAsync(adminUser, "Admin123!");
        await adminMgr.AddToRoleAsync(adminUser, "Admin");
        var adminClient = _factory.CreateClient();
        var loginResp = await adminClient.PostAsJsonAsync("/api/auth/login",
            new PropertyMap.Core.DTOs.Auth.LoginRequest(adminEmail, "Admin123!"));
        var adminAuth = await loginResp.Content.ReadFromJsonAsync<PropertyMap.Core.DTOs.Auth.AuthResponse>();
        adminClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", adminAuth!.AccessToken);
        await adminClient.PatchAsJsonAsync($"/api/admin/listings/{created!.Id}/review",
            new { Aprobar = true, MotivoRechazo = (string?)null });

        return (pubClient, created.Id, publisherId);
    }

    private async Task<(HttpClient userClient, int consultaId)> CreateConsultaAsync(int listingId)
    {
        var (userClient, _) = await TestAuthHelper.CreateAuthenticatedUserAsync(_factory);
        var resp = await userClient.PostAsJsonAsync("/api/consultas",
            new CreateConsultaRequest(listingId, "Hola, consulta de test"));
        var detail = await resp.Content.ReadFromJsonAsync<ConsultaDetailDto>();
        return (userClient, detail!.Id);
    }

    [Fact]
    public async Task PostPropertyRating_WithConsulta_AlquilerTemporario_ReturnsStats()
    {
        var (_, listingId, _) = await SetupApprovedListingAsync(AlquilerTemporarioListing());
        var (userClient, _) = await CreateConsultaAsync(listingId);

        var resp = await userClient.PostAsJsonAsync("/api/ratings/property",
            new RatePropertyRequest(listingId, 5, 4, 3, "Excelente"));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var stats = await resp.Content.ReadFromJsonAsync<PropertyRatingStatsDto>();
        Assert.NotNull(stats);
        Assert.Equal(1, stats!.TotalValoraciones);
        Assert.Equal(5.0, stats.PromedioUbicacion);
        Assert.Equal(4.0, stats.PromedioEstado);
        Assert.Equal(3.0, stats.PrecioCal);
        Assert.Equal(4.0, stats.PromedioGeneral, 1);
    }

    [Fact]
    public async Task PostPropertyRating_NotAlquilerTemporario_Returns400()
    {
        var (_, listingId, _) = await SetupApprovedListingAsync(VentaListing());
        var (userClient, _) = await CreateConsultaAsync(listingId);

        var resp = await userClient.PostAsJsonAsync("/api/ratings/property",
            new RatePropertyRequest(listingId, 5, 5, 5, null));

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task PostPropertyRating_NoConsulta_Returns403()
    {
        var (_, listingId, _) = await SetupApprovedListingAsync(AlquilerTemporarioListing());
        var (userClient, _) = await TestAuthHelper.CreateAuthenticatedUserAsync(_factory);

        var resp = await userClient.PostAsJsonAsync("/api/ratings/property",
            new RatePropertyRequest(listingId, 5, 5, 5, null));

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task PostPropertyRating_SecondTime_UpdatesRating()
    {
        var (_, listingId, _) = await SetupApprovedListingAsync(AlquilerTemporarioListing());
        var (userClient, _) = await CreateConsultaAsync(listingId);

        await userClient.PostAsJsonAsync("/api/ratings/property",
            new RatePropertyRequest(listingId, 1, 1, 1, null));
        var resp2 = await userClient.PostAsJsonAsync("/api/ratings/property",
            new RatePropertyRequest(listingId, 5, 5, 5, null));

        Assert.Equal(HttpStatusCode.OK, resp2.StatusCode);
        var stats = await resp2.Content.ReadFromJsonAsync<PropertyRatingStatsDto>();
        Assert.Equal(1, stats!.TotalValoraciones);
        Assert.Equal(5.0, stats.PromedioGeneral, 1);
    }

    [Fact]
    public async Task GetPropertyStats_ReturnsAverages()
    {
        var (_, listingId, _) = await SetupApprovedListingAsync(AlquilerTemporarioListing());
        var (userClient, _) = await CreateConsultaAsync(listingId);
        await userClient.PostAsJsonAsync("/api/ratings/property",
            new RatePropertyRequest(listingId, 4, 3, 5, null));

        var resp = await _factory.CreateClient().GetAsync($"/api/ratings/property/{listingId}/stats");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var stats = await resp.Content.ReadFromJsonAsync<PropertyRatingStatsDto>();
        Assert.NotNull(stats);
        Assert.Equal(1, stats!.TotalValoraciones);
        Assert.InRange(stats.PromedioGeneral, 3.9, 4.1);
    }

    [Fact]
    public async Task PostAgentRating_WithConsulta_ReturnsStats()
    {
        var (_, listingId, publisherId) = await SetupApprovedListingAsync(AlquilerTemporarioListing());
        var (userClient, _) = await CreateConsultaAsync(listingId);

        var resp = await userClient.PostAsJsonAsync("/api/ratings/agent",
            new RateAgentRequest(publisherId, 5, 5, 4, 3, "Muy bueno"));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var stats = await resp.Content.ReadFromJsonAsync<AgentRatingStatsDto>();
        Assert.NotNull(stats);
        Assert.Equal(1, stats!.TotalValoraciones);
        Assert.Equal(5.0, stats.PromedioAtencion);
        Assert.InRange(stats.PromedioGeneral, 4.2, 4.3);
    }

    [Fact]
    public async Task PostAgentRating_NoConsulta_Returns403()
    {
        var (_, listingId, publisherId) = await SetupApprovedListingAsync(AlquilerTemporarioListing());
        var (userClient, _) = await TestAuthHelper.CreateAuthenticatedUserAsync(_factory);

        var resp = await userClient.PostAsJsonAsync("/api/ratings/agent",
            new RateAgentRequest(publisherId, 5, 5, 5, 5, null));

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task GetRanking_AfterAgentRating_ReturnsPublisher()
    {
        var (_, listingId, publisherId) = await SetupApprovedListingAsync(AlquilerTemporarioListing());
        var (userClient, _) = await CreateConsultaAsync(listingId);
        await userClient.PostAsJsonAsync("/api/ratings/agent",
            new RateAgentRequest(publisherId, 5, 5, 5, 5, null));

        var resp = await _factory.CreateClient().GetAsync("/api/ratings/ranking");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var ranking = await resp.Content.ReadFromJsonAsync<List<AgentRankingItemDto>>();
        Assert.NotNull(ranking);
        Assert.Contains(ranking!, r => r.PublisherId == publisherId);
    }
}

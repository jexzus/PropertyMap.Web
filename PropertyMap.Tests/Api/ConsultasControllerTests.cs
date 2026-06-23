using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using PropertyMap.Core.DTOs.Consultas;
using PropertyMap.Core.DTOs.Properties;
using PropertyMap.Core.Enums;
using Xunit;

namespace PropertyMap.Tests.Api;

public class ConsultasControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public ConsultasControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private static CreateListingRequest SampleListing() => new(
        Operacion: TipoOperacion.Venta,
        TipoPropiedad: TipoPropiedad.Departamento,
        Titulo: "Propiedad consulta test",
        Descripcion: "Test",
        Precio: 80000,
        Moneda: "USD",
        DireccionTexto: "Av. Test 123",
        Ciudad: "Buenos Aires",
        Provincia: "Buenos Aires",
        Lat: -34.60,
        Lng: -58.38,
        Superficie: null,
        SuperficieCubierta: null,
        Ambientes: null,
        Dormitorios: null,
        Banos: null,
        Antiguedad: null,
        Cochera: false,
        Amenities: []);

    private record CreatedIdDto(int Id);

    private async Task<(HttpClient pubClient, string pubUserId, int listingId)> SetupPublishedListingAsync()
    {
        var (pubClient, pubUserId) = await TestAuthHelper.CreateAuthenticatedPublisherAsync(_factory);
        await TestAuthHelper.CreatePublisherProfileAsync(pubClient);
        var createResp = await pubClient.PostAsJsonAsync("/api/properties", SampleListing());
        var created = await createResp.Content.ReadFromJsonAsync<CreatedIdDto>();

        // approve via admin
        using var adminScope = _factory.Services.CreateScope();
        var adminMgr = adminScope.ServiceProvider
            .GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<PropertyMap.Core.Entities.ApplicationUser>>();
        var adminEmail = $"admin_{Guid.NewGuid()}@test.com";
        var adminUser = new PropertyMap.Core.Entities.ApplicationUser
        {
            UserName = adminEmail, Email = adminEmail,
            Nombre = "Admin", Apellido = "Test",
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

        return (pubClient, pubUserId, created.Id);
    }

    [Fact]
    public async Task PostConsulta_CreatesThreadAndFirstMessage()
    {
        var (_, _, listingId) = await SetupPublishedListingAsync();
        var (userClient, _) = await TestAuthHelper.CreateAuthenticatedUserAsync(_factory);

        var resp = await userClient.PostAsJsonAsync("/api/consultas",
            new CreateConsultaRequest(listingId, "Hola, ¿sigue disponible?"));

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var detail = await resp.Content.ReadFromJsonAsync<ConsultaDetailDto>();
        Assert.NotNull(detail);
        Assert.Single(detail!.Mensajes);
        Assert.Equal("Hola, ¿sigue disponible?", detail.Mensajes[0].Mensaje);
        Assert.False(detail.Mensajes[0].EsDelPublisher);
    }

    [Fact]
    public async Task PostConsulta_SameListingId_ReturnsSameThreadWithNewMessage()
    {
        var (_, _, listingId) = await SetupPublishedListingAsync();
        var (userClient, _) = await TestAuthHelper.CreateAuthenticatedUserAsync(_factory);

        var resp1 = await userClient.PostAsJsonAsync("/api/consultas",
            new CreateConsultaRequest(listingId, "Primer mensaje"));
        var detail1 = await resp1.Content.ReadFromJsonAsync<ConsultaDetailDto>();

        var resp2 = await userClient.PostAsJsonAsync("/api/consultas",
            new CreateConsultaRequest(listingId, "Segundo mensaje"));
        var detail2 = await resp2.Content.ReadFromJsonAsync<ConsultaDetailDto>();

        Assert.Equal(detail1!.Id, detail2!.Id);
        Assert.Equal(2, detail2.Mensajes.Count);
    }

    [Fact]
    public async Task GetConsultaDetail_ThirdParty_ReturnsForbid()
    {
        var (_, _, listingId) = await SetupPublishedListingAsync();
        var (userClient, _) = await TestAuthHelper.CreateAuthenticatedUserAsync(_factory);

        var postResp = await userClient.PostAsJsonAsync("/api/consultas",
            new CreateConsultaRequest(listingId, "Consulta privada"));
        var detail = await postResp.Content.ReadFromJsonAsync<ConsultaDetailDto>();

        // A different user tries to access the thread
        var (otherClient, _) = await TestAuthHelper.CreateAuthenticatedUserAsync(_factory);
        var getResp = await otherClient.GetAsync($"/api/consultas/{detail!.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, getResp.StatusCode);
    }

    [Fact]
    public async Task PublisherReply_AddsMessageToThread()
    {
        var (pubClient, _, listingId) = await SetupPublishedListingAsync();
        var (userClient, _) = await TestAuthHelper.CreateAuthenticatedUserAsync(_factory);

        var postResp = await userClient.PostAsJsonAsync("/api/consultas",
            new CreateConsultaRequest(listingId, "Quiero saber el precio"));
        var detail = await postResp.Content.ReadFromJsonAsync<ConsultaDetailDto>();

        var replyResp = await pubClient.PostAsJsonAsync(
            $"/api/consultas/{detail!.Id}/mensajes",
            new SendMensajeRequest("El precio es USD 80.000"));

        Assert.Equal(HttpStatusCode.OK, replyResp.StatusCode);
        var msgDto = await replyResp.Content.ReadFromJsonAsync<ConsultaMensajeDto>();
        Assert.NotNull(msgDto);
        Assert.True(msgDto!.EsDelPublisher);
        Assert.Equal("El precio es USD 80.000", msgDto.Mensaje);
    }

    [Fact]
    public async Task RegularUser_CannotUsePublisherEndpoint()
    {
        var (_, _, listingId) = await SetupPublishedListingAsync();
        var (userClient, _) = await TestAuthHelper.CreateAuthenticatedUserAsync(_factory);

        var postResp = await userClient.PostAsJsonAsync("/api/consultas",
            new CreateConsultaRequest(listingId, "Consulta inicial"));
        var detail = await postResp.Content.ReadFromJsonAsync<ConsultaDetailDto>();

        // Regular user tries to use the publisher reply endpoint
        var replyResp = await userClient.PostAsJsonAsync(
            $"/api/consultas/{detail!.Id}/mensajes",
            new SendMensajeRequest("Intento ilegítimo"));

        Assert.Equal(HttpStatusCode.Forbidden, replyResp.StatusCode);
    }

    [Fact]
    public async Task PublisherReply_WrongPublisher_ReturnsForbid()
    {
        var (_, _, listingId) = await SetupPublishedListingAsync();
        var (userClient, _) = await TestAuthHelper.CreateAuthenticatedUserAsync(_factory);
        var postResp = await userClient.PostAsJsonAsync("/api/consultas",
            new CreateConsultaRequest(listingId, "Consulta"));
        var detail = await postResp.Content.ReadFromJsonAsync<ConsultaDetailDto>();

        // A different publisher — not the owner of this listing
        var (otherPubClient, _) = await TestAuthHelper.CreateAuthenticatedPublisherAsync(_factory);
        await TestAuthHelper.CreatePublisherProfileAsync(otherPubClient);
        var replyResp = await otherPubClient.PostAsJsonAsync(
            $"/api/consultas/{detail!.Id}/mensajes",
            new SendMensajeRequest("Respuesta no autorizada"));

        Assert.Equal(HttpStatusCode.Forbidden, replyResp.StatusCode);
    }

    [Fact]
    public async Task GetMyConsultas_ReturnsThreadsOrderedByLatestMessage()
    {
        var (_, _, listingId) = await SetupPublishedListingAsync();
        var (userClient, _) = await TestAuthHelper.CreateAuthenticatedUserAsync(_factory);

        // Create a thread with two messages
        await userClient.PostAsJsonAsync("/api/consultas",
            new CreateConsultaRequest(listingId, "Primer mensaje"));
        await userClient.PostAsJsonAsync("/api/consultas",
            new CreateConsultaRequest(listingId, "Segundo mensaje"));

        var resp = await userClient.GetAsync("/api/consultas");
        Assert.Equal(System.Net.HttpStatusCode.OK, resp.StatusCode);
        var list = await resp.Content.ReadFromJsonAsync<List<ConsultaSummaryDto>>();
        Assert.NotNull(list);
        Assert.True(list!.Count >= 1);
        // Most recent message is the last preview
        Assert.Equal("Segundo mensaje", list[0].UltimoMensaje);
        Assert.False(list[0].UltimoEsDelPublisher);
    }

    [Fact]
    public async Task GetPublisherConsultas_ReturnsReceivedThreads()
    {
        var (pubClient, _, listingId) = await SetupPublishedListingAsync();
        var (userClient, _) = await TestAuthHelper.CreateAuthenticatedUserAsync(_factory);

        await userClient.PostAsJsonAsync("/api/consultas",
            new CreateConsultaRequest(listingId, "Consulta para el publisher"));

        var resp = await pubClient.GetAsync("/api/consultas/publisher");
        Assert.Equal(System.Net.HttpStatusCode.OK, resp.StatusCode);
        var list = await resp.Content.ReadFromJsonAsync<List<ConsultaSummaryDto>>();
        Assert.NotNull(list);
        Assert.True(list!.Count >= 1);
        Assert.Equal("Consulta para el publisher", list[0].UltimoMensaje);
    }
}

using System.Net;
using System.Net.Http.Json;
using PropertyMap.Core.DTOs.Alerts;
using PropertyMap.Core.Enums;
using Xunit;

namespace PropertyMap.Tests.Api;

public class AlertsControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public AlertsControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreateAlert_ThenGetMine_ReturnsIt()
    {
        var (client, _) = await TestAuthHelper.CreateAuthenticatedUserAsync(_factory);

        var createResp = await client.PostAsJsonAsync("/api/alerts",
            new CreateAlertRequest("Depto en Córdoba", TipoOperacion.Alquiler, TipoPropiedad.Departamento,
                "Córdoba", 100000, "ARS", 2));
        Assert.Equal(HttpStatusCode.Created, createResp.StatusCode);

        var listResp = await client.GetAsync("/api/alerts");
        var alerts = await listResp.Content.ReadFromJsonAsync<List<AlertDto>>();

        Assert.NotNull(alerts);
        Assert.Single(alerts!);
        Assert.Equal("Depto en Córdoba", alerts![0].Nombre);
        Assert.True(alerts[0].Activa);
    }

    [Fact]
    public async Task ToggleAlert_FlipsActiva()
    {
        var (client, _) = await TestAuthHelper.CreateAuthenticatedUserAsync(_factory);
        await client.PostAsJsonAsync("/api/alerts",
            new CreateAlertRequest(null, null, null, null, null, null, null));
        var alerts = await (await client.GetAsync("/api/alerts")).Content.ReadFromJsonAsync<List<AlertDto>>();
        var id = alerts![0].Id;

        var toggleResp = await client.PatchAsync($"/api/alerts/{id}/toggle", null);
        Assert.Equal(HttpStatusCode.NoContent, toggleResp.StatusCode);

        var after = await (await client.GetAsync("/api/alerts")).Content.ReadFromJsonAsync<List<AlertDto>>();
        Assert.False(after![0].Activa);
    }

    [Fact]
    public async Task DeleteAlert_RemovesFromList()
    {
        var (client, _) = await TestAuthHelper.CreateAuthenticatedUserAsync(_factory);
        await client.PostAsJsonAsync("/api/alerts",
            new CreateAlertRequest(null, null, null, null, null, null, null));
        var alerts = await (await client.GetAsync("/api/alerts")).Content.ReadFromJsonAsync<List<AlertDto>>();
        var id = alerts![0].Id;

        var deleteResp = await client.DeleteAsync($"/api/alerts/{id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResp.StatusCode);

        var after = await (await client.GetAsync("/api/alerts")).Content.ReadFromJsonAsync<List<AlertDto>>();
        Assert.Empty(after!);
    }
}

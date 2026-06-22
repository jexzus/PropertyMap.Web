using System.Net;
using System.Net.Http.Json;
using PropertyMap.Core.DTOs.Publisher;
using PropertyMap.Core.Enums;
using Xunit;

namespace PropertyMap.Tests.Api;

public class PublisherControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public PublisherControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetProfile_WithoutAuth_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/publisher/profile");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetProfile_WithoutProfile_Returns404()
    {
        var (client, _) = await TestAuthHelper.CreateAuthenticatedPublisherAsync(_factory);
        var response = await client.GetAsync("/api/publisher/profile");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CreateProfile_ValidData_Returns201WithProfile()
    {
        var (client, _) = await TestAuthHelper.CreateAuthenticatedPublisherAsync(_factory);
        var response = await client.PostAsJsonAsync("/api/publisher/profile",
            new PublisherProfileRequest("Test Inmobiliaria", "+54 9 11 1234-5678", TipoPublicador.Inmobiliaria));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var profile = await response.Content.ReadFromJsonAsync<PublisherProfileResponse>();
        Assert.Equal("Test Inmobiliaria", profile!.Nombre);
        Assert.Equal(TipoPublicador.Inmobiliaria, profile.Tipo);
    }

    [Fact]
    public async Task CreateProfile_Duplicate_Returns409()
    {
        var (client, _) = await TestAuthHelper.CreateAuthenticatedPublisherAsync(_factory);
        var req = new PublisherProfileRequest("Inmobiliaria X", "+54 9 11 0000-0000", TipoPublicador.Particular);
        await client.PostAsJsonAsync("/api/publisher/profile", req);
        var response = await client.PostAsJsonAsync("/api/publisher/profile", req);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task GetProfile_AfterCreate_ReturnsProfile()
    {
        var (client, _) = await TestAuthHelper.CreateAuthenticatedPublisherAsync(_factory);
        await client.PostAsJsonAsync("/api/publisher/profile",
            new PublisherProfileRequest("Mi Inmobiliaria", "+54 9 11 9999-9999", TipoPublicador.Inmobiliaria));

        var response = await client.GetAsync("/api/publisher/profile");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var profile = await response.Content.ReadFromJsonAsync<PublisherProfileResponse>();
        Assert.Equal("Mi Inmobiliaria", profile!.Nombre);
        Assert.Equal(0, profile.TotalPublicaciones);
    }
}

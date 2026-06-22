using System.Net;
using System.Net.Http.Json;
using PropertyMap.Core.DTOs.User;
using Xunit;

namespace PropertyMap.Tests.Api;

public class UserControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public UserControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetProfile_WithoutAuth_Returns401()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync("/api/user/profile");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task GetProfile_Authenticated_ReturnsProfileData()
    {
        var (client, _) = await TestAuthHelper.CreateAuthenticatedPublisherAsync(_factory);
        var resp = await client.GetAsync("/api/user/profile");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var profile = await resp.Content.ReadFromJsonAsync<UserProfileResponse>();
        Assert.NotNull(profile);
        Assert.Equal("Test", profile.Nombre);
        Assert.Equal("Publisher", profile.Apellido);
    }

    [Fact]
    public async Task UpdateProfile_ChangesNombreApellido()
    {
        var (client, _) = await TestAuthHelper.CreateAuthenticatedPublisherAsync(_factory);
        var resp = await client.PutAsJsonAsync("/api/user/profile",
            new UpdateProfileRequest("Nuevo", "Apellido"));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);

        var getResp = await client.GetAsync("/api/user/profile");
        var profile = await getResp.Content.ReadFromJsonAsync<UserProfileResponse>();
        Assert.Equal("Nuevo", profile!.Nombre);
        Assert.Equal("Apellido", profile.Apellido);
    }

    [Fact]
    public async Task UploadAvatar_ValidImage_ReturnsAvatarUrl()
    {
        var (client, _) = await TestAuthHelper.CreateAuthenticatedPublisherAsync(_factory);

        using var form = new MultipartFormDataContent();
        var imgBytes = new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }; // JPEG magic bytes
        var fileContent = new ByteArrayContent(imgBytes);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
        form.Add(fileContent, "file", "test.jpg");

        var resp = await client.PostAsync("/api/user/avatar", form);
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<AvatarUrlResponse>();
        Assert.NotNull(body?.AvatarUrl);
        Assert.Contains("/uploads/avatars/", body.AvatarUrl);
    }

    private record AvatarUrlResponse(string AvatarUrl);
}

using System.Net;
using System.Net.Http.Json;
using PropertyMap.Core.DTOs.Auth;
using Xunit;

namespace PropertyMap.Tests.Api;

public class SecurityTests : IClassFixture<RateLimitTestWebApplicationFactory>
{
    private readonly RateLimitTestWebApplicationFactory _factory;

    public SecurityTests(RateLimitTestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Login_ExceedsAuthRateLimit_Returns429()
    {
        var client = _factory.CreateClient();
        var request = new LoginRequest("nadie@example.com", "Wrong123!");

        HttpResponseMessage? lastResponse = null;
        for (var i = 0; i < 6; i++)
        {
            lastResponse = await client.PostAsJsonAsync("/api/auth/login", request);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, lastResponse!.StatusCode);
    }
}

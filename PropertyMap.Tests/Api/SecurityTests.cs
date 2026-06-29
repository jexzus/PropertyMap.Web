using System.Net;
using System.Net.Http.Json;
using PropertyMap.Core.DTOs.Auth;
using Xunit;

namespace PropertyMap.Tests.Api;

// ADVERTENCIA: la política "auth" particiona el límite por IP del cliente
// (ctx.Connection.RemoteIpAddress). Todos los tests de integración de esta
// clase corren contra el mismo TestServer dentro del mismo IClassFixture,
// por lo que comparten la misma IP simulada y, por lo tanto, el mismo balde
// (bucket) del rate limiter. Si en el futuro se agrega otro [Fact] que
// también golpee endpoints de AuthController, podría agotar ese balde
// compartido y provocar fallos intermitentes según el orden de ejecución
// de los tests.
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

        // Esta prueba valida el límite por partición/IP: con el fix, la política
        // "auth" particiona por ctx.Connection.RemoteIpAddress, pero todas las
        // requests de este test salen del mismo HttpClient/TestServer, así que
        // comparten la misma IP simulada y caen en el mismo balde (bucket).
        // Por eso el test sigue siendo válido tal cual, sin cambios de lógica.
        HttpResponseMessage? lastResponse = null;
        for (var i = 0; i < 6; i++)
        {
            lastResponse = await client.PostAsJsonAsync("/api/auth/login", request);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, lastResponse!.StatusCode);
    }
}

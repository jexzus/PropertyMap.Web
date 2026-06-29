using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using PropertyMap.Core.DTOs.Auth;
using PropertyMap.Core.Entities;
using PropertyMap.Core.Enums;
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

public class LockoutTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;

    public LockoutTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<string> CreateVerifiedUserAsync(HttpClient client)
    {
        var email = $"lockout_{Guid.NewGuid()}@test.com";
        await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("Lock", "Out", email, "Test123!", "Test123!"));

        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        user!.EmailConfirmed = true;
        user.Estado = EstadoUsuario.Activo;
        await userManager.UpdateAsync(user);

        return email;
    }

    [Fact]
    public async Task Login_ThreeFailedAttempts_ThenLocksOut()
    {
        var client = _factory.CreateClient();
        var email = await CreateVerifiedUserAsync(client);

        for (var i = 0; i < 3; i++)
        {
            var failResp = await client.PostAsJsonAsync("/api/auth/login",
                new LoginRequest(email, "WrongPassword1!"));
            Assert.Equal(HttpStatusCode.Unauthorized, failResp.StatusCode);
        }

        var lockedResp = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(email, "Test123!"));

        Assert.Equal(423, (int)lockedResp.StatusCode);
    }

    [Fact]
    public async Task Login_SuccessfulLogin_ResetsFailedCount()
    {
        var client = _factory.CreateClient();
        var email = await CreateVerifiedUserAsync(client);

        await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "WrongPassword1!"));
        await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "WrongPassword1!"));

        var successResp = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "Test123!"));
        Assert.Equal(HttpStatusCode.OK, successResp.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        var failedCount = await userManager.GetAccessFailedCountAsync(user!);
        Assert.Equal(0, failedCount);
    }
}

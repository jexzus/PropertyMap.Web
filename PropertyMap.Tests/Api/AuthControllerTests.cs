using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using PropertyMap.Core.DTOs.Auth;
using PropertyMap.Core.Entities;
using PropertyMap.Core.Enums;
using Xunit;

namespace PropertyMap.Tests.Api;

public class AuthControllerTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly TestWebApplicationFactory _factory;

    public AuthControllerTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Register_ValidData_Returns200WithMessage()
    {
        var request = new RegisterRequest("Juan", "Pérez", $"test_{Guid.NewGuid()}@example.com", "Test123!", "Test123!");
        var response = await _client.PostAsJsonAsync("/api/auth/register", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<MessageResponse>();
        Assert.Contains("Revisá tu email", body!.Message);
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns409()
    {
        var email = $"dup_{Guid.NewGuid()}@example.com";
        var request = new RegisterRequest("A", "B", email, "Test123!", "Test123!");
        await _client.PostAsJsonAsync("/api/auth/register", request);
        var response = await _client.PostAsJsonAsync("/api/auth/register", request);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Register_PasswordMismatch_Returns400()
    {
        var request = new RegisterRequest("A", "B", $"mismatch_{Guid.NewGuid()}@example.com", "Test123!", "Different123!");
        var response = await _client.PostAsJsonAsync("/api/auth/register", request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_UnverifiedEmail_Returns401()
    {
        var email = $"unverified_{Guid.NewGuid()}@example.com";
        await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest("A", "B", email, "Test123!", "Test123!"));
        var response = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "Test123!"));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_VerifiedUser_ReturnsTokens()
    {
        var email = $"verified_{Guid.NewGuid()}@example.com";
        await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest("Ana", "García", email, "Test123!", "Test123!"));

        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        user!.EmailConfirmed = true;
        user.Estado = EstadoUsuario.Activo;
        user.EmailVerificationToken = null;
        await userManager.UpdateAsync(user);

        var response = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "Test123!"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotEmpty(auth!.AccessToken);
        Assert.NotEmpty(auth.RefreshToken);
        Assert.Contains("User", auth.Roles);
    }

    [Fact]
    public async Task VerifyEmail_InvalidToken_Returns400()
    {
        var email = $"verify_{Guid.NewGuid()}@example.com";
        await _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest("A", "B", email, "Test123!", "Test123!"));
        var response = await _client.PostAsJsonAsync("/api/auth/verify-email", new VerifyEmailRequest(email, "000000"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ForgotPassword_UnknownEmail_Returns200WithSafeMessage()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/forgot-password",
            new ForgotPasswordRequest("nobody@example.com"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<MessageResponse>();
        Assert.Contains("Si el email existe", body!.Message);
    }

    [Fact]
    public async Task GetListings_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/listings");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

public record MessageResponse(string Message);

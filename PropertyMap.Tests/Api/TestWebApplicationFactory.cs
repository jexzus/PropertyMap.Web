using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PropertyMap.Core.Interfaces;
using PropertyMap.Infrastructure.Data;
using System.Net.Http.Json;

namespace PropertyMap.Tests.Api;

public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = $"TestDb_{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove all EF Core descriptors (including SqlServer internal services)
            var efDescriptors = services
                .Where(d => d.ServiceType.FullName != null &&
                            (d.ServiceType.FullName.StartsWith("Microsoft.EntityFrameworkCore") ||
                             d.ServiceType == typeof(DbContextOptions<AppDbContext>) ||
                             d.ServiceType == typeof(DbContextOptions) ||
                             d.ServiceType == typeof(AppDbContext)))
                .ToList();
            foreach (var d in efDescriptors) services.Remove(d);

            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase(_dbName));

            var emailDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IEmailService));
            if (emailDescriptor != null) services.Remove(emailDescriptor);
            services.AddScoped<IEmailService, NoOpEmailService>();
        });

        builder.UseEnvironment("Testing");
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        // Ensure roles are seeded into the InMemory database
        using var scope = host.Services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in new[] { "Admin", "Publisher", "User" })
        {
            if (!roleManager.RoleExistsAsync(role).GetAwaiter().GetResult())
                roleManager.CreateAsync(new IdentityRole(role)).GetAwaiter().GetResult();
        }

        return host;
    }
}

public class NoOpEmailService : IEmailService
{
    public Task SendEmailVerificationAsync(string toEmail, string toName, string token) => Task.CompletedTask;
    public Task SendPasswordResetAsync(string toEmail, string toName, string token, string resetUrl) => Task.CompletedTask;
    public Task SendWelcomeAsync(string toEmail, string toName) => Task.CompletedTask;
    public Task SendNuevaConsultaAsync(string toEmail, string publisherNombre, string propertyTitulo, string userNombre, string mensaje) => Task.CompletedTask;
    public Task SendNuevaRespuestaAsync(string toEmail, string userNombre, string propertyTitulo, string publisherNombre, string mensaje) => Task.CompletedTask;
}

public static class TestAuthHelper
{
    public static async Task<(HttpClient client, string userId)> CreateAuthenticatedPublisherAsync(
        TestWebApplicationFactory factory)
    {
        var client = factory.CreateClient();
        var email = $"pub_{Guid.NewGuid()}@test.com";

        // Registrar usuario
        await client.PostAsJsonAsync("/api/auth/register",
            new PropertyMap.Core.DTOs.Auth.RegisterRequest("Test", "Publisher", email, "Test123!", "Test123!"));

        // Confirmar email directamente via UserManager
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<PropertyMap.Core.Entities.ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        user!.EmailConfirmed = true;
        user.Estado = PropertyMap.Core.Enums.EstadoUsuario.Activo;
        await userManager.UpdateAsync(user);

        // Asignar rol Publisher
        await userManager.AddToRoleAsync(user, "Publisher");

        // Login
        var loginResp = await client.PostAsJsonAsync("/api/auth/login",
            new PropertyMap.Core.DTOs.Auth.LoginRequest(email, "Test123!"));
        var auth = await loginResp.Content.ReadFromJsonAsync<PropertyMap.Core.DTOs.Auth.AuthResponse>();

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        return (client, user.Id);
    }

    public static async Task<(HttpClient client, string userId)> CreateAuthenticatedUserAsync(
        TestWebApplicationFactory factory)
    {
        var client = factory.CreateClient();
        var email = $"user_{Guid.NewGuid()}@test.com";

        await client.PostAsJsonAsync("/api/auth/register",
            new PropertyMap.Core.DTOs.Auth.RegisterRequest("Test", "User", email, "Test123!", "Test123!"));

        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<PropertyMap.Core.Entities.ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        user!.EmailConfirmed = true;
        user.Estado = PropertyMap.Core.Enums.EstadoUsuario.Activo;
        await userManager.UpdateAsync(user);

        // No Publisher role — plain user
        var loginResp = await client.PostAsJsonAsync("/api/auth/login",
            new PropertyMap.Core.DTOs.Auth.LoginRequest(email, "Test123!"));
        var auth = await loginResp.Content.ReadFromJsonAsync<PropertyMap.Core.DTOs.Auth.AuthResponse>();

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth!.AccessToken);

        return (client, user.Id);
    }

    public static async Task<int> CreatePublisherProfileAsync(HttpClient client, string nombre = "Test Inmobiliaria")
    {
        var resp = await client.PostAsJsonAsync("/api/publisher/profile",
            new PropertyMap.Core.DTOs.Publisher.PublisherProfileRequest(
                nombre,
                "+54 9 11 1234-5678",
                PropertyMap.Core.Enums.TipoPublicador.Inmobiliaria));
        var body = await resp.Content.ReadFromJsonAsync<PropertyMap.Core.DTOs.Publisher.PublisherProfileResponse>();
        return body!.Id;
    }
}

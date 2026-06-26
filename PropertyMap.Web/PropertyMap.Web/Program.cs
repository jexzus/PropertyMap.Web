using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PropertyMap.Core.Entities;
using PropertyMap.Core.Interfaces;
using PropertyMap.Infrastructure.Data;
using PropertyMap.Infrastructure.Repositories;
using PropertyMap.Web.Auth;
using PropertyMap.Web.Components;
using PropertyMap.Web.Services;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString, sql =>
            sql.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(5),
                errorNumbersToAdd: null))
        .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning)));

// Identity solo para UserManager<ApplicationUser> (necesario para DbSeeder).
// La auth de Blazor va por BlazorAuthStateProvider + JWT, NO por cookies de Identity.
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    options.SignIn.RequireConfirmedAccount = false)
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

// Cliente HTTP nombrado para la API
builder.Services.AddHttpClient("api", client =>
    client.BaseAddress = new Uri(
        builder.Configuration["ApiSettings:BaseUrl"] ?? "https://localhost:7002/"));

// También mantener el cliente tipado de listados (usado por Home.razor y PropertyDetail.razor)
builder.Services.AddHttpClient<IListingApiService, ListingApiService>(client =>
    client.BaseAddress = new Uri(
        builder.Configuration["ApiSettings:BaseUrl"] ?? "https://localhost:7002/"));

// Auth JWT para Blazor
builder.Services.AddScoped<MemoryTokenStore>();
builder.Services.AddScoped<BlazorAuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp =>
    sp.GetRequiredService<BlazorAuthStateProvider>());

// Servicios de la app
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IPropertyApiService, PropertyApiService>();
builder.Services.AddScoped<IUserApiService, UserApiService>();
builder.Services.AddScoped<IFavoritesApiService, FavoritesApiService>();
builder.Services.AddScoped<IConsultasApiService, ConsultasApiService>();
builder.Services.AddScoped<IRatingsApiService, RatingsApiService>();
builder.Services.AddScoped<IAlertsApiService, AlertsApiService>();
builder.Services.AddScoped<INotificationsApiService, NotificationsApiService>();
builder.Services.AddScoped<IReportsApiService, ReportsApiService>();
builder.Services.AddScoped<NotificationHubClient>();

// Repos (aún necesarios mientras Program.cs corre DbSeeder)
builder.Services.AddScoped<IListingRepository, ListingRepository>();
builder.Services.AddScoped<IPublisherRepository, PublisherRepository>();
builder.Services.AddScoped<ILocationRepository, LocationRepository>();

builder.Services.AddCascadingAuthenticationState();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(PropertyMap.Web.Client._Imports).Assembly);

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    const int maxAttempts = 10;
    for (int attempt = 1; ; attempt++)
    {
        try
        {
            await db.Database.MigrateAsync();
            await PropertyMap.Infrastructure.Data.DbSeeder.SeedAsync(db, userManager);
            await PropertyMap.Infrastructure.Data.DbSeeder.SeedPlansAsync(db);
            break;
        }
        catch (Microsoft.Data.SqlClient.SqlException) when (attempt < maxAttempts)
        {
            app.Logger.LogWarning(
                "LocalDB todavía no disponible (intento {Attempt}/{Max}); reintentando en 2s…",
                attempt, maxAttempts);
            await Task.Delay(TimeSpan.FromSeconds(2));
        }
    }
}

app.Run();

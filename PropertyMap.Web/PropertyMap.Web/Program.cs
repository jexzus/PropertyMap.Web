using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using PropertyMap.Core.Interfaces;
using PropertyMap.Infrastructure.Data;
using PropertyMap.Infrastructure.Repositories;
using PropertyMap.Web.Components;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(connectionString, sql =>
            // Resiliencia ante fallos transitorios: LocalDB se apaga al estar
            // inactivo y, tras actualizar VS / SQL Server, puede tardar en levantar.
            // EF reintenta automáticamente las operaciones en esos casos.
            sql.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(5),
                errorNumbersToAdd: null))
        // Falso positivo de EF Core 9: el ValueComparer de PropertyListing.Fotos
        // no se serializa de forma idéntica en el snapshot, por lo que la
        // validación de Migrate() detecta "cambios pendientes" inexistentes.
        // El esquema coincide con el modelo, así que ignoramos solo esta advertencia.
        .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning)));

builder.Services.AddDefaultIdentity<IdentityUser>(options =>
    options.SignIn.RequireConfirmedAccount = false)
    .AddEntityFrameworkStores<AppDbContext>();

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
// Sirve archivos subidos en runtime (wwwroot/uploads). MapStaticAssets solo
// sirve los assets conocidos en tiempo de compilación, no los subidos por el usuario.
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(PropertyMap.Web.Client._Imports).Assembly);

// Migración + seed con reintentos. LocalDB puede fallar al arrancar en frío
// ("SQL Server process failed to start"), sobre todo tras actualizar Visual
// Studio / SQL Server. Ese error no está en la lista transitoria por defecto de
// EF, así que reintentamos manualmente para no caernos durante el arranque.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    const int maxAttempts = 10;
    for (int attempt = 1; ; attempt++)
    {
        try
        {
            await db.Database.MigrateAsync();
            await PropertyMap.Infrastructure.Data.DbSeeder.SeedAsync(db);
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
    
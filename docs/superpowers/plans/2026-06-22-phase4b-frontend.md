# PropertyMap Phase 4B — Frontend: Auth Bridge, Wizard API, Publisher Dashboard

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Conectar Blazor a la API REST: auth JWT (login/register), wizard de publicación consumiendo la API, y dashboard del publisher.

**Architecture:** Un `BlazorAuthStateProvider` custom reemplaza `AddDefaultIdentity<IdentityUser>`. Tokens JWT se guardan en un `MemoryTokenStore` (scoped por circuit) y se persisten en `ProtectedLocalStorage`. Los servicios `AuthService` y `PropertyApiService` usan `IHttpClientFactory` con un cliente nombrado "api". El wizard reemplaza las inyecciones directas de repos por `IPropertyApiService`. El dashboard es una nueva página `@rendermode InteractiveServer`.

**Tech Stack:** Blazor Web App Server (.NET 9), `ProtectedLocalStorage`, `IHttpClientFactory`, `AuthenticationStateProvider`, `System.Security.Claims`.

---

## File Map

### Created
```
PropertyMap.Web/PropertyMap.Web/Services/MemoryTokenStore.cs
PropertyMap.Web/PropertyMap.Web/Services/IAuthService.cs
PropertyMap.Web/PropertyMap.Web/Services/AuthService.cs
PropertyMap.Web/PropertyMap.Web/Auth/BlazorAuthStateProvider.cs
PropertyMap.Web/PropertyMap.Web/Services/IPropertyApiService.cs
PropertyMap.Web/PropertyMap.Web/Services/PropertyApiService.cs
PropertyMap.Web/PropertyMap.Web/Components/Pages/Account/Login.razor
PropertyMap.Web/PropertyMap.Web/Components/Pages/Account/Register.razor
PropertyMap.Web/PropertyMap.Web/Components/Pages/Publisher/Dashboard.razor
```

### Modified
```
PropertyMap.Web/PropertyMap.Web/Program.cs
PropertyMap.Web/PropertyMap.Web/Components/Pages/PublishProperty.razor
```

---

## Task 1: MemoryTokenStore + IAuthService + AuthService

**Files:**
- Create: `PropertyMap.Web/PropertyMap.Web/Services/MemoryTokenStore.cs`
- Create: `PropertyMap.Web/PropertyMap.Web/Services/IAuthService.cs`
- Create: `PropertyMap.Web/PropertyMap.Web/Services/AuthService.cs`

Paths base: `C:\Agentes\PropertyMap\src\PropertyMap.Web\PropertyMap.Web\Services\`

- [ ] **Step 1: Crear MemoryTokenStore**

`PropertyMap.Web/PropertyMap.Web/Services/MemoryTokenStore.cs`
```csharp
using PropertyMap.Core.DTOs.Auth;

namespace PropertyMap.Web.Services;

public class MemoryTokenStore
{
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? AccessTokenExpiry { get; set; }
    public string? UserId { get; set; }
    public string? Email { get; set; }
    public string? NombreCompleto { get; set; }
    public IList<string> Roles { get; set; } = [];

    public bool IsAuthenticated =>
        !string.IsNullOrEmpty(AccessToken) && AccessTokenExpiry > DateTime.UtcNow;

    public void SetFromResponse(AuthResponse r)
    {
        AccessToken = r.AccessToken;
        RefreshToken = r.RefreshToken;
        AccessTokenExpiry = r.AccessTokenExpiry;
        UserId = r.UserId;
        Email = r.Email;
        NombreCompleto = r.NombreCompleto;
        Roles = r.Roles;
    }

    public void Clear()
    {
        AccessToken = null;
        RefreshToken = null;
        AccessTokenExpiry = null;
        UserId = null;
        Email = null;
        NombreCompleto = null;
        Roles = [];
    }
}
```

- [ ] **Step 2: Crear IAuthService**

`PropertyMap.Web/PropertyMap.Web/Services/IAuthService.cs`
```csharp
namespace PropertyMap.Web.Services;

public interface IAuthService
{
    Task<(bool Success, string? Error)> LoginAsync(string email, string password);
    Task<(bool Success, string? Error)> RegisterAsync(
        string nombre, string apellido, string email, string password, string confirmPassword);
    Task LogoutAsync();
    Task<bool> TryRestoreSessionAsync();
}
```

- [ ] **Step 3: Crear AuthService**

`PropertyMap.Web/PropertyMap.Web/Services/AuthService.cs`
```csharp
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using PropertyMap.Core.DTOs.Auth;
using PropertyMap.Web.Auth;

namespace PropertyMap.Web.Services;

public class AuthService : IAuthService
{
    private const string StorageKey = "pm-auth";
    private readonly HttpClient _http;
    private readonly MemoryTokenStore _tokenStore;
    private readonly BlazorAuthStateProvider _authProvider;
    private readonly ProtectedLocalStorage _storage;

    public AuthService(
        IHttpClientFactory httpFactory,
        MemoryTokenStore tokenStore,
        BlazorAuthStateProvider authProvider,
        ProtectedLocalStorage storage)
    {
        _http = httpFactory.CreateClient("api");
        _tokenStore = tokenStore;
        _authProvider = authProvider;
        _storage = storage;
    }

    public async Task<(bool Success, string? Error)> LoginAsync(string email, string password)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("api/auth/login", new LoginRequest(email, password));
            if (resp.StatusCode == HttpStatusCode.Unauthorized || resp.StatusCode == HttpStatusCode.BadRequest)
            {
                var err = await resp.Content.ReadFromJsonAsync<ErrorDto>();
                return (false, err?.Message ?? "Credenciales incorrectas.");
            }
            resp.EnsureSuccessStatusCode();
            var auth = await resp.Content.ReadFromJsonAsync<AuthResponse>();
            if (auth is null) return (false, "Respuesta inesperada del servidor.");

            _tokenStore.SetFromResponse(auth);
            await PersistAsync(auth);
            _authProvider.NotifyStateChanged();
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, $"No se pudo conectar: {ex.Message}");
        }
    }

    public async Task<(bool Success, string? Error)> RegisterAsync(
        string nombre, string apellido, string email, string password, string confirmPassword)
    {
        try
        {
            var resp = await _http.PostAsJsonAsync("api/auth/register",
                new RegisterRequest(nombre, apellido, email, password, confirmPassword));
            if (!resp.IsSuccessStatusCode)
            {
                var err = await resp.Content.ReadFromJsonAsync<ErrorDto>();
                return (false, err?.Message ?? "Error al registrarse.");
            }
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, $"No se pudo conectar: {ex.Message}");
        }
    }

    public async Task LogoutAsync()
    {
        _tokenStore.Clear();
        try { await _storage.DeleteAsync(StorageKey); } catch { }
        _authProvider.NotifyStateChanged();
    }

    public async Task<bool> TryRestoreSessionAsync()
    {
        try
        {
            var result = await _storage.GetAsync<AuthResponse>(StorageKey);
            if (!result.Success || result.Value is null) return false;
            var stored = result.Value;
            if (stored.AccessTokenExpiry <= DateTime.UtcNow) return false;
            _tokenStore.SetFromResponse(stored);
            _authProvider.NotifyStateChanged();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task PersistAsync(AuthResponse auth)
    {
        try { await _storage.SetAsync(StorageKey, auth); } catch { }
    }

    private record ErrorDto(string Message);
}
```

- [ ] **Step 4: Build Web**

```bash
cd C:/Agentes/PropertyMap/src
dotnet build PropertyMap.Web/PropertyMap.Web/PropertyMap.Web.csproj
```
Expected: `Build succeeded` (hay warnings de package version, ignorar).

**NOTA**: El build va a fallar hasta el Task 2 porque `BlazorAuthStateProvider` aún no existe. Está OK — hacer el build de verificación al final de Task 2.

- [ ] **Step 5: Commit (archivos nuevos)**

```bash
cd C:/Agentes/PropertyMap/src
git add PropertyMap.Web/PropertyMap.Web/Services/MemoryTokenStore.cs \
        PropertyMap.Web/PropertyMap.Web/Services/IAuthService.cs \
        PropertyMap.Web/PropertyMap.Web/Services/AuthService.cs
git commit -m "feat(web): add MemoryTokenStore, IAuthService and AuthService for JWT auth"
```

---

## Task 2: BlazorAuthStateProvider + Program.cs refactor

**Files:**
- Create: `PropertyMap.Web/PropertyMap.Web/Auth/BlazorAuthStateProvider.cs`
- Modify: `PropertyMap.Web/PropertyMap.Web/Program.cs`

- [ ] **Step 1: Crear directorio Auth y BlazorAuthStateProvider**

```bash
mkdir C:/Agentes/PropertyMap/src/PropertyMap.Web/PropertyMap.Web/Auth
```

`PropertyMap.Web/PropertyMap.Web/Auth/BlazorAuthStateProvider.cs`
```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using PropertyMap.Web.Services;

namespace PropertyMap.Web.Auth;

public class BlazorAuthStateProvider : AuthenticationStateProvider
{
    private static readonly AuthenticationState Anonymous =
        new(new ClaimsPrincipal(new ClaimsIdentity()));

    private readonly MemoryTokenStore _tokenStore;

    public BlazorAuthStateProvider(MemoryTokenStore tokenStore)
    {
        _tokenStore = tokenStore;
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        if (!_tokenStore.IsAuthenticated)
            return Task.FromResult(Anonymous);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, _tokenStore.UserId!),
            new(ClaimTypes.Email, _tokenStore.Email!),
            new(ClaimTypes.Name, _tokenStore.NombreCompleto!),
        };
        foreach (var role in _tokenStore.Roles)
            claims.Add(new(ClaimTypes.Role, role));

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "jwt"));
        return Task.FromResult(new AuthenticationState(principal));
    }

    public void NotifyStateChanged() =>
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
}
```

- [ ] **Step 2: Reemplazar Program.cs**

Leer el archivo actual primero (`C:\Agentes\PropertyMap\src\PropertyMap.Web\PropertyMap.Web\Program.cs`), luego reemplazarlo completamente con:

```csharp
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
```

- [ ] **Step 3: Build Web para verificar**

```bash
cd C:/Agentes/PropertyMap/src
dotnet build PropertyMap.Web/PropertyMap.Web/PropertyMap.Web.csproj 2>&1 | tail -10
```
Expected: `Build succeeded.`

Si hay errores de `IPropertyApiService not found` — es normal hasta Task 5. Proceder igual con el commit.

- [ ] **Step 4: Commit**

```bash
cd C:/Agentes/PropertyMap/src
git add PropertyMap.Web/PropertyMap.Web/Auth/BlazorAuthStateProvider.cs \
        PropertyMap.Web/PropertyMap.Web/Program.cs
git commit -m "feat(web): add BlazorAuthStateProvider and refactor Program.cs to JWT-based auth"
```

---

## Task 3: Login page

**Files:**
- Create: `PropertyMap.Web/PropertyMap.Web/Components/Pages/Account/Login.razor`

- [ ] **Step 1: Crear directorio Account**

```bash
mkdir "C:/Agentes/PropertyMap/src/PropertyMap.Web/PropertyMap.Web/Components/Pages/Account"
```

- [ ] **Step 2: Crear Login.razor**

`PropertyMap.Web/PropertyMap.Web/Components/Pages/Account/Login.razor`
```razor
@page "/Account/Login"
@rendermode InteractiveServer
@inject IAuthService AuthService
@inject NavigationManager Nav

<PageTitle>Iniciar sesión — PropertyMap</PageTitle>

<div class="auth-page">
    <div class="auth-card">
        <a href="/" class="auth-logo">PropertyMap</a>
        <h1 class="auth-title">Iniciar sesión</h1>

        @if (error is not null)
        {
            <div class="auth-error">@error</div>
        }

        <div class="field">
            <label class="field-label" for="email">Email</label>
            <input id="email" type="email" class="field-input"
                   @bind="email" @bind:event="oninput"
                   placeholder="tu@email.com" autocomplete="email" />
        </div>

        <div class="field">
            <label class="field-label" for="password">Contraseña</label>
            <input id="password" type="password" class="field-input"
                   @bind="password" @bind:event="oninput"
                   @onkeydown="OnKeyDown"
                   placeholder="Tu contraseña" autocomplete="current-password" />
        </div>

        <button class="btn-primary auth-btn" @onclick="DoLogin" disabled="@loading">
            @(loading ? "Ingresando..." : "Ingresar")
        </button>

        <p class="auth-footer">
            ¿No tenés cuenta? <a href="/Account/Register">Registrate</a>
        </p>
    </div>
</div>

@code {
    [SupplyParameterFromQuery]
    private string? ReturnUrl { get; set; }

    private string email = "";
    private string password = "";
    private string? error;
    private bool loading;
    private bool sessionRestored;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && !sessionRestored)
        {
            sessionRestored = true;
            var restored = await AuthService.TryRestoreSessionAsync();
            if (restored)
            {
                Nav.NavigateTo(ReturnUrl ?? "/");
            }
        }
    }

    private async Task OnKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter") await DoLogin();
    }

    private async Task DoLogin()
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            error = "Completá email y contraseña.";
            return;
        }
        loading = true;
        error = null;
        var (success, err) = await AuthService.LoginAsync(email, password);
        if (success)
        {
            Nav.NavigateTo(ReturnUrl ?? "/");
        }
        else
        {
            error = err;
            loading = false;
        }
    }
}
```

- [ ] **Step 3: Agregar estilos de auth a app.css**

Abrir `PropertyMap.Web/PropertyMap.Web/wwwroot/css/app.css` y agregar al final:

```css
/* ── Auth pages ─────────────────────────────────────────────────── */
.auth-page {
    min-height: 100svh;
    display: flex;
    align-items: center;
    justify-content: center;
    background: var(--color-surface-2, oklch(97% 0.005 250));
    padding: var(--space-4);
}

.auth-card {
    background: var(--color-surface, white);
    border: 1px solid var(--color-border, oklch(90% 0.01 250));
    border-radius: var(--radius-lg, 12px);
    padding: var(--space-8, 2rem);
    width: 100%;
    max-width: 400px;
    display: flex;
    flex-direction: column;
    gap: var(--space-4, 1rem);
}

.auth-logo {
    font-weight: 700;
    font-size: 1.25rem;
    color: var(--color-brand, oklch(55% 0.18 250));
    text-decoration: none;
    text-align: center;
}

.auth-title {
    font-size: 1.5rem;
    font-weight: 700;
    text-align: center;
    margin: 0;
}

.auth-error {
    background: oklch(95% 0.05 20);
    color: oklch(40% 0.15 20);
    border: 1px solid oklch(85% 0.1 20);
    border-radius: var(--radius-md, 8px);
    padding: var(--space-3, 0.75rem);
    font-size: 0.875rem;
}

.auth-success {
    background: oklch(95% 0.05 140);
    color: oklch(35% 0.12 140);
    border: 1px solid oklch(82% 0.1 140);
    border-radius: var(--radius-md, 8px);
    padding: var(--space-3, 0.75rem);
    font-size: 0.875rem;
}

.auth-btn {
    width: 100%;
    justify-content: center;
    padding-block: var(--space-3, 0.75rem);
}

.auth-footer {
    text-align: center;
    font-size: 0.875rem;
    color: var(--color-text-muted, oklch(55% 0.01 250));
    margin: 0;
}
```

- [ ] **Step 4: Build**

```bash
cd C:/Agentes/PropertyMap/src
dotnet build PropertyMap.Web/PropertyMap.Web/PropertyMap.Web.csproj 2>&1 | tail -10
```
Expected: `Build succeeded.`

Puede dar error si `IPropertyApiService` aún no existe (Task 5). Si el único error es ese, el build "base" funciona y el Task 3 está correcto.

- [ ] **Step 5: Commit**

```bash
cd C:/Agentes/PropertyMap/src
git add PropertyMap.Web/PropertyMap.Web/Components/Pages/Account/Login.razor \
        PropertyMap.Web/PropertyMap.Web/wwwroot/css/app.css
git commit -m "feat(web): add login page with JWT auth and session restore"
```

---

## Task 4: Register page

**Files:**
- Create: `PropertyMap.Web/PropertyMap.Web/Components/Pages/Account/Register.razor`

- [ ] **Step 1: Crear Register.razor**

`PropertyMap.Web/PropertyMap.Web/Components/Pages/Account/Register.razor`
```razor
@page "/Account/Register"
@rendermode InteractiveServer
@inject IAuthService AuthService
@inject NavigationManager Nav

<PageTitle>Crear cuenta — PropertyMap</PageTitle>

<div class="auth-page">
    <div class="auth-card">
        <a href="/" class="auth-logo">PropertyMap</a>
        <h1 class="auth-title">Crear cuenta</h1>

        @if (error is not null)
        {
            <div class="auth-error">@error</div>
        }

        @if (success)
        {
            <div class="auth-success">
                ✓ Cuenta creada. Revisá tu email para verificar tu cuenta y poder iniciar sesión.
            </div>
            <a href="/Account/Login" class="btn-primary auth-btn" style="text-align:center">
                Ir al login
            </a>
        }
        else
        {
            <div class="field-row">
                <div class="field">
                    <label class="field-label" for="nombre">Nombre</label>
                    <input id="nombre" type="text" class="field-input"
                           @bind="nombre" placeholder="Juan" autocomplete="given-name" />
                </div>
                <div class="field">
                    <label class="field-label" for="apellido">Apellido</label>
                    <input id="apellido" type="text" class="field-input"
                           @bind="apellido" placeholder="Pérez" autocomplete="family-name" />
                </div>
            </div>

            <div class="field">
                <label class="field-label" for="email">Email</label>
                <input id="email" type="email" class="field-input"
                       @bind="email" placeholder="tu@email.com" autocomplete="email" />
            </div>

            <div class="field">
                <label class="field-label" for="password">Contraseña</label>
                <input id="password" type="password" class="field-input"
                       @bind="password" placeholder="Mín. 8 caracteres, 1 mayúscula, 1 número, 1 símbolo"
                       autocomplete="new-password" />
            </div>

            <div class="field">
                <label class="field-label" for="confirm">Confirmar contraseña</label>
                <input id="confirm" type="password" class="field-input"
                       @bind="confirm" placeholder="Repetí la contraseña"
                       autocomplete="new-password" />
            </div>

            <button class="btn-primary auth-btn" @onclick="DoRegister" disabled="@loading">
                @(loading ? "Registrando..." : "Crear cuenta")
            </button>

            <p class="auth-footer">
                ¿Ya tenés cuenta? <a href="/Account/Login">Iniciá sesión</a>
            </p>
        }
    </div>
</div>

@code {
    private string nombre = "";
    private string apellido = "";
    private string email = "";
    private string password = "";
    private string confirm = "";
    private string? error;
    private bool loading;
    private bool success;

    private async Task DoRegister()
    {
        if (string.IsNullOrWhiteSpace(nombre) || string.IsNullOrWhiteSpace(apellido)
            || string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            error = "Completá todos los campos.";
            return;
        }
        if (password != confirm)
        {
            error = "Las contraseñas no coinciden.";
            return;
        }

        loading = true;
        error = null;
        var (ok, err) = await AuthService.RegisterAsync(nombre, apellido, email, password, confirm);
        loading = false;
        if (ok) success = true;
        else error = err;
    }
}
```

- [ ] **Step 2: Build**

```bash
cd C:/Agentes/PropertyMap/src
dotnet build PropertyMap.Web/PropertyMap.Web/PropertyMap.Web.csproj 2>&1 | tail -10
```
Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```bash
cd C:/Agentes/PropertyMap/src
git add PropertyMap.Web/PropertyMap.Web/Components/Pages/Account/Register.razor
git commit -m "feat(web): add register page with email verification notice"
```

---

## Task 5: IPropertyApiService + PropertyApiService

**Files:**
- Create: `PropertyMap.Web/PropertyMap.Web/Services/IPropertyApiService.cs`
- Create: `PropertyMap.Web/PropertyMap.Web/Services/PropertyApiService.cs`

- [ ] **Step 1: Crear IPropertyApiService**

`PropertyMap.Web/PropertyMap.Web/Services/IPropertyApiService.cs`
```csharp
using Microsoft.AspNetCore.Components.Forms;
using PropertyMap.Core.DTOs.Admin;
using PropertyMap.Core.DTOs.Properties;
using PropertyMap.Core.DTOs.Publisher;

namespace PropertyMap.Web.Services;

public interface IPropertyApiService
{
    Task<int> CreateListingAsync(CreateListingRequest request);
    Task<List<string>> UploadImagesAsync(int listingId,
        IEnumerable<(byte[] Data, string FileName, string ContentType)> files);
    Task<List<MyListingDto>> GetMyListingsAsync();
    Task<PublisherProfileResponse?> GetPublisherProfileAsync();
    Task<int> EnsurePublisherProfileAsync(string nombre, string telefono);
}
```

- [ ] **Step 2: Crear PropertyApiService**

`PropertyMap.Web/PropertyMap.Web/Services/PropertyApiService.cs`
```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using PropertyMap.Core.DTOs.Admin;
using PropertyMap.Core.DTOs.Properties;
using PropertyMap.Core.DTOs.Publisher;
using PropertyMap.Core.Enums;

namespace PropertyMap.Web.Services;

public class PropertyApiService : IPropertyApiService
{
    private readonly HttpClient _http;
    private readonly MemoryTokenStore _tokenStore;

    public PropertyApiService(IHttpClientFactory httpFactory, MemoryTokenStore tokenStore)
    {
        _http = httpFactory.CreateClient("api");
        _tokenStore = tokenStore;
    }

    private void SetAuth()
    {
        _http.DefaultRequestHeaders.Authorization = _tokenStore.AccessToken is null
            ? null
            : new AuthenticationHeaderValue("Bearer", _tokenStore.AccessToken);
    }

    public async Task<int> CreateListingAsync(CreateListingRequest request)
    {
        SetAuth();
        var resp = await _http.PostAsJsonAsync("api/properties", request);
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<CreatedIdDto>();
        return body!.Id;
    }

    public async Task<List<string>> UploadImagesAsync(int listingId,
        IEnumerable<(byte[] Data, string FileName, string ContentType)> files)
    {
        SetAuth();
        using var form = new MultipartFormDataContent();
        foreach (var (data, fileName, contentType) in files)
        {
            var content = new ByteArrayContent(data);
            content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            form.Add(content, "files", fileName);
        }
        var resp = await _http.PostAsync($"api/properties/{listingId}/images", form);
        if (!resp.IsSuccessStatusCode) return [];
        var body = await resp.Content.ReadFromJsonAsync<UploadUrlsDto>();
        return body?.Urls ?? [];
    }

    public async Task<List<MyListingDto>> GetMyListingsAsync()
    {
        SetAuth();
        var result = await _http.GetFromJsonAsync<List<MyListingDto>>("api/properties/mine");
        return result ?? [];
    }

    public async Task<PublisherProfileResponse?> GetPublisherProfileAsync()
    {
        SetAuth();
        var resp = await _http.GetAsync("api/publisher/profile");
        if (resp.StatusCode == HttpStatusCode.NotFound) return null;
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<PublisherProfileResponse>();
    }

    public async Task<int> EnsurePublisherProfileAsync(string nombre, string telefono)
    {
        var existing = await GetPublisherProfileAsync();
        if (existing is not null) return existing.Id;

        SetAuth();
        var resp = await _http.PostAsJsonAsync("api/publisher/profile",
            new PublisherProfileRequest(nombre, telefono, TipoPublicador.Particular));
        resp.EnsureSuccessStatusCode();
        var created = await resp.Content.ReadFromJsonAsync<PublisherProfileResponse>();
        return created!.Id;
    }

    private record CreatedIdDto(int Id);
    private record UploadUrlsDto(List<string> Urls);
}
```

- [ ] **Step 3: Build completo**

```bash
cd C:/Agentes/PropertyMap/src
dotnet build PropertyMap.Web/PropertyMap.Web/PropertyMap.Web.csproj 2>&1 | tail -10
```
Expected: `Build succeeded.`

Si hay errores del wizard (Task 6 no está hecho aún), son esperados. Fixear solo si los errores son de este task.

- [ ] **Step 4: Commit**

```bash
cd C:/Agentes/PropertyMap/src
git add PropertyMap.Web/PropertyMap.Web/Services/IPropertyApiService.cs \
        PropertyMap.Web/PropertyMap.Web/Services/PropertyApiService.cs
git commit -m "feat(web): add IPropertyApiService and PropertyApiService for API integration"
```

---

## Task 6: Refactor PublishProperty wizard

**Files:**
- Modify: `PropertyMap.Web/PropertyMap.Web/Components/Pages/PublishProperty.razor`

El wizard actual usa repos directamente y guarda fotos al disco de Blazor. Hay que:
1. Reemplazar repos con `IPropertyApiService`
2. Reemplazar el guardado a disco con buffering en memoria (byte arrays)
3. En `Publish()`: crear listing via API → upload imágenes → navegar
4. Agregar `AuthorizeView` para proteger el wizard

**ANTES de modificar**: Leer el archivo completo para entender el estado actual.
Path: `C:\Agentes\PropertyMap\src\PropertyMap.Web\PropertyMap.Web\Components\Pages\PublishProperty.razor`

- [ ] **Step 1: Reemplazar encabezado y directivas**

El inicio del archivo (líneas 1-14) debe quedar así. **Eliminar** las líneas `@inject IListingRepository`, `@inject IPublisherRepository`, `@inject ILocationRepository`, `@inject IWebHostEnvironment` y **agregar** las nuevas:

```razor
@page "/publicar"
@using PropertyMap.Core
@using PropertyMap.Core.Entities
@using PropertyMap.Core.Enums
@using PropertyMap.Core.Interfaces
@inject IPropertyApiService PropertyApi
@inject IAuthService AuthService
@inject IJSRuntime JS
@inject NavigationManager Nav
@rendermode InteractiveServer
```

- [ ] **Step 2: Envolver el contenido en AuthorizeView**

En el markup, reemplazar la apertura del `<div class="app-shell">` para envolver todo en un `<AuthorizeView>`. El markup raíz debe quedar:

```razor
<PageTitle>Publicar propiedad — PropertyMap</PageTitle>

<AuthorizeView>
    <Authorized>
        <div class="app-shell">
            @* ... todo el wizard igual que antes ... *@
        </div>
    </Authorized>
    <NotAuthorized>
        <div class="auth-page">
            <div class="auth-card">
                <a href="/" class="auth-logo">PropertyMap</a>
                <h2 class="auth-title">Publicar propiedad</h2>
                <p style="text-align:center;color:var(--color-text-muted)">
                    Necesitás iniciar sesión para publicar una propiedad.
                </p>
                <a href="/Account/Login?returnUrl=/publicar" class="btn-primary auth-btn" style="text-align:center">
                    Iniciar sesión
                </a>
                <p class="auth-footer">
                    ¿No tenés cuenta? <a href="/Account/Register">Registrate gratis</a>
                </p>
            </div>
        </div>
    </NotAuthorized>
</AuthorizeView>
```

- [ ] **Step 3: Reemplazar el bloque @code — variables de fotos**

En el bloque `@code`, **reemplazar** las variables relacionadas con fotos y publicación. Encontrar la sección de declaración de campos (líneas 356-370 aprox.) y agregar/modificar:

```csharp
// Reemplaza el campo `private bool publishing;` — queda igual
// Agregar debajo de los campos existentes:

// Lista de buffers de fotos: preview (data URL) + bytes para upload
private readonly List<(string Preview, byte[] Bytes, string FileName, string ContentType)> _photoBuffers = [];

private bool sessionRestored;
```

Y en `WizardModel`, el campo `Fotos` ya existe como `List<string>` — se seguirá usando para las URLs de preview. **No cambiar** `WizardModel`.

- [ ] **Step 4: Reemplazar OnAfterRenderAsync**

Reemplazar el método `OnAfterRenderAsync` existente (que restaura el draft) con:

```csharp
protected override async Task OnAfterRenderAsync(bool firstRender)
{
    if (firstRender && !sessionRestored)
    {
        sessionRestored = true;
        // Restaurar sesión JWT si había una guardada
        await AuthService.TryRestoreSessionAsync();

        // Restaurar borrador del wizard
        try
        {
            var json = await JS.InvokeAsync<string?>("pmStorage.get", StorageKey);
            if (!string.IsNullOrEmpty(json))
            {
                var saved = System.Text.Json.JsonSerializer.Deserialize<WizardModel>(json);
                if (saved is not null)
                {
                    model = saved;
                    if (!string.IsNullOrEmpty(model.Ciudad))
                        ciudadTexto = string.IsNullOrEmpty(model.Provincia)
                            ? model.Ciudad
                            : $"{model.Ciudad}, {model.Provincia}";
                    ciudadLat = model.Lat;
                    ciudadLng = model.Lng;
                    if (model.Lat != 0 || model.Lng != 0)
                        coordsTexto = $"{model.Lat.ToString("F5", System.Globalization.CultureInfo.InvariantCulture)}, {model.Lng.ToString("F5", System.Globalization.CultureInfo.InvariantCulture)}";
                    StateHasChanged();
                }
            }
        }
        catch { }
    }
}
```

- [ ] **Step 5: Reemplazar OnFilesSelected**

Reemplazar el método `OnFilesSelected` completo con la versión que usa buffering en memoria:

```csharp
private async Task OnFilesSelected(InputFileChangeEventArgs e)
{
    foreach (var file in e.GetMultipleFiles(maximumFileCount: MaxFotos))
    {
        if (_photoBuffers.Count >= MaxFotos)
        {
            ShowToast($"Máximo {MaxFotos} fotos.", "error");
            break;
        }
        if (file.Size > 5 * 1024 * 1024)
        {
            ShowToast($"{file.Name} supera 5 MB.", "error");
            continue;
        }
        try
        {
            using var ms = new MemoryStream();
            await file.OpenReadStream(5 * 1024 * 1024).CopyToAsync(ms);
            var bytes = ms.ToArray();
            var contentType = file.ContentType is { Length: > 0 } ct ? ct : "image/jpeg";
            var base64 = Convert.ToBase64String(bytes);
            var preview = $"data:{contentType};base64,{base64}";

            _photoBuffers.Add((preview, bytes, file.Name, contentType));
            model.Fotos.Add(preview);
        }
        catch
        {
            ShowToast($"No se pudo cargar {file.Name}.", "error");
        }
    }
}
```

- [ ] **Step 6: Actualizar MovePhoto y RemovePhoto**

Reemplazar los métodos `MovePhoto` y `RemovePhoto` para mantener `_photoBuffers` sincronizado con `model.Fotos`:

```csharp
private void MovePhoto(int idx, int dir)
{
    var n = idx + dir;
    if (n < 0 || n >= model.Fotos.Count) return;
    (model.Fotos[idx], model.Fotos[n]) = (model.Fotos[n], model.Fotos[idx]);
    (_photoBuffers[idx], _photoBuffers[n]) = (_photoBuffers[n], _photoBuffers[idx]);
}

private void RemovePhoto(int idx)
{
    model.Fotos.RemoveAt(idx);
    if (idx < _photoBuffers.Count) _photoBuffers.RemoveAt(idx);
}
```

- [ ] **Step 7: Reemplazar Publish()**

Reemplazar el método `Publish()` completo:

```csharp
private async Task Publish()
{
    if (!ValidateStep()) return;
    if (_photoBuffers.Count == 0)
    {
        ShowToast("Subí al menos una foto.", "error");
        return;
    }
    publishing = true;
    try
    {
        var request = new PropertyMap.Core.DTOs.Properties.CreateListingRequest(
            Operacion: model.Operacion,
            TipoPropiedad: model.Tipo,
            Titulo: model.Titulo,
            Descripcion: model.Descripcion ?? "",
            Precio: model.Precio,
            Moneda: model.Moneda,
            DireccionTexto: model.Direccion,
            Ciudad: model.Ciudad ?? "",
            Provincia: model.Provincia ?? "",
            Lat: model.Lat,
            Lng: model.Lng,
            Superficie: model.Superficie > 0 ? model.Superficie : null,
            SuperficieCubierta: model.SuperficieCubierta > 0 ? model.SuperficieCubierta : null,
            Ambientes: model.Ambientes > 0 ? model.Ambientes : null,
            Dormitorios: model.Dormitorios > 0 ? model.Dormitorios : null,
            Banos: model.Banos > 0 ? model.Banos : null,
            Antiguedad: model.Antiguedad > 0 ? model.Antiguedad : null,
            Cochera: model.Cochera,
            Amenities: model.Amenities
        );

        int listingId;
        try
        {
            listingId = await PropertyApi.CreateListingAsync(request);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.BadRequest)
        {
            ShowToast("Creá un perfil de publisher primero. Verificá tu cuenta.", "error");
            publishing = false;
            return;
        }

        // Upload imágenes
        await PropertyApi.UploadImagesAsync(listingId,
            _photoBuffers.Select(p => (p.Bytes, p.FileName, p.ContentType)));

        await JS.InvokeVoidAsync("pmStorage.remove", StorageKey);
        ShowToast("¡Propiedad enviada a revisión!", "success");
        await Task.Delay(700);
        Nav.NavigateTo($"/property/{listingId}");
    }
    catch (Exception ex)
    {
        ShowToast($"Error al publicar: {ex.Message}", "error");
        publishing = false;
    }
}
```

- [ ] **Step 8: Build**

```bash
cd C:/Agentes/PropertyMap/src
dotnet build PropertyMap.Web/PropertyMap.Web/PropertyMap.Web.csproj 2>&1 | tail -15
```
Expected: `Build succeeded.`

Si hay errores:
- `IListingRepository/IPublisherRepository/ILocationRepository not found` → quedan registrados en Program.cs, son errores de injección en el razor. Revisar que las líneas `@inject` viejas hayan sido eliminadas.
- `WizardModel no tiene campo X` → verificar que solo se cambió lo especificado en los steps anteriores.

- [ ] **Step 9: Commit**

```bash
cd C:/Agentes/PropertyMap/src
git add PropertyMap.Web/PropertyMap.Web/Components/Pages/PublishProperty.razor
git commit -m "feat(web): refactor wizard to use IPropertyApiService, auth guard, in-memory photo buffering"
```

---

## Task 7: Publisher Dashboard

**Files:**
- Create: `PropertyMap.Web/PropertyMap.Web/Components/Pages/Publisher/Dashboard.razor`

- [ ] **Step 1: Crear directorio Publisher**

```bash
mkdir "C:/Agentes/PropertyMap/src/PropertyMap.Web/PropertyMap.Web/Components/Pages/Publisher"
```

- [ ] **Step 2: Crear Dashboard.razor**

`PropertyMap.Web/PropertyMap.Web/Components/Pages/Publisher/Dashboard.razor`
```razor
@page "/publisher/dashboard"
@rendermode InteractiveServer
@inject IPropertyApiService PropertyApi
@inject IAuthService AuthService
@inject NavigationManager Nav

<PageTitle>Mi panel — PropertyMap</PageTitle>

<AuthorizeView>
    <Authorized>
        <div class="app-shell" style="display:flex;flex-direction:column">
            <nav class="pm-navbar" role="navigation">
                <a href="/" class="pm-navbar__logo">PropertyMap</a>
                <span style="font-weight:600">Mi panel</span>
                <div class="pm-navbar__actions">
                    <a href="/publicar" class="btn-primary">+ Publicar</a>
                    <button class="btn-ghost" @onclick="Logout">Salir</button>
                </div>
            </nav>

            <div style="padding:var(--space-6,1.5rem);max-width:900px;margin:0 auto;width:100%">
                <h1 style="font-size:1.5rem;font-weight:700;margin-bottom:var(--space-4)">
                    Mis propiedades
                </h1>

                @if (loading)
                {
                    <p style="color:var(--color-text-muted)">Cargando...</p>
                }
                else if (listings.Count == 0)
                {
                    <div style="text-align:center;padding:var(--space-10) 0;color:var(--color-text-muted)">
                        <p style="font-size:1.125rem">Todavía no publicaste ninguna propiedad.</p>
                        <a href="/publicar" class="btn-primary" style="margin-top:var(--space-4);display:inline-block">
                            Publicar mi primera propiedad
                        </a>
                    </div>
                }
                else
                {
                    <div style="display:flex;flex-direction:column;gap:var(--space-3)">
                        @foreach (var l in listings)
                        {
                            <div class="dashboard-listing-card">
                                @if (l.FotoPrincipalUrl is not null)
                                {
                                    <img src="@l.FotoPrincipalUrl" alt="@l.Titulo" class="dashboard-listing-img" />
                                }
                                else
                                {
                                    <div class="dashboard-listing-img dashboard-listing-img--placeholder" aria-hidden="true">
                                        <svg width="24" height="24" viewBox="0 0 24 24" fill="currentColor">
                                            <path d="M10 20v-6h4v6h5v-8h3L12 3 2 12h3v8z"/>
                                        </svg>
                                    </div>
                                }
                                <div class="dashboard-listing-body">
                                    <a href="/property/@l.Id" class="dashboard-listing-title">@l.Titulo</a>
                                    <div class="dashboard-listing-meta">
                                        @l.Ciudad · @l.TipoPropiedad · @l.Operacion
                                    </div>
                                    <div class="dashboard-listing-price">
                                        @l.Moneda @l.Precio.ToString("N0")
                                    </div>
                                </div>
                                <span class="dashboard-listing-status @StatusClass(l.Estado)">
                                    @StatusLabel(l.Estado)
                                </span>
                            </div>
                        }
                    </div>
                }
            </div>
        </div>
    </Authorized>
    <NotAuthorized>
        <div class="auth-page">
            <div class="auth-card">
                <a href="/" class="auth-logo">PropertyMap</a>
                <p style="text-align:center">Necesitás iniciar sesión para ver tu panel.</p>
                <a href="/Account/Login?returnUrl=/publisher/dashboard" class="btn-primary auth-btn" style="text-align:center">
                    Iniciar sesión
                </a>
            </div>
        </div>
    </NotAuthorized>
</AuthorizeView>

@code {
    private List<PropertyMap.Core.DTOs.Properties.MyListingDto> listings = [];
    private bool loading = true;
    private bool sessionRestored;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender && !sessionRestored)
        {
            sessionRestored = true;
            await AuthService.TryRestoreSessionAsync();
            await LoadListings();
            StateHasChanged();
        }
    }

    private async Task LoadListings()
    {
        loading = true;
        try
        {
            listings = await PropertyApi.GetMyListingsAsync();
        }
        catch
        {
            listings = [];
        }
        finally
        {
            loading = false;
        }
    }

    private async Task Logout()
    {
        await AuthService.LogoutAsync();
        Nav.NavigateTo("/");
    }

    private static string StatusLabel(string estado) => estado switch
    {
        "Borrador" => "Borrador",
        "PendienteAprobacion" => "En revisión",
        "Publicada" => "Publicada",
        "Pausada" => "Pausada",
        "Vendida" => "Vendida",
        "Alquilada" => "Alquilada",
        "Eliminada" => "Eliminada",
        _ => estado
    };

    private static string StatusClass(string estado) => estado switch
    {
        "Publicada" => "status--publicada",
        "PendienteAprobacion" => "status--pendiente",
        "Pausada" => "status--pausada",
        "Borrador" => "status--borrador",
        _ => "status--default"
    };
}
```

- [ ] **Step 3: Agregar estilos del dashboard a app.css**

Abrir `PropertyMap.Web/PropertyMap.Web/wwwroot/css/app.css` y agregar al final:

```css
/* ── Publisher Dashboard ─────────────────────────────────────────── */
.dashboard-listing-card {
    display: flex;
    align-items: center;
    gap: var(--space-4, 1rem);
    background: var(--color-surface, white);
    border: 1px solid var(--color-border, oklch(90% 0.01 250));
    border-radius: var(--radius-lg, 12px);
    padding: var(--space-3, 0.75rem);
    transition: box-shadow 0.15s;
}

.dashboard-listing-card:hover {
    box-shadow: 0 2px 12px oklch(0% 0 0 / 0.08);
}

.dashboard-listing-img {
    width: 80px;
    height: 60px;
    object-fit: cover;
    border-radius: var(--radius-md, 8px);
    flex-shrink: 0;
}

.dashboard-listing-img--placeholder {
    display: flex;
    align-items: center;
    justify-content: center;
    background: var(--color-surface-2, oklch(97% 0.005 250));
    color: var(--color-text-muted, oklch(55% 0.01 250));
}

.dashboard-listing-body {
    flex: 1;
    min-width: 0;
}

.dashboard-listing-title {
    font-weight: 600;
    font-size: 0.9375rem;
    color: var(--color-text, oklch(20% 0.01 250));
    text-decoration: none;
    display: block;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
}

.dashboard-listing-title:hover { text-decoration: underline; }

.dashboard-listing-meta {
    font-size: 0.8125rem;
    color: var(--color-text-muted, oklch(55% 0.01 250));
    margin-top: 2px;
}

.dashboard-listing-price {
    font-size: 0.875rem;
    font-weight: 600;
    color: var(--color-brand, oklch(55% 0.18 250));
    margin-top: 4px;
}

.dashboard-listing-status {
    font-size: 0.75rem;
    font-weight: 600;
    padding: 3px 10px;
    border-radius: 999px;
    flex-shrink: 0;
    text-transform: uppercase;
    letter-spacing: 0.03em;
}

.status--publicada   { background: oklch(92% 0.1 140); color: oklch(35% 0.12 140); }
.status--pendiente   { background: oklch(94% 0.1 80);  color: oklch(40% 0.12 80);  }
.status--pausada     { background: oklch(92% 0.05 250); color: oklch(40% 0.08 250); }
.status--borrador    { background: oklch(93% 0.02 250); color: oklch(50% 0.05 250); }
.status--default     { background: oklch(93% 0.02 250); color: oklch(50% 0.05 250); }
```

- [ ] **Step 4: Build final**

```bash
cd C:/Agentes/PropertyMap/src
dotnet build PropertyMap.Web/PropertyMap.Web/PropertyMap.Web.csproj 2>&1 | tail -10
```
Expected: `Build succeeded. 0 Error(s).`

- [ ] **Step 5: Commit**

```bash
cd C:/Agentes/PropertyMap/src
git add PropertyMap.Web/PropertyMap.Web/Components/Pages/Publisher/Dashboard.razor \
        PropertyMap.Web/PropertyMap.Web/wwwroot/css/app.css
git commit -m "feat(web): add publisher dashboard with listing status overview"
```

---

## Self-Review

### Spec coverage

- ✅ Auth bridge JWT — `MemoryTokenStore` + `BlazorAuthStateProvider` + `AuthService` (Tasks 1–2)
- ✅ Login page `/Account/Login` — formulario, restaura sesión, redirect a returnUrl (Task 3)
- ✅ Register page `/Account/Register` — formulario, muestra mensaje post-registro (Task 4)
- ✅ Logout — `AuthService.LogoutAsync()` limpia store + localStorage (Task 1)
- ✅ Program.cs — reemplaza `AddDefaultIdentity<IdentityUser>` con `AddIdentity<ApplicationUser>` (fix bug) (Task 2)
- ✅ Wizard usa `IPropertyApiService` — no más repos directos (Task 6)
- ✅ Wizard con auth guard — `<AuthorizeView>` muestra login link si no autenticado (Task 6)
- ✅ Wizard fotos en memoria — `OnFilesSelected` guarda bytes en `_photoBuffers` (Task 6)
- ✅ Wizard `Publish()` — llama a API → sube imágenes (Task 6)
- ✅ Publisher dashboard `/publisher/dashboard` — muestra listados con estados (Task 7)
- ✅ Session restore — `TryRestoreSessionAsync` en `OnAfterRenderAsync` (Tasks 3, 6, 7)
- ✅ Navbar ya tiene los links correctos (`/Account/Login`, `/publisher/dashboard`) — sin cambios

### Dependencias de tasks
```
Task 1 → Task 2 (BlazorAuthStateProvider depende de MemoryTokenStore)
Task 1 → Task 3 (Login usa IAuthService)
Task 1 → Task 4 (Register usa IAuthService)
Task 2 → Task 3 (Program.cs registra los servicios)
Task 5 → Task 6 (Wizard usa IPropertyApiService)
Task 5 → Task 7 (Dashboard usa IPropertyApiService)
Task 1+5 → Task 6 (Wizard usa IAuthService + IPropertyApiService)
```

### Notas para el implementador

- `ProtectedLocalStorage` puede lanzar excepción durante SSR (antes de que JS esté disponible). Siempre usar try-catch al llamar `TryRestoreSessionAsync()`.
- `AddIdentity<ApplicationUser, IdentityRole>` registra cookie auth schemes, pero no interfiere con el `BlazorAuthStateProvider` — la auth state de Blazor viene del provider custom, no de cookies.
- El wizard en el Step 7 llama a `PropertyApi.CreateListingAsync` que llega al API que requiere el rol "Publisher". Si el usuario logueado no tiene un perfil de publisher, la API retorna 400. El wizard muestra el error apropiado.
- Los estilos del dashboard y de las páginas auth están hardcodeados con valores de OKLCH compatibles con el design system existente. Si el design system define CSS variables más específicas, usarlas en lugar de los valores hardcodeados.

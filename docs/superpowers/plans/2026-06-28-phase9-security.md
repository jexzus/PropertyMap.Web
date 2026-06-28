# Phase 9.3 — Security Audit Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Cerrar 4 gaps de seguridad reales en `PropertyMap.Api`: sin rate limiting, sin lockout de cuentas, sin validación de longitud máxima en texto libre, y secret JWT commiteado en texto plano.

**Architecture:** Cambios acotados a `PropertyMap.Api/Program.cs`, `PropertyMap.Api/Controllers/AuthController.cs`, varios DTOs en `PropertyMap.Core/DTOs/`, y `PropertyMap.Tests/Api/TestWebApplicationFactory.cs`. Antes de tocar producción, se blinda la infraestructura de testing (Task 1) para que rate limiting y el cambio del secret JWT no rompan los 101 tests existentes, que comparten una sola instancia de `TestWebApplicationFactory` por clase de test.

**Tech Stack:** `Microsoft.AspNetCore.RateLimiting` (nativo del SDK .NET 9, sin paquete NuGet nuevo), `System.ComponentModel.DataAnnotations`, ASP.NET Core Identity (lockout ya soportado, falta wiring manual).

**Spec de referencia:** `docs/superpowers/specs/2026-06-28-phase9-security-design.md`

**Nota de corrección de spec:** la spec asumía un único frente de cambios en `Program.cs`/`AuthController.cs`, pero al escribir este plan se confirmó que aplicar rate limiting (política `"auth"`, 5 req/15min) directamente rompería los 7 tests existentes de `AuthControllerTests.cs` — todos comparten la misma instancia de `TestWebApplicationFactory` (vía `IClassFixture`) y por lo tanto el mismo estado del rate limiter dentro de esa clase. Por eso este plan agrega una Task 1 (no mencionada explícitamente en la spec) que introduce un flag de configuración para desactivar el rate limiting en tests, y deja la verificación real del rate limiter en una factory de test separada que lo reactiva explícitamente.

---

### Task 1: Blindar TestWebApplicationFactory antes de tocar producción

**Files:**
- Modify: `PropertyMap.Tests/Api/TestWebApplicationFactory.cs`

- [ ] **Step 1: Agregar el using necesario**

Al inicio del archivo, junto a los demás `using`, agregar:

```csharp
using Microsoft.Extensions.Configuration;
```

- [ ] **Step 2: Agregar overrides de configuración para tests**

Dentro de `ConfigureWebHost`, inmediatamente después de `builder.ConfigureServices(services => { ... });` (justo antes de `builder.UseEnvironment("Testing");`), agregar:

```csharp
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimiting:Enabled"] = "false",
                ["JwtSettings:Secret"] = "test-secret-key-for-integration-tests-only-min32chars!"
            });
        });
```

El archivo `ConfigureWebHost` completo debe quedar así (mostrado para referencia, no reemplazar todo, solo insertar el bloque de arriba en la posición indicada):

```csharp
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

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimiting:Enabled"] = "false",
                ["JwtSettings:Secret"] = "test-secret-key-for-integration-tests-only-min32chars!"
            });
        });

        builder.UseEnvironment("Testing");
    }
```

- [ ] **Step 3: Verificar que la suite completa sigue pasando (sin cambios de producción todavía, este paso solo confirma que el archivo de test sigue compilando y los 101 tests no se rompieron)**

Run: `cd C:\Agentes\PropertyMap && dotnet test src/PropertyMap.Tests/PropertyMap.Tests.csproj`
Expected: `Correctas! - Con error: 0, Superado: 101, Total: 101`

- [ ] **Step 4: Commit**

```bash
cd C:\Agentes\PropertyMap\src
git add PropertyMap.Tests/Api/TestWebApplicationFactory.cs
git commit -m "test: disable rate limiting and override JWT secret for integration tests"
```

---

### Task 2: Rate limiting

**Files:**
- Modify: `PropertyMap.Api/Program.cs`
- Modify: `PropertyMap.Api/Controllers/AuthController.cs`
- Create: `PropertyMap.Tests/Api/RateLimitTestWebApplicationFactory.cs`
- Create: `PropertyMap.Tests/Api/SecurityTests.cs`

- [ ] **Step 1: Agregar los usings necesarios en Program.cs**

Al inicio del archivo, junto a los demás `using`, agregar:

```csharp
using System.Threading.RateLimiting;
```

- [ ] **Step 2: Leer el flag de configuración y registrar el rate limiter**

Inmediatamente después de la línea `builder.WebHost.ConfigureKestrel(options => { ... });` (antes de `var app = builder.Build();`), agregar:

```csharp
var rateLimitingEnabled = builder.Configuration.GetValue("RateLimiting:Enabled", true);
if (rateLimitingEnabled)
{
    builder.Services.AddRateLimiter(options =>
    {
        options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
            RateLimitPartition.GetFixedWindowLimiter(
                ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    Window = TimeSpan.FromMinutes(1),
                    PermitLimit = 100
                }));

        options.AddFixedWindowLimiter("auth", opt =>
        {
            opt.Window = TimeSpan.FromMinutes(15);
            opt.PermitLimit = 5;
        });

        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    });
}
```

- [ ] **Step 3: Activar el middleware**

Inmediatamente después de `app.UseAuthorization();` (antes de `app.UseStaticFiles();`), agregar:

```csharp
if (rateLimitingEnabled)
{
    app.UseRateLimiter();
}
```

- [ ] **Step 4: Aplicar la política "auth" a AuthController**

En `PropertyMap.Api/Controllers/AuthController.cs`, agregar el using y el atributo a nivel de clase:

```csharp
using Microsoft.AspNetCore.RateLimiting;
```

Y modificar la declaración de la clase de:
```csharp
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
```
a:
```csharp
[ApiController]
[Route("api/auth")]
[EnableRateLimiting("auth")]
public class AuthController : ControllerBase
```

- [ ] **Step 5: Crear la factory de test que reactiva el rate limiting**

```csharp
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace PropertyMap.Tests.Api;

public class RateLimitTestWebApplicationFactory : TestWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimiting:Enabled"] = "true"
            });
        });
    }
}
```

- [ ] **Step 6: Escribir el test de rate limiting**

```csharp
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
```

- [ ] **Step 7: Correr el test nuevo**

Run: `cd C:\Agentes\PropertyMap && dotnet test src/PropertyMap.Tests/PropertyMap.Tests.csproj --filter "FullyQualifiedName~SecurityTests"`
Expected: `Correctas! - Con error: 0, Superado: 1, Total: 1`

- [ ] **Step 8: Correr la suite completa para confirmar que los 101 tests existentes (con rate limiting deshabilitado vía Task 1) siguen pasando**

Run: `cd C:\Agentes\PropertyMap && dotnet test src/PropertyMap.Tests/PropertyMap.Tests.csproj`
Expected: `Correctas! - Con error: 0, Superado: 102, Total: 102`

- [ ] **Step 9: Commit**

```bash
cd C:\Agentes\PropertyMap\src
git add PropertyMap.Api/Program.cs PropertyMap.Api/Controllers/AuthController.cs PropertyMap.Tests/Api/RateLimitTestWebApplicationFactory.cs PropertyMap.Tests/Api/SecurityTests.cs
git commit -m "feat(security): add rate limiting (global + strict auth policy)"
```

---

### Task 3: Lockout de cuentas

**Files:**
- Modify: `PropertyMap.Api/Program.cs`
- Modify: `PropertyMap.Api/Controllers/AuthController.cs`
- Modify: `PropertyMap.Tests/Api/SecurityTests.cs`

- [ ] **Step 1: Configurar el lockout en AddIdentity**

En `Program.cs`, dentro del bloque `builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options => { ... })`, agregar después de `options.SignIn.RequireConfirmedEmail = true;`:

```csharp
    options.Lockout.MaxFailedAccessAttempts = 3;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.AllowedForNewUsers = true;
```

- [ ] **Step 2: Reescribir Login en AuthController.cs para wirear el lockout manualmente**

Reemplazar el método `Login` completo:

```csharp
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null)
            return Unauthorized(new { message = "Credenciales incorrectas." });

        if (await _userManager.IsLockedOutAsync(user))
            return StatusCode(423, new { message = "Cuenta bloqueada temporalmente por intentos fallidos. Probá de nuevo en unos minutos." });

        if (!await _userManager.CheckPasswordAsync(user, request.Password))
        {
            await _userManager.AccessFailedAsync(user);
            return Unauthorized(new { message = "Credenciales incorrectas." });
        }

        await _userManager.ResetAccessFailedCountAsync(user);

        if (!user.EmailConfirmed)
            return Unauthorized(new { message = "Debés verificar tu email antes de iniciar sesión." });

        if (user.Estado == EstadoUsuario.Suspendido)
            return Unauthorized(new { message = "Tu cuenta está suspendida." });

        var roles = await _userManager.GetRolesAsync(user);
        var jwtSettings = _config.GetSection("JwtSettings");

        user.RefreshToken = _tokenService.GenerateRefreshToken();
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(
            int.Parse(jwtSettings["RefreshTokenExpiryDays"] ?? "7"));
        await _userManager.UpdateAsync(user);

        return Ok(new AuthResponse(
            AccessToken: _tokenService.GenerateAccessToken(user, roles),
            RefreshToken: user.RefreshToken,
            AccessTokenExpiry: DateTime.UtcNow.AddMinutes(
                int.Parse(jwtSettings["AccessTokenExpiryMinutes"] ?? "15")),
            UserId: user.Id,
            Email: user.Email!,
            NombreCompleto: $"{user.Nombre} {user.Apellido}",
            Roles: roles
        ));
    }
```

- [ ] **Step 3: Agregar las pruebas de lockout a SecurityTests.cs**

Agregar estos métodos dentro de la clase `SecurityTests`, después de `Login_ExceedsAuthRateLimit_Returns429`. **Importante:** estas pruebas necesitan un cliente nuevo por cada intento de login para no acumular contra el rate limit de la política `"auth"` (5 req/15min) dentro del mismo test — como la clase usa `RateLimitTestWebApplicationFactory` (rate limiting activo), y cada `[Fact]` ya cuenta como hasta 4-5 requests a `/api/auth/login`, hace falta verificar que el test de lockout no choque con el rate limit. Para evitarlo, estas pruebas de lockout viven en una clase separada con rate limiting deshabilitado:

```csharp
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using PropertyMap.Core.Entities;
using PropertyMap.Core.Enums;
```

(agregar estos usings al inicio de `SecurityTests.cs` si no están)

```csharp
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
```

Esta clase `LockoutTests` se agrega en el mismo archivo `SecurityTests.cs`, debajo de la clase `SecurityTests` ya existente (un archivo puede tener varias clases en C#, y mantenerlas juntas evita un archivo nuevo solo para esto). Asegurarse de que los usings `System.Net`, `System.Net.Http.Json`, `PropertyMap.Core.DTOs.Auth`, `Xunit` ya presentes en el archivo cubran también esta clase.

- [ ] **Step 4: Correr los tests nuevos**

Run: `cd C:\Agentes\PropertyMap && dotnet test src/PropertyMap.Tests/PropertyMap.Tests.csproj --filter "FullyQualifiedName~LockoutTests"`
Expected: `Correctas! - Con error: 0, Superado: 2, Total: 2`

- [ ] **Step 5: Correr la suite completa**

Run: `cd C:\Agentes\PropertyMap && dotnet test src/PropertyMap.Tests/PropertyMap.Tests.csproj`
Expected: `Correctas! - Con error: 0, Superado: 104, Total: 104`

- [ ] **Step 6: Commit**

```bash
cd C:\Agentes\PropertyMap\src
git add PropertyMap.Api/Program.cs PropertyMap.Api/Controllers/AuthController.cs PropertyMap.Tests/Api/SecurityTests.cs
git commit -m "feat(security): lock accounts after 3 failed login attempts"
```

---

### Task 4: Validaciones de longitud máxima

**Files:**
- Modify: `PropertyMap.Core/DTOs/Auth/RegisterRequest.cs`
- Modify: `PropertyMap.Core/DTOs/Properties/CreateListingRequest.cs`
- Modify: `PropertyMap.Core/DTOs/Alerts/CreateAlertRequest.cs`
- Modify: `PropertyMap.Core/DTOs/Reports/CreateReportRequest.cs`
- Modify: `PropertyMap.Tests/Api/PropertiesControllerTests.cs`

- [ ] **Step 1: Agregar StringLength a RegisterRequest**

Reemplazar el contenido completo de `PropertyMap.Core/DTOs/Auth/RegisterRequest.cs`:

```csharp
using System.ComponentModel.DataAnnotations;

namespace PropertyMap.Core.DTOs.Auth;

public record RegisterRequest(
    [StringLength(100)] string Nombre,
    [StringLength(100)] string Apellido,
    string Email,
    string Password,
    string ConfirmPassword
);
```

- [ ] **Step 2: Agregar StringLength a CreateListingRequest**

Reemplazar el contenido completo de `PropertyMap.Core/DTOs/Properties/CreateListingRequest.cs`:

```csharp
using System.ComponentModel.DataAnnotations;
using PropertyMap.Core.Enums;

namespace PropertyMap.Core.DTOs.Properties;

public record CreateListingRequest(
    TipoOperacion Operacion,
    TipoPropiedad TipoPropiedad,
    [StringLength(150)] string Titulo,
    [StringLength(5000)] string Descripcion,
    decimal Precio,
    string Moneda,
    [StringLength(200)] string DireccionTexto,
    [StringLength(200)] string Ciudad,
    [StringLength(200)] string Provincia,
    double Lat,
    double Lng,
    decimal? Superficie,
    decimal? SuperficieCubierta,
    int? Ambientes,
    int? Dormitorios,
    int? Banos,
    int? Antiguedad,
    bool Cochera,
    List<string> Amenities
);
```

- [ ] **Step 3: Agregar StringLength a CreateAlertRequest**

Reemplazar el contenido completo de `PropertyMap.Core/DTOs/Alerts/CreateAlertRequest.cs`:

```csharp
using System.ComponentModel.DataAnnotations;
using PropertyMap.Core.Enums;

namespace PropertyMap.Core.DTOs.Alerts;

public record CreateAlertRequest(
    [StringLength(100)] string? Nombre,
    TipoOperacion? Operacion,
    TipoPropiedad? TipoPropiedad,
    [StringLength(200)] string? Ciudad,
    decimal? PrecioMax,
    string? Moneda,
    int? DormitoriosMin
);
```

- [ ] **Step 4: Agregar StringLength a CreateReportRequest**

Reemplazar el contenido completo de `PropertyMap.Core/DTOs/Reports/CreateReportRequest.cs`:

```csharp
using System.ComponentModel.DataAnnotations;
using PropertyMap.Core.Enums;

namespace PropertyMap.Core.DTOs.Reports;

public record CreateReportRequest(
    int PropertyListingId,
    MotivoReporte Motivo,
    [StringLength(1000)] string? Descripcion
);
```

- [ ] **Step 5: Agregar el test de validación a PropertiesControllerTests.cs**

Abrir `PropertyMap.Tests/Api/PropertiesControllerTests.cs`, revisar cómo construye un `CreateListingRequest` de ejemplo en sus tests existentes (mismo patrón que `BuildListingRequest`/`SampleListing` usado en otros archivos de este mismo directorio), y agregar al final de la clase de test principal de ese archivo:

```csharp
    [Fact]
    public async Task Create_TituloExceedsMaxLength_Returns400()
    {
        var (pubClient, _) = await TestAuthHelper.CreateAuthenticatedPublisherAsync(_factory);
        await TestAuthHelper.CreatePublisherProfileAsync(pubClient);

        var tituloLargo = new string('A', 200); // excede el límite de 150
        var request = new CreateListingRequest(
            Operacion: TipoOperacion.Venta, TipoPropiedad: TipoPropiedad.Casa,
            Titulo: tituloLargo, Descripcion: "Test",
            Precio: 70000, Moneda: "USD",
            DireccionTexto: "Calle Validacion 1", Ciudad: "Salta", Provincia: "Salta",
            Lat: -24.78, Lng: -65.41,
            Superficie: null, SuperficieCubierta: null, Ambientes: null,
            Dormitorios: null, Banos: null, Antiguedad: null,
            Cochera: false, Amenities: []);

        var resp = await pubClient.PostAsJsonAsync("/api/properties", request);

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }
```

Verificar que el archivo ya tiene los `using` necesarios (`System.Net`, `System.Net.Http.Json`, `PropertyMap.Core.DTOs.Properties`, `PropertyMap.Core.Enums`, `Xunit`) — si falta alguno, agregarlo al inicio del archivo.

- [ ] **Step 6: Correr el test nuevo**

Run: `cd C:\Agentes\PropertyMap && dotnet test src/PropertyMap.Tests/PropertyMap.Tests.csproj --filter "FullyQualifiedName~Create_TituloExceedsMaxLength_Returns400"`
Expected: `Correctas! - Con error: 0, Superado: 1, Total: 1`

- [ ] **Step 7: Correr la suite completa**

Run: `cd C:\Agentes\PropertyMap && dotnet test src/PropertyMap.Tests/PropertyMap.Tests.csproj`
Expected: `Correctas! - Con error: 0, Superado: 105, Total: 105`

- [ ] **Step 8: Commit**

```bash
cd C:\Agentes\PropertyMap\src
git add PropertyMap.Core/DTOs/Auth/RegisterRequest.cs PropertyMap.Core/DTOs/Properties/CreateListingRequest.cs PropertyMap.Core/DTOs/Alerts/CreateAlertRequest.cs PropertyMap.Core/DTOs/Reports/CreateReportRequest.cs PropertyMap.Tests/Api/PropertiesControllerTests.cs
git commit -m "feat(security): add max-length validation to free-text request fields"
```

---

### Task 5: Sacar el secret JWT del repo

**Files:**
- Modify: `PropertyMap.Api/appsettings.json`
- Modify: `PropertyMap.Api/Program.cs`

- [ ] **Step 1: Vaciar el secret en appsettings.json**

En `PropertyMap.Api/appsettings.json`, cambiar:
```json
    "Secret": "propertymap-super-secret-key-change-in-production-min32chars!",
```
a:
```json
    "Secret": "",
```

- [ ] **Step 2: Agregar el check explícito en Program.cs**

En `Program.cs`, reemplazar la línea:
```csharp
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
```
por:
```csharp
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var jwtSecret = jwtSettings["Secret"];
if (string.IsNullOrEmpty(jwtSecret))
    throw new InvalidOperationException(
        "JwtSettings:Secret no está configurado. En desarrollo, ejecutar desde PropertyMap.Api/: " +
        "dotnet user-secrets set \"JwtSettings:Secret\" \"<valor-largo-aleatorio>\". " +
        "En producción, configurar la variable de entorno JwtSettings__Secret.");
```

Y reemplazar, dentro de `.AddJwtBearer(options => { ... })`, la línea:
```csharp
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtSettings["Secret"]!)),
```
por:
```csharp
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(jwtSecret)),
```

- [ ] **Step 3: Configurar el secret de desarrollo local con user-secrets**

Run (desde la carpeta del proyecto Api):
```bash
cd C:\Agentes\PropertyMap\src\PropertyMap.Api
dotnet user-secrets init
dotnet user-secrets set "JwtSettings:Secret" "propertymap-dev-secret-rotate-this-key-min32chars!"
```
Expected: confirma `UserSecretsId` agregado al `.csproj` (si no existía) y el secret guardado fuera del repo (en `%APPDATA%\Microsoft\UserSecrets\<id>\secrets.json` en Windows).

- [ ] **Step 4: Verificar que la app sigue arrancando localmente con el secret de user-secrets**

Run: `cd C:\Agentes\PropertyMap && dotnet build src/PropertyMap.Web/PropertyMap.Web.sln`
Expected: `Compilación correcta. 0 Errores`

(No se agrega test automatizado para este cambio — es configuración, no lógica de negocio, y los tests ya reciben su propio secret vía el override agregado en Task 1 de este plan.)

- [ ] **Step 5: Correr la suite completa para confirmar que el cambio de appsettings.json no rompió nada**

Run: `cd C:\Agentes\PropertyMap && dotnet test src/PropertyMap.Tests/PropertyMap.Tests.csproj`
Expected: `Correctas! - Con error: 0, Superado: 105, Total: 105`

- [ ] **Step 6: Commit**

```bash
cd C:\Agentes\PropertyMap\src
git add PropertyMap.Api/appsettings.json PropertyMap.Api/Program.cs
git commit -m "fix(security): remove hardcoded JWT secret from source control"
```

---

### Task 6: Verificación final

**Files:** ninguno (solo verificación)

- [ ] **Step 1: Correr toda la suite de tests**

Run: `cd C:\Agentes\PropertyMap && dotnet test src/PropertyMap.Tests/PropertyMap.Tests.csproj`
Expected: `Correctas! - Con error: 0, Superado: 105` (101 previos + 1 de rate limiting + 2 de lockout + 1 de validación de longitud)

- [ ] **Step 2: Correr el build completo de la solución**

Run: `cd C:\Agentes\PropertyMap && dotnet build src/PropertyMap.Web/PropertyMap.Web.sln`
Expected: `Compilación correcta. 0 Errores`

- [ ] **Step 3: Confirmar manualmente (lectura de archivo, no requiere levantar la app) que appsettings.json ya no tiene el secret en texto plano**

Run: `cd C:\Agentes\PropertyMap\src && grep -n "Secret" PropertyMap.Api/appsettings.json`
Expected: `"Secret": "",` (vacío, sin el valor hardcodeado original)

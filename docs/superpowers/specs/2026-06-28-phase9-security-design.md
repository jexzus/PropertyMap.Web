# Phase 9.3 — Security Audit — Design

**Status:** Approved
**Scope:** Tercer sub-proyecto de Phase 9 (Escala & Calidad). Orden completo de Phase 9: 1) Clustering (✅), 2) Testing exhaustivo (✅), 3) Security audit (este spec), 4) Audit logs, 5) Búsqueda avanzada.

## Contexto

El roadmap original describe esta tarea como "Pentest básico, rate limiting, validaciones". Se descartó un pentest formal (no hay herramientas de pentest automatizado en este entorno ni tiempo para uno manual real) en favor de un repaso dirigido de la postura de seguridad actual, encontrando 4 gaps reales concretos durante la exploración inicial:

1. **Sin rate limiting** en ningún endpoint de `PropertyMap.Api` — riesgo de fuerza bruta y abuso/DoS.
2. **Sin lockout** de cuentas tras intentos fallidos de login — `AuthController.Login` usa `UserManager.CheckPasswordAsync` directamente (no `SignInManager.PasswordSignInAsync`), por lo que el lockout de Identity no se aplica aunque se configure `options.Lockout.*` — hay que wirearlo manualmente.
3. **Sin validaciones de longitud máxima** en DTOs de request expuestos a texto libre (`Titulo`, `Descripcion`, `Nombre`, etc.) — riesgo de abuso de almacenamiento/payloads gigantes.
4. **Secret JWT hardcodeado en texto plano** en `appsettings.json`, commiteado al repo (`"propertymap-super-secret-key-change-in-production-min32chars!"`).

Un quinto gap conocido (`Routes.razor` usando `RouteView` en vez de `AuthorizeRouteView`) **ya estaba resuelto** antes de este spec (commit `8aa0b24`, descubierto durante la exploración de este sub-proyecto) — no requiere trabajo nuevo, se menciona solo para que quede registrado que se verificó.

El upload de imágenes (`ImageService.cs`) ya tiene allowlist de extensiones (`.jpg/.jpeg/.png/.webp`) y límite de tamaño (10MB) — no es un gap, no requiere cambios.

## Alcance

4 frentes independientes, todos en `PropertyMap.Api` (el frontend Blazor no requiere cambios en este sub-proyecto):

### 1. Rate limiting

Middleware nativo de .NET 9 (`Microsoft.AspNetCore.RateLimiting`, ya incluido en el SDK, sin paquete NuGet nuevo). Dos políticas:

- **Global** (`options.GlobalLimiter`): fixed window por IP, 100 requests/minuto. Generoso para no afectar el uso normal del mapa (polling de listings, clustering, etc.).
- **`"auth"`** (política nombrada, `options.AddFixedWindowLimiter`): 5 requests / 15 minutos, aplicada a `AuthController` completo vía `[EnableRateLimiting("auth")]` a nivel de clase — cubre login, register, confirm-email, forgot/reset-password con el mismo límite estricto, ya que todos comparten el mismo riesgo de abuso (spam de emails, enumeración de usuarios, fuerza bruta).
- `options.RejectionStatusCode = StatusCodes.Status429TooManyRequests`.
- `app.UseRateLimiter()` se agrega después de `app.UseAuthentication()`/`app.UseAuthorization()`, antes de `app.MapControllers()`.

### 2. Lockout de cuentas

Configuración base en `AddIdentity` (`Program.cs`):
```csharp
options.Lockout.MaxFailedAccessAttempts = 3;
options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
options.Lockout.AllowedForNewUsers = true;
```

Pero esto **no alcanza por sí solo** — `AuthController.Login` debe wirear manualmente las llamadas a `UserManager`:

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
    // ... resto del método sin cambios (EmailConfirmed, Estado, generación de tokens)
}
```

423 (Locked) es el código HTTP semánticamente correcto para "la cuenta existe pero está temporalmente bloqueada", distinto de 401 (credenciales incorrectas) y 429 (rate limit — un concepto relacionado pero separado: rate limit es por IP/ventana de tiempo sin importar la cuenta, lockout es por cuenta sin importar la IP).

### 3. Validaciones de longitud máxima

DataAnnotations (`[StringLength]`) en los campos de texto libre de los DTOs de request más expuestos:
- `RegisterRequest`: `Nombre`/`Apellido` ≤ 100 caracteres.
- `CreateListingRequest`: `Titulo` ≤ 150, `Descripcion` ≤ 5000, `DireccionTexto`/`Ciudad`/`Provincia` ≤ 200.
- `CreateAlertRequest`/`CreateReportRequest`: cualquier campo de texto libre sin límite actual (a confirmar campo por campo durante la implementación, leyendo cada DTO real).

`[ApiController]` ya devuelve 400 automáticamente cuando falla la validación de DataAnnotations — no se necesita código adicional en los controllers ni `ModelState.IsValid` explícito.

### 4. Secret JWT fuera del repo

- `appsettings.json`: `JwtSettings:Secret` pasa a `""`.
- El código ya tiene (verificar en `Program.cs`) un patrón `?? throw new InvalidOperationException(...)` para la connection string — aplicar el mismo patrón al leer el secret, para que el arranque falle con un mensaje claro si no está configurado, en vez de generar tokens con una clave vacía.
- Documentar en el spec (no requiere código) el comando para desarrollo local: `dotnet user-secrets set "JwtSettings:Secret" "<valor-largo-aleatorio>"` ejecutado desde `PropertyMap.Api/`. En producción, variable de entorno `JwtSettings__Secret` (doble guión bajo, convención de ASP.NET Core para configuración jerárquica).

## Testing

Tests de integración nuevos (mismo patrón xUnit + EF InMemory + `WebApplicationFactory` ya usado en los 18 archivos existentes):
- Rate limit: exceder el límite de la política `"auth"` en `/api/auth/login` devuelve 429 en el intento que excede el contador.
- Lockout: 3 intentos fallidos de login devuelven 401 cada uno; el 4° intento (con cualquier contraseña, incluso la correcta) devuelve 423; un login exitoso antes de alcanzar el límite resetea el contador (verificado con `UserManager.GetAccessFailedCountAsync`).
- Validaciones: un `Titulo` de 200 caracteres en `CreateListingRequest` (excede el límite de 150) devuelve 400.

Sin tests E2E reales ni pentest automatizado (fuera de scope, decisión ya tomada). El cambio del secret JWT no requiere test nuevo — es config, no lógica.

## Fuera de scope

- Pentest formal (automatizado o manual real) — no hay herramientas ni alcance para esto en este entorno.
- Rate limiting granular por usuario autenticado (solo por IP en esta iteración) — válido para el volumen de tráfico actual del proyecto.
- Auditoría de dependencias/CVEs más allá del warning NU1902 de MailKit ya conocido (eso se evaluará si corresponde en `Phase 9.4` o por separado).
- Cualquier cambio en `Routes.razor` — ya resuelto, sin trabajo pendiente.

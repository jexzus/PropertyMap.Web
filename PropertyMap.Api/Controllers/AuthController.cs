using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PropertyMap.Core.DTOs.Auth;
using PropertyMap.Core.Entities;
using PropertyMap.Core.Enums;
using PropertyMap.Core.Interfaces;
using PropertyMap.Infrastructure.Data;

namespace PropertyMap.Api.Controllers;

[ApiController]
[Route("api/auth")]
[EnableRateLimiting("auth")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITokenService _tokenService;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _config;
    private readonly AppDbContext _db;
    private readonly IPublisherRepository _publishers;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        ITokenService tokenService,
        IEmailService emailService,
        IConfiguration config,
        AppDbContext db,
        IPublisherRepository publishers)
    {
        _userManager = userManager;
        _tokenService = tokenService;
        _emailService = emailService;
        _config = config;
        _db = db;
        _publishers = publishers;
    }

    // ── Flujo legacy (usado por tests) ────────────────────────────────────────

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        if (request.Password != request.ConfirmPassword)
            return BadRequest(new { message = "Las contraseñas no coinciden." });

        if (await _userManager.FindByEmailAsync(request.Email) != null)
            return Conflict(new { message = "El email ya está registrado." });

        var token = GenerateCode();
        var user = new ApplicationUser
        {
            Nombre = request.Nombre,
            Apellido = request.Apellido,
            Email = request.Email,
            UserName = request.Email,
            Estado = EstadoUsuario.PendienteVerificacion,
            EmailVerificationToken = token,
            EmailVerificationExpiry = DateTime.UtcNow.AddHours(24),
            FechaRegistro = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
            return BadRequest(new { errors = result.Errors.Select(e => e.Description) });

        await _userManager.AddToRoleAsync(user, "User");
        await _emailService.SendEmailVerificationAsync(request.Email, request.Nombre, token);

        return Ok(new { message = "Registro exitoso. Revisá tu email para verificar tu cuenta." });
    }

    [HttpPost("verify-email")]
    public async Task<IActionResult> VerifyEmail(VerifyEmailRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null)
            return NotFound(new { message = "Usuario no encontrado." });

        if (user.EmailVerificationToken != request.Token ||
            user.EmailVerificationExpiry < DateTime.UtcNow)
            return BadRequest(new { message = "Token inválido o expirado." });

        user.EmailConfirmed = true;
        user.Estado = EstadoUsuario.Activo;
        user.EmailVerificationToken = null;
        user.EmailVerificationExpiry = null;
        await _userManager.UpdateAsync(user);
        await _emailService.SendWelcomeAsync(user.Email!, user.Nombre);

        return Ok(new { message = "Email verificado correctamente." });
    }

    // ── Flujo nuevo: registro en 3 pasos ─────────────────────────────────────

    [HttpPost("pre-registro")]
    public async Task<IActionResult> PreRegistro(PreRegistroRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            return BadRequest(new { message = "El email es requerido." });

        if (await _userManager.FindByEmailAsync(request.Email) != null)
            return Conflict(new { message = "El email ya está registrado." });

        // Throttle: no reenviar si hay un token activo de menos de 30 segundos
        var reciente = await _db.PreRegistroTokens
            .Where(t => t.Email == request.Email && t.Tipo == "registro" &&
                        !t.Usado && t.CreatedAt > DateTime.UtcNow.AddSeconds(-30))
            .AnyAsync();
        if (reciente)
            return BadRequest(new { message = "Esperá unos segundos antes de reenviar el código." });

        var codigo = GenerateCode();
        _db.PreRegistroTokens.Add(new PreRegistroToken
        {
            Email = request.Email,
            TokenHash = HashCode(codigo),
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
            Usado = false,
            CreatedAt = DateTime.UtcNow,
            Tipo = "registro"
        });
        await _db.SaveChangesAsync();

        await _emailService.SendEmailVerificationAsync(request.Email, "", codigo);

        return Ok(new { message = "Código enviado al email." });
    }

    [HttpPost("confirmar-pre-registro")]
    public async Task<IActionResult> ConfirmarPreRegistro(ConfirmarPreRegistroRequest request)
    {
        var token = await _db.PreRegistroTokens
            .Where(t => t.Email == request.Email && t.Tipo == "registro" && !t.Usado)
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync();

        if (token == null || token.ExpiresAt < DateTime.UtcNow ||
            token.TokenHash != HashCode(request.Codigo))
            return BadRequest(new { message = "Código inválido o expirado." });

        token.Usado = true;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Código verificado. Podés completar el registro." });
    }

    [HttpPost("registrar")]
    public async Task<IActionResult> Registrar(RegistrarRequest request)
    {
        if (request.Password != request.ConfirmPassword)
            return BadRequest(new { message = "Las contraseñas no coinciden." });

        if (await _userManager.FindByEmailAsync(request.Email) != null)
            return Conflict(new { message = "El email ya está registrado." });

        var user = new ApplicationUser
        {
            Nombre = request.Nombre,
            Apellido = request.Apellido,
            Email = request.Email,
            UserName = request.Email,
            EmailConfirmed = true,
            Estado = EstadoUsuario.Activo,
            FechaRegistro = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
            return BadRequest(new { errors = result.Errors.Select(e => e.Description) });

        await _userManager.AddToRoleAsync(user, "User");
        await _userManager.AddToRoleAsync(user, "Publisher");

        await _publishers.AddAsync(new Publisher
        {
            UserId = user.Id,
            Nombre = $"{request.Nombre} {request.Apellido}".Trim(),
            Email = request.Email,
            Telefono = "",
            Tipo = TipoPublicador.Particular
        });

        await _emailService.SendWelcomeAsync(request.Email, request.Nombre);

        return Ok(new { message = "Cuenta creada exitosamente." });
    }

    // ── Flujo nuevo: recuperar contraseña en 2 pasos ──────────────────────────

    [HttpPost("solicitar-recuperacion")]
    public async Task<IActionResult> SolicitarRecuperacion(SolicitarRecuperacionRequest request)
    {
        const string safeMsg = "Si el email está registrado, recibirás un código en breve.";

        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null || !user.EmailConfirmed)
            return Ok(new { message = safeMsg });

        var reciente = await _db.PreRegistroTokens
            .Where(t => t.Email == request.Email && t.Tipo == "recuperacion" &&
                        !t.Usado && t.CreatedAt > DateTime.UtcNow.AddSeconds(-30))
            .AnyAsync();
        if (reciente)
            return BadRequest(new { message = "Esperá unos segundos antes de reenviar el código." });

        var codigo = GenerateCode();
        _db.PreRegistroTokens.Add(new PreRegistroToken
        {
            Email = request.Email,
            TokenHash = HashCode(codigo),
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
            Usado = false,
            CreatedAt = DateTime.UtcNow,
            Tipo = "recuperacion"
        });
        await _db.SaveChangesAsync();

        await _emailService.SendCodigoRecuperacionAsync(request.Email, user.Nombre, codigo);

        return Ok(new { message = safeMsg });
    }

    [HttpPost("cambiar-contrasena")]
    public async Task<IActionResult> CambiarContrasena(CambiarContrasenaRequest request)
    {
        var token = await _db.PreRegistroTokens
            .Where(t => t.Email == request.Email && t.Tipo == "recuperacion" && !t.Usado)
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync();

        if (token == null || token.ExpiresAt < DateTime.UtcNow ||
            token.TokenHash != HashCode(request.Codigo))
            return BadRequest(new { message = "Código inválido o expirado." });

        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null)
            return BadRequest(new { message = "Usuario no encontrado." });

        var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, resetToken, request.NuevaContrasena);
        if (!result.Succeeded)
            return BadRequest(new { errors = result.Errors.Select(e => e.Description) });

        token.Usado = true;
        await _db.SaveChangesAsync();

        return Ok(new { message = "Contraseña actualizada correctamente." });
    }

    // ── Login / Refresh (sin cambios) ─────────────────────────────────────────

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

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(RefreshTokenRequest request)
    {
        var principal = _tokenService.ValidateExpiredToken(request.AccessToken);
        if (principal == null)
            return Unauthorized(new { message = "Token inválido." });

        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = await _userManager.FindByIdAsync(userId!);
        if (user == null ||
            user.RefreshToken != request.RefreshToken ||
            user.RefreshTokenExpiry < DateTime.UtcNow)
            return Unauthorized(new { message = "Refresh token inválido o expirado." });

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

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string GenerateCode() =>
        Random.Shared.Next(100000, 999999).ToString();

    private static string HashCode(string code)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(code));
        return Convert.ToHexString(bytes);
    }
}

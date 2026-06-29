using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using PropertyMap.Core.DTOs.Auth;
using PropertyMap.Core.Entities;
using PropertyMap.Core.Enums;
using PropertyMap.Core.Interfaces;

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

    public AuthController(
        UserManager<ApplicationUser> userManager,
        ITokenService tokenService,
        IEmailService emailService,
        IConfiguration config)
    {
        _userManager = userManager;
        _tokenService = tokenService;
        _emailService = emailService;
        _config = config;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        if (request.Password != request.ConfirmPassword)
            return BadRequest(new { message = "Las contraseñas no coinciden." });

        if (await _userManager.FindByEmailAsync(request.Email) != null)
            return Conflict(new { message = "El email ya está registrado." });

        var token = GenerateVerificationCode();
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

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null || !await _userManager.CheckPasswordAsync(user, request.Password))
            return Unauthorized(new { message = "Credenciales incorrectas." });

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

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request)
    {
        const string safeMessage = "Si el email existe, recibirás instrucciones en breve.";
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null || !user.EmailConfirmed)
            return Ok(new { message = safeMessage });

        user.PasswordResetToken = _tokenService.GenerateRefreshToken();
        user.PasswordResetExpiry = DateTime.UtcNow.AddHours(1);
        await _userManager.UpdateAsync(user);

        var resetUrl = _config["AppSettings:FrontendUrl"] ?? "https://propertymap.com.ar";
        await _emailService.SendPasswordResetAsync(
            user.Email!, user.Nombre, user.PasswordResetToken, $"{resetUrl}/reset-password");

        return Ok(new { message = safeMessage });
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request)
    {
        if (request.NewPassword != request.ConfirmNewPassword)
            return BadRequest(new { message = "Las contraseñas no coinciden." });

        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null ||
            user.PasswordResetToken != request.Token ||
            user.PasswordResetExpiry < DateTime.UtcNow)
            return BadRequest(new { message = "Token inválido o expirado." });

        var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, resetToken, request.NewPassword);
        if (!result.Succeeded)
            return BadRequest(new { errors = result.Errors.Select(e => e.Description) });

        user.PasswordResetToken = null;
        user.PasswordResetExpiry = null;
        await _userManager.UpdateAsync(user);

        return Ok(new { message = "Contraseña restablecida correctamente." });
    }

    private static string GenerateVerificationCode() =>
        Random.Shared.Next(100000, 999999).ToString();
}

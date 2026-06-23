using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using MimeKit;
using PropertyMap.Core.Interfaces;

namespace PropertyMap.Infrastructure.Services;

public class EmailService : IEmailService
{
    private readonly IConfiguration _config;

    public EmailService(IConfiguration config)
    {
        _config = config;
    }

    private async Task SendAsync(string toEmail, string toName, string subject, string htmlBody)
    {
        var smtp = _config.GetSection("SmtpSettings");
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(smtp["FromName"], smtp["FromEmail"]));
        message.To.Add(new MailboxAddress(toName, toEmail));
        message.Subject = subject;
        message.Body = new TextPart("html") { Text = htmlBody };

        using var client = new SmtpClient();
        await client.ConnectAsync(smtp["Host"], int.Parse(smtp["Port"] ?? "587"), SecureSocketOptions.StartTls);
        await client.AuthenticateAsync(smtp["Username"], smtp["Password"]);
        await client.SendAsync(message);
        await client.DisconnectAsync(true);
    }

    public async Task SendEmailVerificationAsync(string toEmail, string toName, string token)
    {
        var html = $"""
            <!DOCTYPE html>
            <html>
            <body style="font-family:system-ui,sans-serif;max-width:480px;margin:0 auto;padding:24px">
              <h2 style="color:#be123c">Verificá tu cuenta</h2>
              <p>Hola <strong>{toName}</strong>,</p>
              <p>Tu código de verificación es:</p>
              <div style="font-size:40px;font-weight:bold;letter-spacing:12px;color:#1a1a1a;padding:16px 0">{token}</div>
              <p style="color:#666">El código expira en 24 horas.</p>
            </body>
            </html>
            """;
        await SendAsync(toEmail, toName, "Verificá tu cuenta en PropertyMap", html);
    }

    public async Task SendPasswordResetAsync(string toEmail, string toName, string token, string resetUrl)
    {
        var link = $"{resetUrl}?token={Uri.EscapeDataString(token)}&email={Uri.EscapeDataString(toEmail)}";
        var html = $"""
            <!DOCTYPE html>
            <html>
            <body style="font-family:system-ui,sans-serif;max-width:480px;margin:0 auto;padding:24px">
              <h2 style="color:#be123c">Recuperar contraseña</h2>
              <p>Hola <strong>{toName}</strong>,</p>
              <p>Recibimos una solicitud para restablecer tu contraseña.</p>
              <p><a href="{link}" style="background:#be123c;color:white;padding:12px 24px;border-radius:8px;text-decoration:none;display:inline-block">Restablecer contraseña</a></p>
              <p style="color:#666">El link expira en 1 hora.</p>
            </body>
            </html>
            """;
        await SendAsync(toEmail, toName, "Restablecé tu contraseña en PropertyMap", html);
    }

    public async Task SendWelcomeAsync(string toEmail, string toName)
    {
        var html = $"""
            <!DOCTYPE html>
            <html>
            <body style="font-family:system-ui,sans-serif;max-width:480px;margin:0 auto;padding:24px">
              <h2 style="color:#be123c">¡Bienvenido a PropertyMap!</h2>
              <p>Hola <strong>{toName}</strong>,</p>
              <p>Tu cuenta está activa. Ya podés buscar propiedades, guardar favoritos y configurar alertas.</p>
              <p><a href="https://propertymap.com.ar" style="background:#be123c;color:white;padding:12px 24px;border-radius:8px;text-decoration:none;display:inline-block">Ir a PropertyMap</a></p>
            </body>
            </html>
            """;
        await SendAsync(toEmail, toName, "¡Bienvenido a PropertyMap!", html);
    }

    public async Task SendNuevaConsultaAsync(
        string toEmail, string publisherNombre,
        string propertyTitulo, string userNombre, string mensaje)
    {
        var html = $"""
            <!DOCTYPE html>
            <html>
            <body style="font-family:system-ui,sans-serif;max-width:480px;margin:0 auto;padding:24px">
              <h2 style="color:#be123c">Nueva consulta</h2>
              <p>Hola <strong>{publisherNombre}</strong>,</p>
              <p><strong>{userNombre}</strong> te envió una consulta sobre <strong>{propertyTitulo}</strong>:</p>
              <blockquote style="border-left:3px solid #be123c;padding-left:12px;color:#333;margin:16px 0">
                {mensaje}
              </blockquote>
              <p>Respondé desde tu panel en PropertyMap.</p>
            </body>
            </html>
            """;
        await SendAsync(toEmail, publisherNombre, $"Nueva consulta sobre {propertyTitulo}", html);
    }

    public async Task SendNuevaRespuestaAsync(
        string toEmail, string userNombre,
        string propertyTitulo, string publisherNombre, string mensaje)
    {
        var html = $"""
            <!DOCTYPE html>
            <html>
            <body style="font-family:system-ui,sans-serif;max-width:480px;margin:0 auto;padding:24px">
              <h2 style="color:#be123c">Respuesta a tu consulta</h2>
              <p>Hola <strong>{userNombre}</strong>,</p>
              <p><strong>{publisherNombre}</strong> respondió tu consulta sobre <strong>{propertyTitulo}</strong>:</p>
              <blockquote style="border-left:3px solid #be123c;padding-left:12px;color:#333;margin:16px 0">
                {mensaje}
              </blockquote>
              <p>Ver la conversación completa en PropertyMap.</p>
            </body>
            </html>
            """;
        await SendAsync(toEmail, userNombre, $"Respuesta de {publisherNombre}", html);
    }
}

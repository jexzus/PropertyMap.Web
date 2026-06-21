namespace PropertyMap.Core.Interfaces;

public interface IEmailService
{
    Task SendEmailVerificationAsync(string toEmail, string toName, string token);
    Task SendPasswordResetAsync(string toEmail, string toName, string token, string resetUrl);
    Task SendWelcomeAsync(string toEmail, string toName);
}

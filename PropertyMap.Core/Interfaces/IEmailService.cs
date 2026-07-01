namespace PropertyMap.Core.Interfaces;

public interface IEmailService
{
    Task SendEmailVerificationAsync(string toEmail, string toName, string token);
    Task SendPasswordResetAsync(string toEmail, string toName, string token, string resetUrl);
    Task SendWelcomeAsync(string toEmail, string toName);

    Task SendNuevaConsultaAsync(
        string toEmail, string publisherNombre,
        string propertyTitulo, string userNombre, string mensaje);

    Task SendNuevaRespuestaAsync(
        string toEmail, string userNombre,
        string propertyTitulo, string publisherNombre, string mensaje);

    Task SendAlertMatchAsync(
        string toEmail, string userNombre,
        string alertNombre, string propertyTitulo, int propertyId);

    Task SendReportConfirmationAsync(string toEmail, string userNombre, string propertyTitulo);
    Task SendCodigoRecuperacionAsync(string toEmail, string toName, string codigo);
}

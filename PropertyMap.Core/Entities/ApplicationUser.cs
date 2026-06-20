using Microsoft.AspNetCore.Identity;
using PropertyMap.Core.Enums;

namespace PropertyMap.Core.Entities;

public class ApplicationUser : IdentityUser
{
    public string Nombre { get; set; } = "";
    public string Apellido { get; set; } = "";
    public string? AvatarUrl { get; set; }
    public EstadoUsuario Estado { get; set; } = EstadoUsuario.PendienteVerificacion;
    public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiry { get; set; }
    public string? EmailVerificationToken { get; set; }
    public DateTime? EmailVerificationExpiry { get; set; }
    public string? PasswordResetToken { get; set; }
    public DateTime? PasswordResetExpiry { get; set; }

    public Publisher? Publisher { get; set; }
    public ICollection<Notification> Notifications { get; set; } = [];
    public NotificationPreference? NotificationPreference { get; set; }
    public ICollection<PropertyFavorite> Favorites { get; set; } = [];
    public ICollection<PropertyRating> PropertyRatings { get; set; } = [];
    public ICollection<AgentRating> AgentRatings { get; set; } = [];
    public ICollection<PropertyQuestion> Questions { get; set; } = [];
    public ICollection<Alert> Alerts { get; set; } = [];
    public Subscription? Subscription { get; set; }
}

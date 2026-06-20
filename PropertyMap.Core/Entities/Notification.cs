using PropertyMap.Core.Enums;

namespace PropertyMap.Core.Entities;

public class Notification
{
    public int Id { get; set; }
    public string UserId { get; set; } = "";
    public ApplicationUser User { get; set; } = null!;
    public TipoNotificacion Tipo { get; set; }
    public string Titulo { get; set; } = "";
    public string Mensaje { get; set; } = "";
    public bool Leida { get; set; }
    public string? UrlAccion { get; set; }
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
}

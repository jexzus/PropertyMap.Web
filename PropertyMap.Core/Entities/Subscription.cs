using PropertyMap.Core.Enums;

namespace PropertyMap.Core.Entities;

public class Subscription
{
    public int Id { get; set; }
    public string UserId { get; set; } = "";
    public ApplicationUser User { get; set; } = null!;
    public int PlanId { get; set; }
    public Plan Plan { get; set; } = null!;
    public EstadoSuscripcion Estado { get; set; } = EstadoSuscripcion.Activa;
    public DateTime FechaInicio { get; set; } = DateTime.UtcNow;
    public DateTime FechaVencimiento { get; set; }
    public bool AutoRenovar { get; set; } = true;
}

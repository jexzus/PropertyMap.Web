using PropertyMap.Core.Enums;

namespace PropertyMap.Core.Entities;

public class Report
{
    public int Id { get; set; }
    public int PropertyListingId { get; set; }
    public PropertyListing PropertyListing { get; set; } = null!;
    public string UserId { get; set; } = "";
    public ApplicationUser User { get; set; } = null!;
    public MotivoReporte Motivo { get; set; }
    public string? Descripcion { get; set; }
    public EstadoReporte Estado { get; set; } = EstadoReporte.Pendiente;
    public DateTime FechaReporte { get; set; } = DateTime.UtcNow;
}

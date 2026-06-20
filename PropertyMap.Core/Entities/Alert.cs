using PropertyMap.Core.Enums;

namespace PropertyMap.Core.Entities;

public class Alert
{
    public int Id { get; set; }
    public string UserId { get; set; } = "";
    public ApplicationUser User { get; set; } = null!;
    public string? Nombre { get; set; }
    public TipoOperacion? Operacion { get; set; }
    public TipoPropiedad? TipoPropiedad { get; set; }
    public string? Ciudad { get; set; }
    public decimal? PrecioMax { get; set; }
    public string? Moneda { get; set; }
    public int? DormitoriosMin { get; set; }
    public bool Activa { get; set; } = true;
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
}

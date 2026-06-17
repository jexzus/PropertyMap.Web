using PropertyMap.Core.Enums;

namespace PropertyMap.Core.Entities;

public class PropertyListing
{
    public int Id { get; set; }
    public int PublisherId { get; set; }
    public Publisher Publisher { get; set; } = null!;
    public int LocationId { get; set; }
    public Location Location { get; set; } = null!;
    public string Titulo { get; set; } = "";
    public string Descripcion { get; set; } = "";
    public decimal Precio { get; set; }
    public string Moneda { get; set; } = "USD";
    public TipoPropiedad TipoPropiedad { get; set; }
    public TipoOperacion Operacion { get; set; }
    public decimal? Superficie { get; set; }
    public decimal? SuperficieCubierta { get; set; }
    public int? Ambientes { get; set; }
    public int? Dormitorios { get; set; }
    public int? Banos { get; set; }
    public int? Antiguedad { get; set; }
    public bool Cochera { get; set; }
    public List<string> Amenities { get; set; } = [];
    public List<string> Fotos { get; set; } = [];
    public EstadoPropiedad Estado { get; set; } = EstadoPropiedad.Activa;
    public DateTime FechaPublicacion { get; set; } = DateTime.UtcNow;
}

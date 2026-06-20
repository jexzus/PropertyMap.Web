namespace PropertyMap.Core.Entities;

public class PropertyRating
{
    public int Id { get; set; }
    public int PropertyListingId { get; set; }
    public PropertyListing PropertyListing { get; set; } = null!;
    public string UserId { get; set; } = "";
    public ApplicationUser User { get; set; } = null!;
    public int PuntajeUbicacion { get; set; }
    public int PuntajeEstado { get; set; }
    public int PuntajePrecioCalidad { get; set; }
    public string? Comentario { get; set; }
    public DateTime FechaValoracion { get; set; } = DateTime.UtcNow;
}

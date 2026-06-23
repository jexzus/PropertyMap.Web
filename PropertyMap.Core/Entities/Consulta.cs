// PropertyMap.Core/Entities/Consulta.cs
namespace PropertyMap.Core.Entities;

public class Consulta
{
    public int Id { get; set; }
    public int PropertyListingId { get; set; }
    public string UserId { get; set; } = "";
    public DateTime FechaCreacion { get; set; }
    public DateTime FechaUltimoMensaje { get; set; }

    public PropertyListing PropertyListing { get; set; } = null!;
    public ApplicationUser User { get; set; } = null!;
    public ICollection<ConsultaMensaje> Mensajes { get; set; } = [];
}

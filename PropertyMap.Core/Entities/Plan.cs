namespace PropertyMap.Core.Entities;

public class Plan
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public string Slug { get; set; } = "";
    public decimal PrecioMensual { get; set; }
    public string Moneda { get; set; } = "ARS";
    public int? MaxPublicaciones { get; set; }
    public int DestacadosIncluidos { get; set; }
    public bool EstadisticasAvanzadas { get; set; }
    public bool Activo { get; set; } = true;

    public ICollection<Subscription> Subscriptions { get; set; } = [];
}

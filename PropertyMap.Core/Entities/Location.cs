namespace PropertyMap.Core.Entities;

public class Location
{
    public int Id { get; set; }
    public double Latitud { get; set; }
    public double Longitud { get; set; }
    public string DireccionTexto { get; set; } = "";
    public string Ciudad { get; set; } = "";
    public string Provincia { get; set; } = "";
    public string Pais { get; set; } = "Argentina";
    public ICollection<PropertyListing> Listings { get; set; } = [];
}

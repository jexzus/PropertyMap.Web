namespace PropertyMap.Core.Entities;

public class PropertyImage
{
    public int Id { get; set; }
    public int PropertyListingId { get; set; }
    public PropertyListing PropertyListing { get; set; } = null!;
    public string Url { get; set; } = "";
    public int Orden { get; set; }
    public bool EsPrincipal { get; set; }
}

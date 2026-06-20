namespace PropertyMap.Core.Entities;

public class PropertyFavorite
{
    public int Id { get; set; }
    public int PropertyListingId { get; set; }
    public PropertyListing PropertyListing { get; set; } = null!;
    public string UserId { get; set; } = "";
    public ApplicationUser User { get; set; } = null!;
    public DateTime FechaAgregado { get; set; } = DateTime.UtcNow;
}

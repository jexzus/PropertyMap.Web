namespace PropertyMap.Core.Entities;

public class PropertyView
{
    public int Id { get; set; }
    public int PropertyListingId { get; set; }
    public PropertyListing PropertyListing { get; set; } = null!;
    public string? UserId { get; set; }
    public ApplicationUser? User { get; set; }
    public DateTime FechaVista { get; set; } = DateTime.UtcNow;
    public string? IpAddress { get; set; }
}

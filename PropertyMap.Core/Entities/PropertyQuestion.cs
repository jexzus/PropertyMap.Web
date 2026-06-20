namespace PropertyMap.Core.Entities;

public class PropertyQuestion
{
    public int Id { get; set; }
    public int PropertyListingId { get; set; }
    public PropertyListing PropertyListing { get; set; } = null!;
    public string UserId { get; set; } = "";
    public ApplicationUser User { get; set; } = null!;
    public string Mensaje { get; set; } = "";
    public DateTime FechaPregunta { get; set; } = DateTime.UtcNow;

    public ICollection<PropertyAnswer> Answers { get; set; } = [];
}

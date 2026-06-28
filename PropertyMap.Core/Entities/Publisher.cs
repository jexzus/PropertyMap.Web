using System.Text.Json.Serialization;
using PropertyMap.Core.Enums;

namespace PropertyMap.Core.Entities;

public class Publisher
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public string Email { get; set; } = "";
    public TipoPublicador Tipo { get; set; }
    public string? LogoUrl { get; set; }
    public string? Telefono { get; set; }
    public string UserId { get; set; } = ""; // FK a AspNetUsers.Id
    [JsonIgnore]
    public ICollection<PropertyListing> Listings { get; set; } = [];
    public ApplicationUser? User { get; set; }
    public ICollection<AgentRating> Ratings { get; set; } = [];
}

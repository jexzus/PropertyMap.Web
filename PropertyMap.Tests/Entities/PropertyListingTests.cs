using PropertyMap.Core.Entities;
using PropertyMap.Core.Enums;

namespace PropertyMap.Tests.Entities;

public class PropertyListingTests
{
    [Fact]
    public void NewListing_HasDefaultState_Borrador()
    {
        var listing = new PropertyListing();
        Assert.Equal(EstadoPublicacion.Borrador, listing.Estado);
    }

    [Fact]
    public void NewListing_HasEmptyImagesList()
    {
        var listing = new PropertyListing();
        Assert.NotNull(listing.Images);
        Assert.Empty(listing.Images);
    }

    [Fact]
    public void NewListing_FechaPublicacion_IsSet()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        var listing = new PropertyListing();
        Assert.True(listing.FechaPublicacion >= before);
    }
}

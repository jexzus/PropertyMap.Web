using PropertyMap.Core.Entities;
using PropertyMap.Core.Enums;

namespace PropertyMap.Tests.Entities;

public class PropertyListingTests
{
    [Fact]
    public void NewListing_HasDefaultState_Activa()
    {
        var listing = new PropertyListing();
        Assert.Equal(EstadoPropiedad.Activa, listing.Estado);
    }

    [Fact]
    public void NewListing_HasEmptyFotosList()
    {
        var listing = new PropertyListing();
        Assert.NotNull(listing.Fotos);
        Assert.Empty(listing.Fotos);
    }

    [Fact]
    public void NewListing_FechaPublicacion_IsSet()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        var listing = new PropertyListing();
        Assert.True(listing.FechaPublicacion >= before);
    }
}

using Microsoft.EntityFrameworkCore;
using PropertyMap.Core.Entities;
using PropertyMap.Core.Enums;
using PropertyMap.Infrastructure.Data;

namespace PropertyMap.Tests.Data;

public class AppDbContextTests
{
    private AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task CanSave_And_Retrieve_PropertyListing()
    {
        using var ctx = CreateContext();

        var location = new Location
        {
            Latitud = -34.6037,
            Longitud = -58.3816,
            DireccionTexto = "Florida 123",
            Ciudad = "Buenos Aires"
        };
        var publisher = new Publisher
        {
            Nombre = "Inmobiliaria Test",
            Email = "test@test.com",
            Tipo = TipoPublicador.Inmobiliaria,
            UserId = "user-1"
        };
        ctx.Locations.Add(location);
        ctx.Publishers.Add(publisher);
        await ctx.SaveChangesAsync();

        var listing = new PropertyListing
        {
            Titulo = "Departamento en Florida",
            Precio = 150000,
            Moneda = "USD",
            TipoPropiedad = TipoPropiedad.Departamento,
            Operacion = TipoOperacion.Venta,
            LocationId = location.Id,
            PublisherId = publisher.Id
        };
        ctx.PropertyListings.Add(listing);
        await ctx.SaveChangesAsync();

        var saved = await ctx.PropertyListings
            .Include(l => l.Location)
            .Include(l => l.Publisher)
            .FirstOrDefaultAsync(l => l.Id == listing.Id);

        Assert.NotNull(saved);
        Assert.Equal("Departamento en Florida", saved.Titulo);
        Assert.Equal("Buenos Aires", saved.Location.Ciudad);
        Assert.Equal("Inmobiliaria Test", saved.Publisher.Nombre);
    }

    [Fact]
    public async Task MultipleListings_SameLocation_IsAllowed()
    {
        using var ctx = CreateContext();

        var location = new Location { Latitud = -34.6, Longitud = -58.3, DireccionTexto = "Av. Corrientes 1000" };
        var pub1 = new Publisher { Nombre = "Pub A", Email = "a@a.com", Tipo = TipoPublicador.Inmobiliaria, UserId = "u1" };
        var pub2 = new Publisher { Nombre = "Pub B", Email = "b@b.com", Tipo = TipoPublicador.Particular, UserId = "u2" };
        ctx.Locations.Add(location);
        ctx.Publishers.AddRange(pub1, pub2);
        await ctx.SaveChangesAsync();

        ctx.PropertyListings.AddRange(
            new PropertyListing { Titulo = "Piso 1", Precio = 80000, Moneda = "USD", TipoPropiedad = TipoPropiedad.Departamento, Operacion = TipoOperacion.Venta, LocationId = location.Id, PublisherId = pub1.Id },
            new PropertyListing { Titulo = "Piso 2", Precio = 90000, Moneda = "USD", TipoPropiedad = TipoPropiedad.Departamento, Operacion = TipoOperacion.Venta, LocationId = location.Id, PublisherId = pub2.Id }
        );
        await ctx.SaveChangesAsync();

        var count = await ctx.PropertyListings.CountAsync(l => l.LocationId == location.Id);
        Assert.Equal(2, count);
    }
}

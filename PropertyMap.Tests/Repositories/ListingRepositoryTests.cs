using Microsoft.EntityFrameworkCore;
using PropertyMap.Core.Entities;
using PropertyMap.Core.Enums;
using PropertyMap.Infrastructure.Data;
using PropertyMap.Infrastructure.Repositories;

namespace PropertyMap.Tests.Repositories;

public class ListingRepositoryTests
{
    private AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static (Location location, Publisher publisher) CreateBaseData()
    {
        var location = new Location
        {
            Latitud = -34.6,
            Longitud = -58.3,
            DireccionTexto = "Corrientes 1000",
            Ciudad = "Buenos Aires"
        };
        var publisher = new Publisher
        {
            Nombre = "Test Publisher",
            Email = "test@test.com",
            Tipo = TipoPublicador.Particular,
            UserId = "user-1"
        };
        return (location, publisher);
    }

    [Fact]
    public async Task GetActiveListings_ReturnsOnlyActiveListings()
    {
        using var ctx = CreateContext();
        var repo = new ListingRepository(ctx);
        var (location, publisher) = CreateBaseData();
        ctx.Locations.Add(location);
        ctx.Publishers.Add(publisher);
        await ctx.SaveChangesAsync();

        ctx.PropertyListings.AddRange(
            new PropertyListing { Titulo = "Activa", Precio = 1, Moneda = "USD", TipoPropiedad = TipoPropiedad.Casa, Operacion = TipoOperacion.Venta, LocationId = location.Id, PublisherId = publisher.Id, Estado = EstadoPropiedad.Activa },
            new PropertyListing { Titulo = "Pausada", Precio = 1, Moneda = "USD", TipoPropiedad = TipoPropiedad.Casa, Operacion = TipoOperacion.Venta, LocationId = location.Id, PublisherId = publisher.Id, Estado = EstadoPropiedad.Pausada },
            new PropertyListing { Titulo = "Vendida", Precio = 1, Moneda = "USD", TipoPropiedad = TipoPropiedad.Casa, Operacion = TipoOperacion.Venta, LocationId = location.Id, PublisherId = publisher.Id, Estado = EstadoPropiedad.Vendida }
        );
        await ctx.SaveChangesAsync();

        var result = await repo.GetActiveListingsAsync();

        Assert.Single(result);
        Assert.Equal("Activa", result.First().Titulo);
    }

    [Fact]
    public async Task GetListingsByPublisher_ReturnsOnlyThatPublishersListings()
    {
        using var ctx = CreateContext();
        var repo = new ListingRepository(ctx);
        var (location, pub1) = CreateBaseData();
        var pub2 = new Publisher { Nombre = "Otro", Email = "b@b.com", Tipo = TipoPublicador.Inmobiliaria, UserId = "user-2" };
        ctx.Locations.Add(location);
        ctx.Publishers.AddRange(pub1, pub2);
        await ctx.SaveChangesAsync();

        ctx.PropertyListings.AddRange(
            new PropertyListing { Titulo = "P1-L1", Precio = 1, Moneda = "USD", TipoPropiedad = TipoPropiedad.Casa, Operacion = TipoOperacion.Venta, LocationId = location.Id, PublisherId = pub1.Id },
            new PropertyListing { Titulo = "P1-L2", Precio = 1, Moneda = "USD", TipoPropiedad = TipoPropiedad.Casa, Operacion = TipoOperacion.Venta, LocationId = location.Id, PublisherId = pub1.Id },
            new PropertyListing { Titulo = "P2-L1", Precio = 1, Moneda = "USD", TipoPropiedad = TipoPropiedad.Casa, Operacion = TipoOperacion.Venta, LocationId = location.Id, PublisherId = pub2.Id }
        );
        await ctx.SaveChangesAsync();

        var result = await repo.GetListingsByPublisherAsync(pub1.Id);

        Assert.Equal(2, result.Count());
        Assert.All(result, l => Assert.Equal(pub1.Id, l.PublisherId));
    }

    [Fact]
    public async Task GetActiveListingsForMap_ReturnsDtosWithCoordinates()
    {
        using var ctx = CreateContext();
        var repo = new ListingRepository(ctx);
        var (location, publisher) = CreateBaseData();
        ctx.Locations.Add(location);
        ctx.Publishers.Add(publisher);
        await ctx.SaveChangesAsync();

        ctx.PropertyListings.Add(new PropertyListing
        {
            Titulo = "Depto test",
            Precio = 100000,
            Moneda = "USD",
            TipoPropiedad = TipoPropiedad.Departamento,
            Operacion = TipoOperacion.Venta,
            Estado = EstadoPropiedad.Activa,
            LocationId = location.Id,
            PublisherId = publisher.Id
        });
        await ctx.SaveChangesAsync();

        var result = (await repo.GetActiveListingsForMapAsync()).ToList();

        Assert.Single(result);
        Assert.Equal(-34.6, result[0].Lat);
        Assert.Equal(-58.3, result[0].Lng);
        Assert.Equal("Departamento", result[0].TipoPropiedad);
    }

    [Fact]
    public async Task AddListing_PersistsToDatabase()
    {
        using var ctx = CreateContext();
        var repo = new ListingRepository(ctx);
        var (location, publisher) = CreateBaseData();
        ctx.Locations.Add(location);
        ctx.Publishers.Add(publisher);
        await ctx.SaveChangesAsync();

        var listing = new PropertyListing
        {
            Titulo = "Nueva propiedad",
            Precio = 200000,
            Moneda = "USD",
            TipoPropiedad = TipoPropiedad.Departamento,
            Operacion = TipoOperacion.Venta,
            LocationId = location.Id,
            PublisherId = publisher.Id
        };

        await repo.AddAsync(listing);

        Assert.True(listing.Id > 0);
        Assert.Equal(1, await ctx.PropertyListings.CountAsync());
    }

    [Fact]
    public async Task DeleteListing_RemovesFromDatabase()
    {
        using var ctx = CreateContext();
        var repo = new ListingRepository(ctx);
        var (location, publisher) = CreateBaseData();
        ctx.Locations.Add(location);
        ctx.Publishers.Add(publisher);
        await ctx.SaveChangesAsync();

        var listing = new PropertyListing
        {
            Titulo = "A eliminar",
            Precio = 1,
            Moneda = "USD",
            TipoPropiedad = TipoPropiedad.Casa,
            Operacion = TipoOperacion.Venta,
            LocationId = location.Id,
            PublisherId = publisher.Id
        };
        ctx.PropertyListings.Add(listing);
        await ctx.SaveChangesAsync();

        await repo.DeleteAsync(listing.Id);

        Assert.Equal(0, await ctx.PropertyListings.CountAsync());
    }
}

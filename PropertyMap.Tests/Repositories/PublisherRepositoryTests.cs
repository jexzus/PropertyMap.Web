using Microsoft.EntityFrameworkCore;
using PropertyMap.Core.Entities;
using PropertyMap.Core.Enums;
using PropertyMap.Infrastructure.Data;
using PropertyMap.Infrastructure.Repositories;

namespace PropertyMap.Tests.Repositories;

public class PublisherRepositoryTests
{
    private AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task GetByUserId_ReturnsPublisher_WhenExists()
    {
        using var ctx = CreateContext();
        var repo = new PublisherRepository(ctx);

        ctx.Publishers.Add(new Publisher
        {
            Nombre = "Mi Inmobiliaria",
            Email = "m@m.com",
            Tipo = TipoPublicador.Inmobiliaria,
            UserId = "user-abc"
        });
        await ctx.SaveChangesAsync();

        var result = await repo.GetByUserIdAsync("user-abc");

        Assert.NotNull(result);
        Assert.Equal("Mi Inmobiliaria", result.Nombre);
    }

    [Fact]
    public async Task GetByUserId_ReturnsNull_WhenNotExists()
    {
        using var ctx = CreateContext();
        var repo = new PublisherRepository(ctx);

        var result = await repo.GetByUserIdAsync("nobody");

        Assert.Null(result);
    }

    [Fact]
    public async Task AddPublisher_PersistsToDatabase()
    {
        using var ctx = CreateContext();
        var repo = new PublisherRepository(ctx);

        var publisher = new Publisher
        {
            Nombre = "Nuevo Publisher",
            Email = "nuevo@test.com",
            Tipo = TipoPublicador.Particular,
            UserId = "user-new"
        };

        await repo.AddAsync(publisher);

        Assert.True(publisher.Id > 0);
        Assert.Equal(1, await ctx.Publishers.CountAsync());
    }
}

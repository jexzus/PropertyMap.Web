using Microsoft.EntityFrameworkCore;
using PropertyMap.Core.DTOs.Properties;
using PropertyMap.Core.Entities;
using PropertyMap.Core.Interfaces;
using PropertyMap.Infrastructure.Data;

namespace PropertyMap.Infrastructure.Repositories;

public class FavoriteRepository(AppDbContext ctx) : IFavoriteRepository
{
    public async Task AddAsync(PropertyFavorite favorite)
    {
        var exists = await IsFavoritedAsync(favorite.PropertyListingId, favorite.UserId);
        if (!exists)
        {
            ctx.PropertyFavorites.Add(favorite);
            await ctx.SaveChangesAsync();
        }
    }

    public async Task RemoveAsync(int listingId, string userId)
    {
        var fav = await ctx.PropertyFavorites
            .FirstOrDefaultAsync(f => f.PropertyListingId == listingId && f.UserId == userId);
        if (fav is not null)
        {
            ctx.PropertyFavorites.Remove(fav);
            await ctx.SaveChangesAsync();
        }
    }

    public async Task<List<MyListingDto>> GetByUserAsync(string userId) =>
        await ctx.PropertyFavorites
            .Where(f => f.UserId == userId)
            .OrderByDescending(f => f.FechaAgregado)
            .Select(f => new MyListingDto(
                f.PropertyListing.Id,
                f.PropertyListing.Titulo,
                f.PropertyListing.Location.DireccionTexto,
                f.PropertyListing.Location.Ciudad,
                f.PropertyListing.Precio,
                f.PropertyListing.Moneda,
                f.PropertyListing.TipoPropiedad.ToString(),
                f.PropertyListing.Operacion.ToString(),
                f.PropertyListing.Estado.ToString(),
                f.PropertyListing.Images.Where(i => i.EsPrincipal).Select(i => i.Url).FirstOrDefault(),
                f.PropertyListing.FechaPublicacion,
                f.PropertyListing.FechaActualizacion
            ))
            .ToListAsync();

    public async Task<bool> IsFavoritedAsync(int listingId, string userId) =>
        await ctx.PropertyFavorites
            .AnyAsync(f => f.PropertyListingId == listingId && f.UserId == userId);

    public async Task<int> GetCountAsync(int listingId) =>
        await ctx.PropertyFavorites.CountAsync(f => f.PropertyListingId == listingId);
}

using Microsoft.EntityFrameworkCore;
using PropertyMap.Core.DTOs;
using PropertyMap.Core.Entities;
using PropertyMap.Core.Enums;
using PropertyMap.Core.Interfaces;
using PropertyMap.Infrastructure.Data;

namespace PropertyMap.Infrastructure.Repositories;

public class ListingRepository(AppDbContext ctx) : IListingRepository
{
    public async Task<IEnumerable<PropertyListing>> GetActiveListingsAsync() =>
        await ctx.PropertyListings
            .Where(l => l.Estado == EstadoPublicacion.Publicada)
            .Include(l => l.Location)
            .Include(l => l.Publisher)
            .ToListAsync();

    public async Task<IEnumerable<PropertyListing>> GetListingsByPublisherAsync(int publisherId) =>
        await ctx.PropertyListings
            .Where(l => l.PublisherId == publisherId)
            .Include(l => l.Location)
            .OrderByDescending(l => l.FechaPublicacion)
            .ToListAsync();

    public async Task<IEnumerable<ListingMapDto>> GetActiveListingsForMapAsync() =>
        await ctx.PropertyListings
            .Where(l => l.Estado == EstadoPublicacion.Publicada)
            .Include(l => l.Location)
            .Select(l => new ListingMapDto(
                l.Id,
                l.Location.Latitud,
                l.Location.Longitud,
                l.Titulo,
                l.Precio,
                l.Moneda,
                l.TipoPropiedad.ToString(),
                l.Operacion.ToString(),
                l.Images.Where(i => i.EsPrincipal).Select(i => i.Url).FirstOrDefault()
            ))
            .ToListAsync();

    public async Task<PropertyListing?> GetByIdAsync(int id) =>
        await ctx.PropertyListings
            .Include(l => l.Location)
            .Include(l => l.Publisher)
            .FirstOrDefaultAsync(l => l.Id == id);

    public async Task<PropertyListing> AddAsync(PropertyListing listing)
    {
        ctx.PropertyListings.Add(listing);
        await ctx.SaveChangesAsync();
        return listing;
    }

    public async Task UpdateAsync(PropertyListing listing)
    {
        ctx.PropertyListings.Update(listing);
        await ctx.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var listing = await ctx.PropertyListings.FindAsync(id);
        if (listing is not null)
        {
            ctx.PropertyListings.Remove(listing);
            await ctx.SaveChangesAsync();
        }
    }
}

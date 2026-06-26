using Microsoft.EntityFrameworkCore;
using PropertyMap.Core.DTOs;
using PropertyMap.Core.DTOs.Admin;
using PropertyMap.Core.DTOs.Properties;
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
            .OrderByDescending(l => l.Destacado)
            .ThenByDescending(l => l.FechaPublicacion)
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
            .OrderByDescending(l => l.Destacado)
            .ThenByDescending(l => l.FechaPublicacion)
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

    public async Task<ListingDetailDto?> GetByIdAsDetailAsync(int id)
    {
        var listing = await ctx.PropertyListings
            .Include(l => l.Location)
            .Include(l => l.Publisher)
            .Include(l => l.Images.OrderBy(i => i.Orden))
            .FirstOrDefaultAsync(l => l.Id == id && l.Estado == EstadoPublicacion.Publicada);

        if (listing == null) return null;

        return new ListingDetailDto(
            Id: listing.Id,
            Titulo: listing.Titulo,
            Descripcion: listing.Descripcion,
            Precio: listing.Precio,
            Moneda: listing.Moneda,
            TipoPropiedad: listing.TipoPropiedad.ToString(),
            Operacion: listing.Operacion.ToString(),
            DireccionTexto: listing.Location.DireccionTexto,
            Ciudad: listing.Location.Ciudad,
            Provincia: listing.Location.Provincia,
            Lat: listing.Location.Latitud,
            Lng: listing.Location.Longitud,
            Superficie: listing.Superficie,
            SuperficieCubierta: listing.SuperficieCubierta,
            Ambientes: listing.Ambientes,
            Dormitorios: listing.Dormitorios,
            Banos: listing.Banos,
            Antiguedad: listing.Antiguedad,
            Cochera: listing.Cochera,
            Amenities: listing.Amenities,
            FotoUrls: listing.Images.Select(i => i.Url).ToList(),
            PublisherNombre: listing.Publisher.Nombre,
            PublisherTelefono: listing.Publisher.Telefono,
            PublisherLogoUrl: listing.Publisher.LogoUrl,
            FechaPublicacion: listing.FechaPublicacion
        );
    }

    public async Task<IEnumerable<MyListingDto>> GetMyListingsAsync(int publisherId) =>
        await ctx.PropertyListings
            .Where(l => l.PublisherId == publisherId)
            .Include(l => l.Location)
            .Include(l => l.Images.Where(i => i.EsPrincipal))
            .OrderByDescending(l => l.FechaActualizacion)
            .Select(l => new MyListingDto(
                l.Id,
                l.Titulo,
                l.Location.DireccionTexto,
                l.Location.Ciudad,
                l.Precio,
                l.Moneda,
                l.TipoPropiedad.ToString(),
                l.Operacion.ToString(),
                l.Estado.ToString(),
                l.Images.Where(i => i.EsPrincipal).Select(i => i.Url).FirstOrDefault(),
                l.FechaPublicacion,
                l.FechaActualizacion
            ))
            .ToListAsync();

    public async Task<IEnumerable<PendingListingDto>> GetPendingListingsAsync() =>
        await ctx.PropertyListings
            .Where(l => l.Estado == EstadoPublicacion.PendienteAprobacion)
            .Include(l => l.Location)
            .Include(l => l.Publisher)
            .Include(l => l.Images.Where(i => i.EsPrincipal))
            .OrderBy(l => l.FechaPublicacion)
            .Select(l => new PendingListingDto(
                l.Id,
                l.Titulo,
                l.Location.DireccionTexto,
                l.Location.Ciudad,
                l.Precio,
                l.Moneda,
                l.TipoPropiedad.ToString(),
                l.Operacion.ToString(),
                l.Images.Where(i => i.EsPrincipal).Select(i => i.Url).FirstOrDefault(),
                l.Publisher.Nombre,
                l.Publisher.Email,
                l.FechaPublicacion
            ))
            .ToListAsync();

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

using Microsoft.EntityFrameworkCore;
using PropertyMap.Core.DTOs.Stats;
using PropertyMap.Core.Interfaces;
using PropertyMap.Infrastructure.Data;

namespace PropertyMap.Infrastructure.Repositories;

public class ListingStatsRepository(AppDbContext ctx) : IListingStatsRepository
{
    public async Task<ListingStatsDto?> GetForListingAsync(int listingId, int publisherId)
    {
        var listing = await ctx.PropertyListings
            .FirstOrDefaultAsync(l => l.Id == listingId && l.PublisherId == publisherId);
        if (listing is null) return null;

        return await BuildStatsAsync(listing.Id, listing.Titulo);
    }

    public async Task<List<ListingStatsDto>> GetForPublisherAsync(int publisherId)
    {
        var listings = await ctx.PropertyListings
            .Where(l => l.PublisherId == publisherId)
            .Select(l => new { l.Id, l.Titulo })
            .ToListAsync();

        var result = new List<ListingStatsDto>();
        foreach (var l in listings)
            result.Add(await BuildStatsAsync(l.Id, l.Titulo));

        return result;
    }

    private async Task<ListingStatsDto> BuildStatsAsync(int listingId, string titulo)
    {
        var vistas = await ctx.PropertyViews.CountAsync(v => v.PropertyListingId == listingId);
        var favoritos = await ctx.PropertyFavorites.CountAsync(f => f.PropertyListingId == listingId);
        var consultas = await ctx.Consultas.CountAsync(c => c.PropertyListingId == listingId);

        // Conversión = consulta con al menos un mensaje de respuesta del publisher.
        var consultaIds = await ctx.Consultas
            .Where(c => c.PropertyListingId == listingId)
            .Select(c => c.Id)
            .ToListAsync();

        var conversiones = 0;
        if (consultaIds.Count > 0)
        {
            var consultasConRespuesta = await ctx.ConsultaMensajes
                .Where(m => consultaIds.Contains(m.ConsultaId) && m.EsDelPublisher)
                .Select(m => m.ConsultaId)
                .Distinct()
                .CountAsync();
            conversiones = consultasConRespuesta;
        }

        return new ListingStatsDto(listingId, titulo, vistas, favoritos, consultas, conversiones);
    }
}

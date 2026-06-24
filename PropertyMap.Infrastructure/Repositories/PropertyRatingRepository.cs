// PropertyMap.Infrastructure/Repositories/PropertyRatingRepository.cs
using Microsoft.EntityFrameworkCore;
using PropertyMap.Core.DTOs.Ratings;
using PropertyMap.Core.Entities;
using PropertyMap.Core.Interfaces;
using PropertyMap.Infrastructure.Data;

namespace PropertyMap.Infrastructure.Repositories;

public class PropertyRatingRepository : IPropertyRatingRepository
{
    private readonly AppDbContext _ctx;

    public PropertyRatingRepository(AppDbContext ctx)
    {
        _ctx = ctx;
    }

    public async Task<bool> HasConsultaAsync(int listingId, string userId)
    {
        return await _ctx.Consultas
            .AnyAsync(c => c.PropertyListingId == listingId && c.UserId == userId);
    }

    public async Task<PropertyRating?> GetByUserAndListingAsync(int listingId, string userId)
    {
        return await _ctx.PropertyRatings
            .FirstOrDefaultAsync(r => r.PropertyListingId == listingId && r.UserId == userId);
    }

    public async Task AddOrUpdateAsync(PropertyRating rating)
    {
        var existing = await _ctx.PropertyRatings
            .FirstOrDefaultAsync(r => r.UserId == rating.UserId && r.PropertyListingId == rating.PropertyListingId);

        if (existing is null)
        {
            _ctx.PropertyRatings.Add(rating);
        }
        else
        {
            existing.PuntajeUbicacion = rating.PuntajeUbicacion;
            existing.PuntajeEstado = rating.PuntajeEstado;
            existing.PuntajePrecioCalidad = rating.PuntajePrecioCalidad;
            existing.Comentario = rating.Comentario;
            existing.FechaValoracion = rating.FechaValoracion;
        }

        await _ctx.SaveChangesAsync();
    }

    public async Task<PropertyRatingStatsDto> GetStatsAsync(int listingId)
    {
        var ratings = await _ctx.PropertyRatings
            .Where(r => r.PropertyListingId == listingId)
            .ToListAsync();

        if (ratings.Count == 0)
            return new PropertyRatingStatsDto(0, 0, 0, 0, 0);

        var promedioUbicacion = ratings.Average(r => (double)r.PuntajeUbicacion);
        var promedioEstado    = ratings.Average(r => (double)r.PuntajeEstado);
        var promedioPrecio    = ratings.Average(r => (double)r.PuntajePrecioCalidad);
        var promedioGeneral   = (promedioUbicacion + promedioEstado + promedioPrecio) / 3.0;

        return new PropertyRatingStatsDto(
            Math.Round(promedioUbicacion, 2),
            Math.Round(promedioEstado, 2),
            Math.Round(promedioPrecio, 2),
            Math.Round(promedioGeneral, 2),
            ratings.Count);
    }
}

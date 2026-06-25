using Microsoft.EntityFrameworkCore;
using PropertyMap.Core.DTOs.Alerts;
using PropertyMap.Core.Entities;
using PropertyMap.Core.Interfaces;
using PropertyMap.Infrastructure.Data;

namespace PropertyMap.Infrastructure.Repositories;

public class AlertRepository(AppDbContext ctx) : IAlertRepository
{
    public async Task<Alert> AddAsync(Alert alert)
    {
        ctx.Alerts.Add(alert);
        await ctx.SaveChangesAsync();
        return alert;
    }

    public async Task<List<AlertDto>> GetByUserAsync(string userId) =>
        await ctx.Alerts
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.FechaCreacion)
            .Select(a => new AlertDto(
                a.Id, a.Nombre, a.Operacion, a.TipoPropiedad, a.Ciudad,
                a.PrecioMax, a.Moneda, a.DormitoriosMin, a.Activa, a.FechaCreacion))
            .ToListAsync();

    public async Task<Alert?> GetByIdAsync(int id) =>
        await ctx.Alerts.FirstOrDefaultAsync(a => a.Id == id);

    public async Task UpdateAsync(Alert alert)
    {
        ctx.Alerts.Update(alert);
        await ctx.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var alert = await ctx.Alerts.FindAsync(id);
        if (alert is not null)
        {
            ctx.Alerts.Remove(alert);
            await ctx.SaveChangesAsync();
        }
    }

    public async Task<List<Alert>> GetActiveMatchingAsync(PropertyListing listing)
    {
        // Two-step materialization: EF InMemory no soporta bien combinaciones
        // de null-coalescing en Where complejos contra navegación Location.
        var candidates = await ctx.Alerts
            .Include(a => a.User)
            .Where(a => a.Activa)
            .ToListAsync();

        return candidates.Where(a =>
            (a.Operacion == null || a.Operacion == listing.Operacion) &&
            (a.TipoPropiedad == null || a.TipoPropiedad == listing.TipoPropiedad) &&
            (a.Ciudad == null || a.Ciudad == listing.Location.Ciudad) &&
            (a.PrecioMax == null || listing.Precio <= a.PrecioMax) &&
            (a.DormitoriosMin == null || (listing.Dormitorios != null && listing.Dormitorios >= a.DormitoriosMin))
        ).ToList();
    }
}

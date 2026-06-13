using Microsoft.EntityFrameworkCore;
using PropertyMap.Core.Entities;
using PropertyMap.Core.Interfaces;
using PropertyMap.Infrastructure.Data;

namespace PropertyMap.Infrastructure.Repositories;

public class LocationRepository(AppDbContext ctx) : ILocationRepository
{
    public async Task<Location?> FindByCoordinatesAsync(double lat, double lng, double toleranceMeters = 10)
    {
        double toleranceDeg = toleranceMeters / 111000.0;
        return await ctx.Locations.FirstOrDefaultAsync(l =>
            Math.Abs(l.Latitud - lat) < toleranceDeg &&
            Math.Abs(l.Longitud - lng) < toleranceDeg);
    }

    public async Task<Location> AddAsync(Location location)
    {
        ctx.Locations.Add(location);
        await ctx.SaveChangesAsync();
        return location;
    }
}

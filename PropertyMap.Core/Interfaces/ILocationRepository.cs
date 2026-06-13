using PropertyMap.Core.Entities;

namespace PropertyMap.Core.Interfaces;

public interface ILocationRepository
{
    Task<Location?> FindByCoordinatesAsync(double lat, double lng, double toleranceMeters = 10);
    Task<Location> AddAsync(Location location);
}
     
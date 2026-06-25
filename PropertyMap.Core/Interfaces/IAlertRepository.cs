using PropertyMap.Core.DTOs.Alerts;
using PropertyMap.Core.Entities;

namespace PropertyMap.Core.Interfaces;

public interface IAlertRepository
{
    Task<Alert> AddAsync(Alert alert);
    Task<List<AlertDto>> GetByUserAsync(string userId);
    Task<Alert?> GetByIdAsync(int id);
    Task UpdateAsync(Alert alert);
    Task DeleteAsync(int id);
    Task<List<Alert>> GetActiveMatchingAsync(PropertyListing listing);
}

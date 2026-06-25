using PropertyMap.Core.DTOs.Alerts;

namespace PropertyMap.Web.Services;

public interface IAlertsApiService
{
    Task<List<AlertDto>> GetMyAlertsAsync();
    Task<bool> CreateAsync(CreateAlertRequest request);
    Task<bool> ToggleAsync(int id);
    Task<bool> DeleteAsync(int id);
}

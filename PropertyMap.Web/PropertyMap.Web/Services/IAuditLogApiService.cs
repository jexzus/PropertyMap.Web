using PropertyMap.Core.DTOs.Admin;

namespace PropertyMap.Web.Services;

public interface IAuditLogApiService
{
    Task<List<AuditLogDto>> GetRecentAsync();
}

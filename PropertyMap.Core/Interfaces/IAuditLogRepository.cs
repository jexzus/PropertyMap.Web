using PropertyMap.Core.DTOs.Admin;
using PropertyMap.Core.Entities;

namespace PropertyMap.Core.Interfaces;

public interface IAuditLogRepository
{
    Task AddAsync(AuditLog log);
    Task<List<AuditLogDto>> GetRecentAsync(int take = 50);
}

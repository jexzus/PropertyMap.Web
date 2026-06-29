using Microsoft.EntityFrameworkCore;
using PropertyMap.Core.DTOs.Admin;
using PropertyMap.Core.Entities;
using PropertyMap.Core.Interfaces;
using PropertyMap.Infrastructure.Data;

namespace PropertyMap.Infrastructure.Repositories;

public class AuditLogRepository(AppDbContext ctx) : IAuditLogRepository
{
    public async Task AddAsync(AuditLog log)
    {
        ctx.AuditLogs.Add(log);
        await ctx.SaveChangesAsync();
    }

    public async Task<List<AuditLogDto>> GetRecentAsync(int take = 50) =>
        await ctx.AuditLogs
            .OrderByDescending(a => a.FechaAccion)
            .Take(take)
            .Select(a => new AuditLogDto(
                a.Id, a.UserId, a.Accion, a.Entidad, a.EntidadId,
                a.Detalles, a.FechaAccion, a.IpAddress))
            .ToListAsync();
}

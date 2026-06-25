using Microsoft.EntityFrameworkCore;
using PropertyMap.Core.DTOs.Reports;
using PropertyMap.Core.Entities;
using PropertyMap.Core.Enums;
using PropertyMap.Core.Interfaces;
using PropertyMap.Infrastructure.Data;

namespace PropertyMap.Infrastructure.Repositories;

public class ReportRepository(AppDbContext ctx) : IReportRepository
{
    public async Task<Report> AddAsync(Report report)
    {
        ctx.Reports.Add(report);
        await ctx.SaveChangesAsync();
        return report;
    }

    public async Task<Report?> GetByIdAsync(int id) =>
        await ctx.Reports
            .Include(r => r.PropertyListing)
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.Id == id);

    public async Task<List<ReportDto>> GetPendingAsync() =>
        await ctx.Reports
            .Include(r => r.PropertyListing)
            .Include(r => r.User)
            .Where(r => r.Estado == EstadoReporte.Pendiente || r.Estado == EstadoReporte.EnRevision)
            .OrderBy(r => r.FechaReporte)
            .Select(r => new ReportDto(
                r.Id, r.PropertyListingId, r.PropertyListing.Titulo,
                $"{r.User.Nombre} {r.User.Apellido}", r.Motivo, r.Descripcion,
                r.Estado, r.FechaReporte))
            .ToListAsync();

    public async Task UpdateAsync(Report report)
    {
        ctx.Reports.Update(report);
        await ctx.SaveChangesAsync();
    }
}

using Microsoft.EntityFrameworkCore;
using PropertyMap.Core.DTOs.Plans;
using PropertyMap.Core.Entities;
using PropertyMap.Core.Interfaces;
using PropertyMap.Infrastructure.Data;

namespace PropertyMap.Infrastructure.Repositories;

public class PlanRepository(AppDbContext ctx) : IPlanRepository
{
    public async Task<List<PlanDto>> GetActiveAsync() =>
        await ctx.Plans
            .Where(p => p.Activo)
            .OrderBy(p => p.PrecioMensual)
            .Select(p => new PlanDto(
                p.Id, p.Nombre, p.Slug, p.PrecioMensual, p.Moneda,
                p.MaxPublicaciones, p.DestacadosIncluidos, p.EstadisticasAvanzadas))
            .ToListAsync();

    public async Task<Plan?> GetByIdAsync(int id) =>
        await ctx.Plans.FirstOrDefaultAsync(p => p.Id == id);
}

using Microsoft.EntityFrameworkCore;
using PropertyMap.Core.Entities;
using PropertyMap.Core.Enums;
using PropertyMap.Core.Interfaces;
using PropertyMap.Infrastructure.Data;

namespace PropertyMap.Infrastructure.Repositories;

public class SubscriptionRepository(AppDbContext ctx) : ISubscriptionRepository
{
    public async Task<Subscription?> GetByUserIdAsync(string userId) =>
        await ctx.Subscriptions
            .Include(s => s.Plan)
            .FirstOrDefaultAsync(s => s.UserId == userId);

    public async Task<Subscription> CreateOrReplaceAsync(string userId, int planId, DateTime vencimiento)
    {
        // Subscription tiene índice único en UserId — una sola fila activa por usuario.
        var existing = await ctx.Subscriptions.FirstOrDefaultAsync(s => s.UserId == userId);
        if (existing is null)
        {
            existing = new Subscription
            {
                UserId = userId,
                PlanId = planId,
                Estado = EstadoSuscripcion.Activa,
                FechaInicio = DateTime.UtcNow,
                FechaVencimiento = vencimiento,
                AutoRenovar = true
            };
            ctx.Subscriptions.Add(existing);
        }
        else
        {
            existing.PlanId = planId;
            existing.Estado = EstadoSuscripcion.Activa;
            existing.FechaInicio = DateTime.UtcNow;
            existing.FechaVencimiento = vencimiento;
        }
        await ctx.SaveChangesAsync();

        return await ctx.Subscriptions.Include(s => s.Plan).FirstAsync(s => s.UserId == userId);
    }
}

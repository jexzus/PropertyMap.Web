using Microsoft.EntityFrameworkCore;
using PropertyMap.Core.Entities;
using PropertyMap.Core.Interfaces;
using PropertyMap.Infrastructure.Data;

namespace PropertyMap.Infrastructure.Repositories;

public class PublisherRepository(AppDbContext ctx) : IPublisherRepository
{
    public async Task<Publisher?> GetByUserIdAsync(string userId) =>
        await ctx.Publishers.FirstOrDefaultAsync(p => p.UserId == userId);

    public async Task<Publisher?> GetByIdAsync(int id) =>
        await ctx.Publishers
            .Include(p => p.Listings)
            .FirstOrDefaultAsync(p => p.Id == id);

    public async Task<Publisher> AddAsync(Publisher publisher)
    {
        ctx.Publishers.Add(publisher);
        await ctx.SaveChangesAsync();
        return publisher;
    }

    public async Task UpdateAsync(Publisher publisher)
    {
        ctx.Publishers.Update(publisher);
        await ctx.SaveChangesAsync();
    }
}

using PropertyMap.Core.Entities;

namespace PropertyMap.Core.Interfaces;

public interface IPublisherRepository
{
    Task<Publisher?> GetByUserIdAsync(string userId);
    Task<Publisher?> GetByIdAsync(int id);
    Task<Publisher> AddAsync(Publisher publisher);
    Task UpdateAsync(Publisher publisher);
}

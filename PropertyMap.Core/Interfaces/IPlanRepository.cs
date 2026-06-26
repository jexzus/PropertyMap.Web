using PropertyMap.Core.DTOs.Plans;
using PropertyMap.Core.Entities;

namespace PropertyMap.Core.Interfaces;

public interface IPlanRepository
{
    Task<List<PlanDto>> GetActiveAsync();
    Task<Plan?> GetByIdAsync(int id);
}

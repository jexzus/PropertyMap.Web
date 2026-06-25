using PropertyMap.Core.DTOs.Reports;
using PropertyMap.Core.Entities;
using PropertyMap.Core.Enums;

namespace PropertyMap.Core.Interfaces;

public interface IReportRepository
{
    Task<Report> AddAsync(Report report);
    Task<Report?> GetByIdAsync(int id);
    Task<List<ReportDto>> GetPendingAsync();
    Task UpdateAsync(Report report);
}

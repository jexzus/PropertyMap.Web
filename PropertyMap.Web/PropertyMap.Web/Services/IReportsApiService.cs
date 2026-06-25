using PropertyMap.Core.DTOs.Reports;

namespace PropertyMap.Web.Services;

public interface IReportsApiService
{
    Task<bool> CreateAsync(CreateReportRequest request);
}

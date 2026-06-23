using PropertyMap.Core.DTOs.Consultas;

namespace PropertyMap.Web.Services;

public interface IConsultasApiService
{
    Task<ConsultaDetailDto?> CreateOrContinueAsync(int listingId, string mensaje);
    Task<List<ConsultaSummaryDto>> GetMyConsultasAsync();
    Task<List<ConsultaSummaryDto>> GetPublisherConsultasAsync();
    Task<ConsultaDetailDto?> GetDetailAsync(int consultaId);
    Task<ConsultaMensajeDto?> ReplyAsync(int consultaId, string mensaje);
}

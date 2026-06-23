// PropertyMap.Core/Interfaces/IConsultaRepository.cs
using PropertyMap.Core.DTOs.Consultas;
using PropertyMap.Core.Entities;

namespace PropertyMap.Core.Interfaces;

public interface IConsultaRepository
{
    Task<Consulta> GetOrCreateAsync(int listingId, string userId);
    Task<ConsultaDetailDto?> GetByIdAsync(int consultaId, string requesterId);
    Task<List<ConsultaSummaryDto>> GetByUserAsync(string userId);
    Task<List<ConsultaSummaryDto>> GetByPublisherAsync(string publisherUserId);
    Task<ConsultaMensajeDto> AddMessageAsync(ConsultaMensaje message);
    Task<bool> CanPublisherReplyAsync(int consultaId, string publisherUserId);
    Task<string?> GetConsultaOwnerUserIdAsync(int consultaId);
    Task CreateNotificationAsync(Notification notification);
}

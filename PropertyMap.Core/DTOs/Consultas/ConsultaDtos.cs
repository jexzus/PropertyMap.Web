namespace PropertyMap.Core.DTOs.Consultas;

public record CreateConsultaRequest(int ListingId, string Mensaje);

public record SendMensajeRequest(string Mensaje);

public record ConsultaSummaryDto(
    int Id,
    int PropertyListingId,
    string PropertyTitulo,
    string UltimoMensaje,
    bool UltimoEsDelPublisher,
    DateTime FechaUltimoMensaje);

public record ConsultaMensajeDto(
    int Id,
    string SenderNombre,
    bool EsDelPublisher,
    string Mensaje,
    DateTime FechaEnvio);

public record ConsultaDetailDto(
    int Id,
    int PropertyListingId,
    string PropertyTitulo,
    List<ConsultaMensajeDto> Mensajes);

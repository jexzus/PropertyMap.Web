using PropertyMap.Core.Enums;

namespace PropertyMap.Core.DTOs.Notifications;

public record NotificationDto(
    int Id,
    TipoNotificacion Tipo,
    string Titulo,
    string Mensaje,
    bool Leida,
    string? UrlAccion,
    DateTime FechaCreacion
);

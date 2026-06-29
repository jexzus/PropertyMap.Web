namespace PropertyMap.Core.DTOs.Admin;

public record AuditLogDto(
    int Id,
    string? UserId,
    string Accion,
    string Entidad,
    string EntidadId,
    string? Detalles,
    DateTime FechaAccion,
    string? IpAddress
);

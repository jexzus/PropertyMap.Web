using PropertyMap.Core.Enums;

namespace PropertyMap.Core.DTOs.Reports;

public record ReportDto(
    int Id,
    int PropertyListingId,
    string PropertyTitulo,
    string UserNombre,
    MotivoReporte Motivo,
    string? Descripcion,
    EstadoReporte Estado,
    DateTime FechaReporte
);

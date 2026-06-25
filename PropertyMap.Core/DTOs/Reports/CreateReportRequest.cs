using PropertyMap.Core.Enums;

namespace PropertyMap.Core.DTOs.Reports;

public record CreateReportRequest(
    int PropertyListingId,
    MotivoReporte Motivo,
    string? Descripcion
);

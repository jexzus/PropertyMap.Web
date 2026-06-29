using System.ComponentModel.DataAnnotations;
using PropertyMap.Core.Enums;

namespace PropertyMap.Core.DTOs.Reports;

public record CreateReportRequest(
    int PropertyListingId,
    MotivoReporte Motivo,
    [StringLength(1000)] string? Descripcion
);
